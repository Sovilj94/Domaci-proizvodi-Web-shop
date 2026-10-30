using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using EPiljara.Application.Mapping;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EPiljara.Application.Features.Korpice;

/// <summary>
/// Kupčev editor sadržaja korpice. Sve tri tačke (lista, detalji, izmjena) dijele isti pojam
/// „korpica koja se još može mijenjati" — vidi <see cref="MojaKorpicaPravila"/>.
/// </summary>
public static class MojaKorpicaPravila
{
    /// <summary>
    /// Statusi narudžbe u kojima se sadržaj još smije mijenjati. Od Packing nadalje su pakeri
    /// već dobili listu, pa izmjena koja se ne vidi u magacinu nije izmjena nego laž kupcu.
    /// </summary>
    public static readonly OrderStatus[] StatusiKojiDozvoljavaju =
        [OrderStatus.Pending, OrderStatus.Confirmed];

    /// <summary>
    /// Učitava proizvod stavke narudžbe i potvrđuje da ta stavka JESTE korpica.
    ///
    /// Provjerava se <see cref="Product.Vrsta"/>, a ne <c>KorpicaStavke.Any()</c>: postojanje
    /// redova sadržaja je POSLJEDICA toga što je stavka korpica, a posljedica se može
    /// izgubiti (sadržaj te sedmice obrisan) dok činjenica ostaje. Bez ove straže se PUT može
    /// uperiti u običnu stavku svoje narudžbe — 1 kg krompira — i na nju upisati proizvoljne
    /// redove koje pakerska lista potom sabira i množi sa Quantity.
    ///
    /// <c>IgnoreQueryFilters</c>: korpica je mogla biti soft-obrisana iz kataloga POSLIJE
    /// narudžbe. Bez toga bi ova straža legalnu korpicu pretvorila u 404, a Velicina (i time
    /// minimalan broj artikala) bi tiho pala na Mala.
    ///
    /// Baca <see cref="NotFoundException"/> — istu grešku kao nepostojeći OrderItemId. Za
    /// ovaj endpoint obična stavka narudžbe nije resurs, a identičan odgovor ne daje način da
    /// se „ne postoji" razlikuje od „postoji ali nije korpica".
    /// </summary>
    public static async Task<Product> UcitajKorpicuAsync(
        IAppDbContext db, OrderItem red, CancellationToken ct)
    {
        var korpica = await db.Products.IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == red.ProductId, ct);

        if (korpica is null || korpica.Vrsta != VrstaProizvoda.Korpica)
            throw new NotFoundException("Korpica nije pronađena.");

        return korpica;
    }

    /// <summary>
    /// Sve provjere prije kupčeve izmjene korpice (zamjena, vraćanje): stavka postoji i jeste
    /// korpica, pripada prijavljenom kupcu, narudžba se još ne pakuje, rok nije prošao i za tu
    /// sedmicu postoji objavljen sadržaj sa ciljem. Sadržaj se vraća sa stavkama i zamjenama.
    /// </summary>
    public static async Task<(OrderItem Red, Product Korpica, SedmicniSadrzaj Sadrzaj)> UcitajZaIzmjenuAsync(
        IAppDbContext db, ICurrentUser current, Guid orderItemId, KorpiceOpcije o, CancellationToken ct)
    {
        var red = await db.OrderItems
            .Include(i => i.Order)
            .Include(i => i.KorpicaStavke)
            .FirstOrDefaultAsync(i => i.Id == orderItemId, ct)
            ?? throw new NotFoundException("Korpica nije pronađena.");

        if (!current.IsAuthenticated || red.Order!.CustomerId != current.Id)
            throw new ForbiddenException("Ova korpica nije tvoja.");

        var korpica = await UcitajKorpicuAsync(db, red, ct);

        if (!StatusiKojiDozvoljavaju.Contains(red.Order.Status))
            throw new ConflictException(
                "Narudžba se već pakuje, sadržaj se više ne može mijenjati.", "korpica_se_pakuje");

        var (dan, vrijemeOd, cutoff) = await SedmicaHelper.UcitajRokAsync(db, o, ct);
        var rok = SedmicaHelper
            .DatumIsporuke(red.Order.Godina, red.Order.Sedmica, dan, vrijemeOd)
            .AddHours(-cutoff);
        if (DateTime.UtcNow > rok)
            throw new DomainException("Rok za izmjenu ove korpice je prošao.", "rok_istekao");

        var sadrzaj = await db.SedmicniSadrzaji
            .Include(s => s.Stavke)
            .Include(s => s.Zamjene)
            .FirstOrDefaultAsync(s => s.KorpicaId == red.ProductId
                                      && s.Godina == red.Order.Godina
                                      && s.Sedmica == red.Order.Sedmica, ct);
        if (sadrzaj is null || sadrzaj.CiljnaVrijednost <= 0m)
            throw new DomainException(
                $"Za korpicu {korpica.Name} nema objavljenog sadržaja za tu sedmicu, pa se " +
                "ne može mijenjati.", "korpica_bez_sadrzaja");

        return (red, korpica, sadrzaj);
    }
}

public record GetMojeKorpiceQuery : IRequest<IReadOnlyList<MojaKorpicaDto>>;

public class GetMojeKorpiceHandler(
    IAppDbContext db, ICurrentUser current, IOptions<KorpiceOpcije> opcije)
    : IRequestHandler<GetMojeKorpiceQuery, IReadOnlyList<MojaKorpicaDto>>
{
    public async Task<IReadOnlyList<MojaKorpicaDto>> Handle(
        GetMojeKorpiceQuery q, CancellationToken ct)
    {
        if (!current.IsAuthenticated) return [];

        var (dan, vrijemeOd, cutoff) = await SedmicaHelper.UcitajRokAsync(db, opcije.Value, ct);

        var redovi = await db.OrderItems
            .Include(i => i.Order)
            .Include(i => i.KorpicaStavke)
            .Where(i => i.Order!.CustomerId == current.Id
                        && i.KorpicaStavke.Any()
                        && MojaKorpicaPravila.StatusiKojiDozvoljavaju.Contains(i.Order.Status))
            .ToListAsync(ct);

        var korpiceIdjevi = redovi.Select(r => r.ProductId).Distinct().ToList();

        // IgnoreQueryFilters: korpica soft-obrisana poslije narudžbe ne smije ispasti iz
        // rječnika — inače Velicina padne na Mala i kupac dobije tuđi minimalan broj artikala.
        var korpice = await db.Products.IgnoreQueryFilters()
            .Where(p => korpiceIdjevi.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        // CiljnaVrijednost je ZAMRZNUTA vrijednost sa sedmičnog sadržaja, upisana pri objavi.
        // NE smije se računati iz redova narudžbe: oni se mijenjaju kad kupac izbaci artikal,
        // pa bi budžet uvijek ispadao 100% i cijela provjera iz editora bila besmislena.
        var sedmice = redovi
            .Select(r => (r.ProductId, r.Order!.Godina, r.Order.Sedmica))
            .Distinct()
            .ToList();

        var sadrzaji = await db.SedmicniSadrzaji
            .Where(s => korpiceIdjevi.Contains(s.KorpicaId))
            .Select(s => new { s.KorpicaId, s.Godina, s.Sedmica, s.CiljnaVrijednost })
            .ToListAsync(ct);

        var ciljevi = sadrzaji
            .Where(s => sedmice.Contains((s.KorpicaId, s.Godina, s.Sedmica)))
            .ToDictionary(s => (s.KorpicaId, s.Godina, s.Sedmica), s => s.CiljnaVrijednost);

        var sada = DateTime.UtcNow;

        return redovi
            .Select(i => Sastavi(i, korpice, ciljevi, dan, vrijemeOd, cutoff, opcije.Value))
            .Where(d => d.RokIzmjene > sada)
            .OrderBy(d => d.RokIzmjene)
            .ToList();
    }

    internal static MojaKorpicaDto Sastavi(
        OrderItem red,
        IReadOnlyDictionary<Guid, Product> korpice,
        IReadOnlyDictionary<(Guid, int, int), decimal> ciljevi,
        DayOfWeek dan, string vrijemeOd, int cutoff,
        KorpiceOpcije opcije)
    {
        korpice.TryGetValue(red.ProductId, out var korpica);
        var o = red.Order!;
        var rok = SedmicaHelper.DatumIsporuke(o.Godina, o.Sedmica, dan, vrijemeOd)
            .AddHours(-cutoff);

        // Kad sadržaja nema (obrisan poslije narudžbe), cilj pada na zbir redova i odnos
        // ispadne 100%. To je ISKLJUČIVO prikaz — da stara narudžba ne obori listu i da se
        // ne dijeli nulom. NIJE dozvola: putanja upisa (IzmijeniMojuKorpicuHandler) u tom
        // stanju odbija izmjenu sa `korpica_bez_sadrzaja`, jer straža koja se sama isključi
        // kad joj nestane ulaz nije straža.
        var trenutna = Math.Round(red.KorpicaStavke.Sum(s => s.Kolicina * s.JedinicnaCijena), 2);
        var cilj = ciljevi.TryGetValue((red.ProductId, o.Godina, o.Sedmica), out var c) && c > 0m
            ? c
            : trenutna;

        return new MojaKorpicaDto
        {
            OrderItemId = red.Id,
            OrderId = o.Id,
            KodNarudzbe = o.Code,
            Naziv = red.ProductName,
            ImageUrl = red.ImageUrl,
            Godina = o.Godina,
            Sedmica = o.Sedmica,
            RokIzmjene = rok,
            CiljnaVrijednost = cilj,
            TrenutnaVrijednost = trenutna,
            BrojArtikala = red.KorpicaStavke.Count,
            MinimalanBrojArtikala = opcije.MinimalanBrojArtikala.GetValueOrDefault(
                korpica?.Velicina ?? VelicinaKorpice.Mala, 5),
        };
    }
}

public record GetMojaKorpicaQuery(Guid OrderItemId) : IRequest<MojaKorpicaDetaljiDto>;

public class GetMojaKorpicaHandler(
    IAppDbContext db, ICurrentUser current, IOptions<KorpiceOpcije> opcije)
    : IRequestHandler<GetMojaKorpicaQuery, MojaKorpicaDetaljiDto>
{
    public async Task<MojaKorpicaDetaljiDto> Handle(GetMojaKorpicaQuery q, CancellationToken ct)
    {
        var red = await db.OrderItems
            .Include(i => i.Order)
            .Include(i => i.KorpicaStavke)
            .FirstOrDefaultAsync(i => i.Id == q.OrderItemId, ct)
            ?? throw new NotFoundException("Korpica nije pronađena.");

        if (!current.IsAuthenticated || red.Order!.CustomerId != current.Id)
            throw new ForbiddenException("Ova korpica nije tvoja.");

        // Obična stavka narudžbe nije korpica i ovaj endpoint joj ne smije izdati ni sadržaj
        // ni ponudu — vidi MojaKorpicaPravila.UcitajKorpicuAsync.
        var korpica = await MojaKorpicaPravila.UcitajKorpicuAsync(db, red, ct);

        // Ponuda je CIJELA sezonska ponuda — odluka P2b. Artikli iz kategorija ove korpice se
        // obilježavaju da ih birač može staviti prve, ali se ništa ne zabranjuje.
        // KorpicaKategorija se vezuje za TipKorpice, ne za Id konkretne korpice — vidi entitet.
        var kategorijeKorpice = korpica.TipKorpice is null
            ? new List<Guid>()
            : await db.KorpicaKategorije
                .Where(k => k.TipKorpice == korpica.TipKorpice)
                .Select(k => k.CategoryId)
                .ToListAsync(ct);

        var mjesec = SedmicaHelper
            .DatumIsporuke(red.Order.Godina, red.Order.Sedmica, DayOfWeek.Monday, "00:00")
            .Month;

        // Predložene zamjene objavljenog sadržaja te sedmice — birač zamjene ih nudi prve.
        var preporucene = await db.ZamjeneSadrzaja
            .Where(z => z.SedmicniSadrzaj!.KorpicaId == red.ProductId
                        && z.SedmicniSadrzaj.Godina == red.Order.Godina
                        && z.SedmicniSadrzaj.Sedmica == red.Order.Sedmica
                        && z.SedmicniSadrzaj.Status != StatusSadrzaja.Nacrt)
            .Select(z => z.ProductId)
            .ToListAsync(ct);

        // Mjeseci su CSV u bazi i ne mogu se filtrirati u SQL-u — bazen se mora materijalizovati
        // (ToListAsync) PRIJE nego što USezoni pređe preko njega u memoriji.
        var ponuda = KorpicaPodobnost
            .USezoni(await KorpicaPodobnost.UpitPodobnih(db.Products).ToListAsync(ct), mjesec)
            .Select(p => new PonudaArtiklaDto
            {
                ProductId = p.Id,
                Naziv = p.Name,
                Jedinica = p.Unit.ToApi(),
                Cijena = p.Price,
                ImageUrl = p.ImageUrl,
                IzOveKorpice = kategorijeKorpice.Contains(p.CategoryId),
                PreporucenaZamjena = preporucene.Contains(p.Id),
            })
            .OrderByDescending(p => p.PreporucenaZamjena)
            .ThenByDescending(p => p.IzOveKorpice)
            .ThenBy(p => p.Naziv)
            .ToList();

        var cilj = await db.SedmicniSadrzaji
            .Where(s => s.KorpicaId == red.ProductId
                        && s.Godina == red.Order.Godina
                        && s.Sedmica == red.Order.Sedmica)
            .Select(s => (decimal?)s.CiljnaVrijednost)
            .FirstOrDefaultAsync(ct);

        var (dan, vrijemeOd, cutoff) = await SedmicaHelper.UcitajRokAsync(db, opcije.Value, ct);
        var zaglavlje = GetMojeKorpiceHandler.Sastavi(
            red,
            new Dictionary<Guid, Product> { [korpica.Id] = korpica },
            cilj is null
                ? new Dictionary<(Guid, int, int), decimal>()
                : new() { [(red.ProductId, red.Order.Godina, red.Order.Sedmica)] = cilj.Value },
            dan, vrijemeOd, cutoff, opcije.Value);

        return new MojaKorpicaDetaljiDto
        {
            Korpica = zaglavlje,
            Stavke = red.KorpicaStavke
                .Select(s => new StavkaMojeKorpiceDto
                {
                    ProductId = s.ProductId,
                    Naziv = s.ProductName,
                    Kolicina = s.Kolicina,
                    Jedinica = s.Unit.ToApi(),
                    JedinicnaCijena = s.JedinicnaCijena,
                    DodaoKupac = s.DodaoKupac,
                })
                .OrderBy(s => s.Naziv)
                .ToList(),
            Ponuda = ponuda,
            Izbjegnuti = await db.IzbjegnutiArtikli
                .Where(x => x.UserId == current.Id)
                .Select(x => x.ProductId)
                .ToListAsync(ct),
        };
    }
}

public record IzmijeniMojuKorpicuCommand(Guid OrderItemId, IReadOnlyList<Guid> ProductIds)
    : IRequest<MojaKorpicaDetaljiDto>;

public class IzmijeniMojuKorpicuValidator : AbstractValidator<IzmijeniMojuKorpicuCommand>
{
    public IzmijeniMojuKorpicuValidator()
    {
        RuleFor(x => x.ProductIds).NotEmpty().WithMessage("Korpica ne može ostati prazna.");
    }
}

public class IzmijeniMojuKorpicuHandler(
    IAppDbContext db, ICurrentUser current, IOptions<KorpiceOpcije> opcije, IMediator mediator)
    : IRequestHandler<IzmijeniMojuKorpicuCommand, MojaKorpicaDetaljiDto>
{
    /// <summary>Donja i gornja granica budžeta, kao udio CiljnaVrijednost.</summary>
    public const decimal DonjaGranica = 0.95m;
    public const decimal GornjaGranica = 1.00m;

    public async Task<MojaKorpicaDetaljiDto> Handle(
        IzmijeniMojuKorpicuCommand cmd, CancellationToken ct)
    {
        var red = await db.OrderItems
            .Include(i => i.Order)
            .Include(i => i.KorpicaStavke)
            .FirstOrDefaultAsync(i => i.Id == cmd.OrderItemId, ct)
            ?? throw new NotFoundException("Korpica nije pronađena.");

        if (!current.IsAuthenticated || red.Order!.CustomerId != current.Id)
            throw new ForbiddenException("Ova korpica nije tvoja.");

        // PRVO pitanje, prije svih pravila: je li ova stavka uopšte korpica. Bez toga se PUT
        // uperi u običnu stavku svoje narudžbe, cilj budžeta ispadne 0, obje granice se
        // isključe i proizvoljni redovi odu na pakersku listu — vidi UcitajKorpicuAsync.
        var korpica = await MojaKorpicaPravila.UcitajKorpicuAsync(db, red, ct);

        if (!MojaKorpicaPravila.StatusiKojiDozvoljavaju.Contains(red.Order.Status))
            throw new ConflictException(
                "Narudžba se već pakuje, sadržaj se više ne može mijenjati.", "korpica_se_pakuje");

        // Duplikat bi se tiho udvostručio u količini — odbijamo ga umjesto da ga sažmemo,
        // jer kupac koji je dvaput kliknuo isti artikal nije tražio dvostruku količinu.
        if (cmd.ProductIds.Distinct().Count() != cmd.ProductIds.Count)
            throw new DomainException("Isti artikal je naveden dvaput.", "duplirani_artikal");

        var (dan, vrijemeOd, cutoff) = await SedmicaHelper.UcitajRokAsync(db, opcije.Value, ct);
        var rok = SedmicaHelper
            .DatumIsporuke(red.Order.Godina, red.Order.Sedmica, dan, vrijemeOd)
            .AddHours(-cutoff);
        if (DateTime.UtcNow > rok)
            throw new DomainException(
                "Rok za izmjenu ove korpice je prošao.", "rok_istekao");

        var mjesec = SedmicaHelper
            .DatumIsporuke(red.Order.Godina, red.Order.Sedmica, DayOfWeek.Monday, "00:00")
            .Month;

        var podobni = KorpicaPodobnost
            .USezoni(await KorpicaPodobnost.UpitPodobnih(db.Products).ToListAsync(ct), mjesec)
            .ToDictionary(p => p.Id);

        // Količine iz šablona te sedmice: artikal koji je u šablonu zadržava svoju količinu,
        // sve ostalo dobija 1 u svojoj jedinici. Količinu NIKAD ne šalje klijent — vidi spec.
        var sadrzaj = await db.SedmicniSadrzaji
            .Include(s => s.Stavke)
            .FirstOrDefaultAsync(s => s.KorpicaId == red.ProductId
                                      && s.Godina == red.Order.Godina
                                      && s.Sedmica == red.Order.Sedmica, ct);

        // Sedmični sadržaj je JEDINI izvor cilja budžeta. Ako ga nema, upis se ODBIJA, a ne
        // propušta bez granica: straža koja se sama isključi kad joj nestane ulaz nije straža.
        // Prikaz (vidi Sastavi) u istom stanju pada na 100% da stara narudžba ne obori listu —
        // to je prikaz, ne dozvola.
        if (sadrzaj is null || sadrzaj.CiljnaVrijednost <= 0m)
            throw new DomainException(
                $"Za korpicu {korpica.Name} nema objavljenog sadržaja za tu sedmicu, pa se " +
                "ne može mijenjati.", "korpica_bez_sadrzaja");

        var sablonskeKolicine = sadrzaj.Stavke.ToDictionary(s => s.ProductId, s => s.Kolicina);
        var sablonskeCijene = sadrzaj.Stavke.ToDictionary(s => s.ProductId, s => s.JedinicnaCijena);

        // Redovi koji su već na ovoj narudžbi. Nose ZAMRZNUT snimak (naziv, jedinicu, cijenu)
        // iz trenutka narudžbe i on se izmjenom ne smije pomjeriti — vidi niže.
        var zadrzani = red.KorpicaStavke.ToDictionary(s => s.ProductId);

        // Nazivi SVIH poslanih id-ova, i onih koji nisu podobni (obrisan, van sezone, bez
        // zaliha) — poruka o nepodobnom artiklu mora da ga imenuje, inače kupac vidi ćorsokak
        // bez ijednog putokaza.
        var trazeniIdjevi = cmd.ProductIds.ToList();
        var nazivi = await db.Products.IgnoreQueryFilters()
            .Where(p => trazeniIdjevi.Contains(p.Id))
            .Select(p => new { p.Id, p.Name })
            .ToDictionaryAsync(p => p.Id, p => p.Name, ct);

        var nove = new List<StavkaKorpiceNarudzbe>();
        foreach (var pid in cmd.ProductIds)
        {
            // Zadržan artikal preskače provjeru podobnosti: spec vezuje podobnost za DODATI
            // artikal. Artikal iz šablona koji je u međuvremenu izašao iz sezone ili sa zaliha
            // inače čini cijelu kutiju zauvijek nesačuvljivom, a kupac ga nije ni dodao.
            if (zadrzani.TryGetValue(pid, out var stara))
            {
                nove.Add(new StavkaKorpiceNarudzbe
                {
                    OrderItemId = red.Id,
                    ProductId = stara.ProductId,
                    // Naziv, jedinica i cijena iz SNIMKA, ne iz kataloga: artikal je mogao
                    // biti obrisan, a cijena izmijenjena poslije objave — kupac ga samo
                    // zadržava, pa ni jedno ni drugo nema šta da ga pomjeri.
                    ProductName = stara.ProductName,
                    Unit = stara.Unit,
                    Kolicina = sablonskeKolicine.GetValueOrDefault(pid, stara.Kolicina),
                    JedinicnaCijena = stara.JedinicnaCijena,
                    DodaoKupac = stara.DodaoKupac,
                });
                continue;
            }

            if (!podobni.TryGetValue(pid, out var p))
                throw new DomainException(
                    nazivi.TryGetValue(pid, out var naziv)
                        ? $"Artikal {naziv} nije dostupan za korpicu ove sedmice."
                        : "Artikal nije dostupan za korpicu ove sedmice.",
                    "artikal_nije_podoban");

            nove.Add(new StavkaKorpiceNarudzbe
            {
                OrderItemId = red.Id,
                ProductId = p.Id,
                ProductName = p.Name,
                Unit = p.Unit,
                Kolicina = sablonskeKolicine.GetValueOrDefault(p.Id, 1m),
                // Cijena po prioritetu: red koji je već na narudžbi (gore), pa šablon te
                // sedmice, pa — samo za stvarno nov artikal, kojem nijedan snimak ne postoji —
                // trenutna katalog cijena. Inače bi se kutija mjerila današnjim cijenama
                // naspram cilja zamrznutog pri objavi, i jedno poskupljenje bi je trajno
                // izbacilo iznad plafona.
                JedinicnaCijena = sablonskeCijene.TryGetValue(p.Id, out var sablonska)
                    ? sablonska
                    : p.Price,
                DodaoKupac = !sablonskeKolicine.ContainsKey(p.Id),
            });
        }

        var cilj = sadrzaj.CiljnaVrijednost;
        var vrijednost = Math.Round(nove.Sum(s => s.Kolicina * s.JedinicnaCijena), 2);

        if (vrijednost < Math.Round(cilj * DonjaGranica, 2))
            throw new DomainException(
                $"Vrijednost korpice je {vrijednost:0.##} KM, a mora biti bar " +
                $"{cilj * DonjaGranica:0.##} KM.", "budzet_premali");

        if (vrijednost > Math.Round(cilj * GornjaGranica, 2))
            throw new DomainException(
                $"Vrijednost korpice je {vrijednost:0.##} KM, a najviše može biti " +
                $"{cilj * GornjaGranica:0.##} KM.", "budzet_preveliki");

        var minimum = opcije.Value.MinimalanBrojArtikala.GetValueOrDefault(
            korpica.Velicina ?? VelicinaKorpice.Mala, 5);
        if (nove.Count < minimum)
            throw new DomainException(
                $"Korpica mora imati bar {minimum} artikala.", "premalo_artikala");

        // Dodat artikal (ili veća količina zadržanog) mora imati slobodnog stanja — onog što
        // već nije obećano drugim korpicama i Špajzu. Zadržan artikal u istoj količini se ne
        // provjerava, iz istog razloga iz kojeg preskače podobnost gore.
        var zalihe = await Zalihe.UcitajAsync(db, red.Order.Godina, red.Order.Sedmica, ct, red.Id);
        var novoUzeti = nove
            .Where(s => !zadrzani.TryGetValue(s.ProductId, out var z) || s.Kolicina > z.Kolicina)
            .ToList();
        var novoIdjevi = novoUzeti.Select(s => s.ProductId).ToList();
        var artikli = await db.Products.IgnoreQueryFilters().Where(p => novoIdjevi.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        foreach (var s in novoUzeti)
        {
            var slobodno = artikli.TryGetValue(s.ProductId, out var p) ? zalihe.Slobodno(p) : 0m;
            if (s.Kolicina * red.Quantity > slobodno)
                throw new DomainException(
                    $"Artikla {s.ProductName} nema dovoljno za ovu korpicu. Izaberi drugi artikal.",
                    "nema_zalihe");
        }

        // Stari redovi se brišu eksplicitno kroz DbSet. `Clear()` na kolekciji praćenog
        // roditelja je u P1 dao DbUpdateConcurrencyException — EF je pokušao da ih i obriše i
        // ažurira. Vidi CLAUDE.md.
        db.StavkeKorpiceNarudzbe.RemoveRange(red.KorpicaStavke);
        foreach (var s in nove) db.StavkeKorpiceNarudzbe.Add(s);

        await db.SaveChangesAsync(ct);

        return await mediator.Send(new GetMojaKorpicaQuery(red.Id), ct);
    }
}

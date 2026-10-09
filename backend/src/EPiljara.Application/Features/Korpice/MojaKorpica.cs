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

        var korpice = await db.Products
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

        // Kad sadržaja nema (obrisan poslije narudžbe), cilj pada na zbir redova — tada je
        // odnos 100% i editor nikog ne blokira. Bolje nego dijeliti nulom ili tvrditi budžet
        // koji ne postoji.
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
            throw new ForbiddenException("Ova korpica nije vaša.");

        var korpica = await db.Products.FirstOrDefaultAsync(p => p.Id == red.ProductId, ct);

        // Ponuda je CIJELA sezonska ponuda — odluka P2b. Artikli iz kategorija ove korpice se
        // obilježavaju da ih birač može staviti prve, ali se ništa ne zabranjuje.
        // KorpicaKategorija se vezuje za TipKorpice, ne za Id konkretne korpice — vidi entitet.
        var kategorijeKorpice = korpica?.TipKorpice is null
            ? new List<Guid>()
            : await db.KorpicaKategorije
                .Where(k => k.TipKorpice == korpica.TipKorpice)
                .Select(k => k.CategoryId)
                .ToListAsync(ct);

        var mjesec = SedmicaHelper
            .DatumIsporuke(red.Order.Godina, red.Order.Sedmica, DayOfWeek.Monday, "00:00")
            .Month;

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
            })
            .OrderByDescending(p => p.IzOveKorpice)
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
            korpica is null ? new Dictionary<Guid, Product>() : new() { [korpica.Id] = korpica },
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
            throw new ForbiddenException("Ova korpica nije vaša.");

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
        var sablonskeKolicine = sadrzaj?.Stavke.ToDictionary(s => s.ProductId, s => s.Kolicina)
                                ?? [];

        var nove = new List<StavkaKorpiceNarudzbe>();
        foreach (var pid in cmd.ProductIds)
        {
            if (!podobni.TryGetValue(pid, out var p))
                throw new DomainException(
                    "Artikal nije dostupan za korpicu ove sedmice.", "artikal_nije_podoban");

            nove.Add(new StavkaKorpiceNarudzbe
            {
                OrderItemId = red.Id,
                ProductId = p.Id,
                ProductName = p.Name,
                Unit = p.Unit,
                Kolicina = sablonskeKolicine.GetValueOrDefault(p.Id, 1m),
                JedinicnaCijena = p.Price,
                DodaoKupac = !sablonskeKolicine.ContainsKey(p.Id),
            });
        }

        var cilj = sadrzaj?.CiljnaVrijednost ?? 0m;
        var vrijednost = Math.Round(nove.Sum(s => s.Kolicina * s.JedinicnaCijena), 2);

        if (cilj > 0m && vrijednost < Math.Round(cilj * DonjaGranica, 2))
            throw new DomainException(
                $"Vrijednost korpice je {vrijednost:0.##} KM, a mora biti bar " +
                $"{cilj * DonjaGranica:0.##} KM.", "budzet_premali");

        if (cilj > 0m && vrijednost > Math.Round(cilj * GornjaGranica, 2))
            throw new DomainException(
                $"Vrijednost korpice je {vrijednost:0.##} KM, a najviše može biti " +
                $"{cilj * GornjaGranica:0.##} KM.", "budzet_preveliki");

        var korpica = await db.Products.FirstOrDefaultAsync(p => p.Id == red.ProductId, ct);
        var minimum = opcije.Value.MinimalanBrojArtikala.GetValueOrDefault(
            korpica?.Velicina ?? VelicinaKorpice.Mala, 5);
        if (nove.Count < minimum)
            throw new DomainException(
                $"Korpica mora imati bar {minimum} artikala.", "premalo_artikala");

        // Stari redovi se brišu eksplicitno kroz DbSet. `Clear()` na kolekciji praćenog
        // roditelja je u P1 dao DbUpdateConcurrencyException — EF je pokušao da ih i obriše i
        // ažurira. Vidi CLAUDE.md.
        db.StavkeKorpiceNarudzbe.RemoveRange(red.KorpicaStavke);
        foreach (var s in nove) db.StavkeKorpiceNarudzbe.Add(s);

        await db.SaveChangesAsync(ct);

        return await mediator.Send(new GetMojaKorpicaQuery(red.Id), ct);
    }
}

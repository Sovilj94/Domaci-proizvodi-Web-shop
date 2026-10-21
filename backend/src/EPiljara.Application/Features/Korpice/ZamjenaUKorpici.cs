using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EPiljara.Application.Features.Korpice;

/// <summary>
/// Kupac neće jedan artikal iz korpice i uzima drugi umjesto njega. Za razliku od PUT-a
/// (<see cref="IzmijeniMojuKorpicuCommand"/>), gdje novi artikal uvijek dolazi u količini 1,
/// ovdje server sam određuje količinu novog artikla — tako da vrijednost kutije ostane
/// ista kao sa artiklom koji izlazi, unutar granica budžeta (95–100% cilja).
///
/// Novi artikal mora biti podoban za korpicu te sedmice (sezona, zaliha); predložene
/// zamjene iz sedmičnog sadržaja su samo preporuka, ne jedini izbor. Uz to zamjena ne smije
/// oboriti maržu: ako artikal ima nabavnu cijenu, a kutija bi sa njim koštala više od
/// nabavnog budžeta (i više nego prije zamjene), zamjena se odbija.
/// </summary>
public record ZamijeniArtikalUKorpiciCommand(Guid OrderItemId, Guid Izbaci, Guid Ubaci)
    : IRequest<MojaKorpicaDetaljiDto>;

public class ZamijeniArtikalUKorpiciHandler(
    IAppDbContext db, ICurrentUser current, IOptions<KorpiceOpcije> opcije, IMediator mediator)
    : IRequestHandler<ZamijeniArtikalUKorpiciCommand, MojaKorpicaDetaljiDto>
{
    public async Task<MojaKorpicaDetaljiDto> Handle(
        ZamijeniArtikalUKorpiciCommand cmd, CancellationToken ct)
    {
        var o = opcije.Value;
        var red = await db.OrderItems
            .Include(i => i.Order)
            .Include(i => i.KorpicaStavke)
            .FirstOrDefaultAsync(i => i.Id == cmd.OrderItemId, ct)
            ?? throw new NotFoundException("Korpica nije pronađena.");

        if (!current.IsAuthenticated || red.Order!.CustomerId != current.Id)
            throw new ForbiddenException("Ova korpica nije tvoja.");

        var korpica = await MojaKorpicaPravila.UcitajKorpicuAsync(db, red, ct);

        if (!MojaKorpicaPravila.StatusiKojiDozvoljavaju.Contains(red.Order.Status))
            throw new ConflictException(
                "Narudžba se već pakuje, sadržaj se više ne može mijenjati.", "korpica_se_pakuje");

        var (dan, vrijemeOd, cutoff) = await SedmicaHelper.UcitajRokAsync(db, o, ct);
        var rok = SedmicaHelper
            .DatumIsporuke(red.Order.Godina, red.Order.Sedmica, dan, vrijemeOd)
            .AddHours(-cutoff);
        if (DateTime.UtcNow > rok)
            throw new DomainException("Rok za izmjenu ove korpice je prošao.", "rok_istekao");

        if (cmd.Izbaci == cmd.Ubaci)
            throw new DomainException("Artikal se ne može zamijeniti samim sobom.", "isti_artikal");

        var stara = red.KorpicaStavke.FirstOrDefault(s => s.ProductId == cmd.Izbaci)
            ?? throw new NotFoundException("Taj artikal nije u ovoj korpici.");

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

        var mjesec = SedmicaHelper
            .DatumIsporuke(red.Order.Godina, red.Order.Sedmica, DayOfWeek.Monday, "00:00")
            .Month;
        var nov = KorpicaPodobnost.USezoni(
                await KorpicaPodobnost.UpitPodobnih(db.Products)
                    .Where(p => p.Id == cmd.Ubaci)
                    .ToListAsync(ct),
                mjesec)
            .FirstOrDefault();
        if (nov is null)
        {
            var naziv = await db.Products.IgnoreQueryFilters()
                .Where(p => p.Id == cmd.Ubaci).Select(p => p.Name).FirstOrDefaultAsync(ct);
            throw new DomainException(
                naziv is null
                    ? "Artikal nije dostupan za korpicu ove sedmice."
                    : $"Artikal {naziv} nije dostupan za korpicu ove sedmice.",
                "artikal_nije_podoban");
        }

        if (red.KorpicaStavke.Any(s => s.ProductId == nov.Id))
            throw new DomainException($"{nov.Name} je već u korpici.", "artikal_vec_u_korpici");

        // Cijena istim redom kao u PUT-u: snimak iz sadržaja (zamjena, pa šablon), pa katalog.
        var cijena = sadrzaj.Zamjene.FirstOrDefault(z => z.ProductId == nov.Id)?.JedinicnaCijena
                     ?? sadrzaj.Stavke.FirstOrDefault(s => s.ProductId == nov.Id)?.JedinicnaCijena
                     ?? nov.Price;
        if (cijena <= 0)
            throw new DomainException($"Artikal {nov.Name} nema cijenu.", "artikal_nije_podoban");

        var ostale = red.KorpicaStavke.Where(s => s.ProductId != stara.ProductId).ToList();
        var ostalo = ostale.Sum(s => s.Kolicina * s.JedinicnaCijena);
        var cilj = sadrzaj.CiljnaVrijednost;
        var donja = Math.Round(cilj * IzmijeniMojuKorpicuHandler.DonjaGranica, 2);
        var gornja = Math.Round(cilj * IzmijeniMojuKorpicuHandler.GornjaGranica, 2);

        var kolicina = Kolicina(stara.Kolicina * stara.JedinicnaCijena, ostalo, cijena, donja, gornja, nov.Unit)
            ?? throw new DomainException(
                $"{nov.Name} ne može da zamijeni {stara.ProductName} a da vrijednost korpice ostane " +
                "u granicama. Izaberi drugi artikal.", "zamjena_ne_staje");

        if (kolicina > nov.Stock)
            throw new DomainException(
                $"Nema dovoljno artikla {nov.Name} na stanju za ovu zamjenu.", "nema_zalihe");

        await ProvjeriMarzuAsync(red, ostale, stara, nov, kolicina, o, ct);

        db.StavkeKorpiceNarudzbe.Remove(stara);
        db.StavkeKorpiceNarudzbe.Add(new StavkaKorpiceNarudzbe
        {
            OrderItemId = red.Id,
            ProductId = nov.Id,
            ProductName = nov.Name,
            Unit = nov.Unit,
            Kolicina = kolicina,
            JedinicnaCijena = cijena,
            DodaoKupac = !sadrzaj.Stavke.Any(s => s.ProductId == nov.Id),
        });

        await db.SaveChangesAsync(ct);
        return await mediator.Send(new GetMojaKorpicaQuery(red.Id), ct);
    }

    /// <summary>
    /// Količina novog artikla: ona najbliža vrijednosti artikla koji izlazi, u koraku jedinice,
    /// tako da zbir kutije (zaokružen na 2 decimale, kao u PUT-u) ostane u [donja, gornja].
    /// Null kad takva količina ne postoji.
    /// </summary>
    public static decimal? Kolicina(
        decimal vrijednostStare, decimal ostalo, decimal cijena,
        decimal donja, decimal gornja, EPiljara.Domain.Enums.Unit jedinica)
    {
        var korak = GenerisiSedmicniSadrzajHandler.KoracZaokruzivanja(jedinica);
        bool UOpsegu(decimal k)
        {
            var ukupno = Math.Round(ostalo + k * cijena, 2);
            return ukupno >= donja && ukupno <= gornja;
        }

        var najmanja = Math.Max(korak, Math.Ceiling((donja - ostalo) / cijena / korak) * korak);
        var najveca = Math.Floor((gornja - ostalo) / cijena / korak) * korak;
        // Granice izračunate dijeljenjem mogu pasti korak pored zbog zaokruživanja zbira.
        if (!UOpsegu(najmanja) && UOpsegu(najmanja + korak)) najmanja += korak;
        if (!UOpsegu(najveca) && najveca - korak >= korak && UOpsegu(najveca - korak)) najveca -= korak;
        if (najmanja > najveca || !UOpsegu(najmanja) || !UOpsegu(najveca)) return null;

        var zeljena = Math.Round(vrijednostStare / cijena / korak, MidpointRounding.AwayFromZero) * korak;
        return Math.Clamp(zeljena, najmanja, najveca);
    }

    /// <summary>
    /// Nabavni budžet kutije je cijena koju je kupac platio × (1 − minimalna marža). Računaju
    /// se samo artikli sa unesenom nabavnom cijenom (trenutnom, iz kataloga). Zamjena koja
    /// kutiju gura preko budžeta se odbija — osim kad je i dalje jeftinija od stanja prije.
    /// Poruka kupcu ne pominje maržu.
    /// </summary>
    private async Task ProvjeriMarzuAsync(
        OrderItem red, List<StavkaKorpiceNarudzbe> ostale, StavkaKorpiceNarudzbe stara,
        Product nov, decimal kolicina, KorpiceOpcije o, CancellationToken ct)
    {
        if (nov.NabavnaCijena is not > 0) return;

        var idjevi = ostale.Select(s => s.ProductId).Append(stara.ProductId).ToList();
        var nabavne = await db.Products.IgnoreQueryFilters()
            .Where(p => idjevi.Contains(p.Id) && p.NabavnaCijena != null)
            .ToDictionaryAsync(p => p.Id, p => p.NabavnaCijena!.Value, ct);

        decimal Trosak(StavkaKorpiceNarudzbe s) => s.Kolicina * nabavne.GetValueOrDefault(s.ProductId);
        var ostaliTrosak = ostale.Sum(Trosak);
        var prije = ostaliTrosak + Trosak(stara);
        var poslije = ostaliTrosak + kolicina * nov.NabavnaCijena.Value;
        var budzet = red.UnitPrice * (1 - o.MinimalnaMarza);

        if (poslije > budzet && poslije > prije)
            throw new DomainException(
                $"{nov.Name} se ne može uzeti kao zamjena za {stara.ProductName}. Izaberi drugi artikal.",
                "zamjena_nije_dozvoljena");
    }
}

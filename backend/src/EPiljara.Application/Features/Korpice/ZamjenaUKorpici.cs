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
///
/// <see cref="Ubaci"/> = null znači „izaberi umjesto mene": server uzima prvi artikal koji staje,
/// redom iz <see cref="AutoZamjena.KandidatiAsync"/>, preskačući kupčev spisak „ne stavljaj mi".
/// <see cref="Zapamti"/> stavlja izbačeni artikal na taj spisak — naredne korpice ga ne dobiju.
/// </summary>
public record ZamijeniArtikalUKorpiciCommand(Guid OrderItemId, Guid Izbaci, Guid? Ubaci, bool Zapamti = false)
    : IRequest<MojaKorpicaDetaljiDto>;

public class ZamijeniArtikalUKorpiciHandler(
    IAppDbContext db, ICurrentUser current, IOptions<KorpiceOpcije> opcije, IMediator mediator)
    : IRequestHandler<ZamijeniArtikalUKorpiciCommand, MojaKorpicaDetaljiDto>
{
    public async Task<MojaKorpicaDetaljiDto> Handle(
        ZamijeniArtikalUKorpiciCommand cmd, CancellationToken ct)
    {
        var o = opcije.Value;
        var (red, korpica, sadrzaj) = await MojaKorpicaPravila.UcitajZaIzmjenuAsync(db, current, cmd.OrderItemId, o, ct);

        if (cmd.Izbaci == cmd.Ubaci)
            throw new DomainException("Artikal se ne može zamijeniti samim sobom.", "isti_artikal");

        var stara = red.KorpicaStavke.FirstOrDefault(s => s.ProductId == cmd.Izbaci)
            ?? throw new NotFoundException("Taj artikal nije u ovoj korpici.");
        var ostale = red.KorpicaStavke.Where(s => s.ProductId != stara.ProductId).ToList();

        var mjesec = SedmicaHelper
            .DatumIsporuke(red.Order!.Godina, red.Order.Sedmica, DayOfWeek.Monday, "00:00")
            .Month;
        var kandidati = await AutoZamjena.KandidatiAsync(db, sadrzaj, korpica, mjesec, ct);
        var nabavne = await AutoZamjena.NabavneAsync(db, red.KorpicaStavke, ct);
        // Slobodno po jednoj korpici: stanje minus ono što su druge korpice i Špajz već uzeli.
        var zalihe = await Zalihe.UcitajAsync(db, red.Order.Godina, red.Order.Sedmica, ct, red.Id);
        decimal Slobodno(Product p) => zalihe.Slobodno(p) / red.Quantity;

        AutoZamjena.Izbor izbor;
        if (cmd.Ubaci is null)
        {
            var izbjegnuti = (await db.IzbjegnutiArtikli
                .Where(x => x.UserId == current.Id)
                .Select(x => x.ProductId)
                .ToListAsync(ct)).ToHashSet();
            izbjegnuti.Add(stara.ProductId);
            izbor = AutoZamjena.Nadji(kandidati, stara, ostale, sadrzaj, red.UnitPrice, nabavne, izbjegnuti, o, Slobodno)
                ?? throw new DomainException(
                    $"Za {stara.ProductName} ove sedmice nema zamjene koja staje u korpicu.", "nema_zamjene");
        }
        else
        {
            var nov = kandidati.FirstOrDefault(p => p.Id == cmd.Ubaci);
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
            if (ostale.Any(s => s.ProductId == nov.Id))
                throw new DomainException($"{nov.Name} je već u korpici.", "artikal_vec_u_korpici");

            var (probano, prepreka) = AutoZamjena.Probaj(nov, stara, ostale, sadrzaj, red.UnitPrice, nabavne, o, Slobodno);
            izbor = probano ?? throw prepreka switch
            {
                AutoZamjena.Prepreka.NemaZalihe => new DomainException(
                    $"Nema dovoljno artikla {nov.Name} na stanju za ovu zamjenu.", "nema_zalihe"),
                AutoZamjena.Prepreka.Marza => new DomainException(
                    $"{nov.Name} se ne može uzeti kao zamjena za {stara.ProductName}. Izaberi drugi artikal.",
                    "zamjena_nije_dozvoljena"),
                _ => new DomainException(
                    $"{nov.Name} ne može da zamijeni {stara.ProductName} a da vrijednost korpice ostane " +
                    "u granicama. Izaberi drugi artikal.", "zamjena_ne_staje"),
            };
        }

        db.StavkeKorpiceNarudzbe.Remove(stara);
        db.StavkeKorpiceNarudzbe.Add(AutoZamjena.Red(red.Id, izbor, sadrzaj));

        if (cmd.Zapamti && !await db.IzbjegnutiArtikli.AnyAsync(
                x => x.UserId == current.Id && x.ProductId == stara.ProductId, ct))
            db.IzbjegnutiArtikli.Add(new IzbjegnutArtikal { UserId = current.Id!.Value, ProductId = stara.ProductId });

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
}

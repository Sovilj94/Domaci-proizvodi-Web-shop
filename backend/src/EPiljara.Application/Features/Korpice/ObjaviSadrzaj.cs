using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using EPiljara.Domain.Common;
using EPiljara.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EPiljara.Application.Features.Korpice;

/// <summary>
/// Nacrt → Objavljen. Ponovo snima jedinične cijene i zamrzava CiljnuVrijednost,
/// koja je od tog trenutka budžet kupčevog editora (P2).
/// </summary>
public record ObjaviSadrzajCommand(Guid SadrzajId) : IRequest<AdminSadrzajDto>;

public class ObjaviSadrzajHandler(IAppDbContext db, IOptions<KorpiceOpcije> opcije)
    : IRequestHandler<ObjaviSadrzajCommand, AdminSadrzajDto>
{
    public async Task<AdminSadrzajDto> Handle(ObjaviSadrzajCommand c, CancellationToken ct)
    {
        // IgnoreQueryFilters(): StavkaSadrzaja → Product je OBAVEZNA veza (ProductId nije
        // nullable), pa EF za ThenInclude generiše INNER JOIN. Product nosi globalni
        // !IsDeleted filter (vidi AppDbContext) — bez IgnoreQueryFilters() taj join tiho
        // IZBACI CIJELU stavku iz sadrzaj.Stavke za obrisan artikal (nije dovoljno da
        // stavka.Product bude null; stavka sama nestane iz liste), pa bi objava mogla
        // "nestati" tačno onoliko stavki koliko je artikala obrisano — do potpuno praznog
        // sadržaja koji bi ispao kao sadrzaj_prazan, ne kao jasna greška o uzroku. Sa
        // IgnoreQueryFilters() se svaka stavka vidi, obrisan Product je pravi objekat
        // (IsDeleted == true, IsAvailable == false), i provjera ispod ga hvata po imenu.
        var sadrzaj = await db.SedmicniSadrzaji
            .IgnoreQueryFilters()
            .Include(s => s.Korpica)
            .Include(s => s.Stavke).ThenInclude(st => st.Product).ThenInclude(p => p!.Producer)
            .Include(s => s.Zamjene).ThenInclude(z => z.Product)
            .FirstOrDefaultAsync(s => s.Id == c.SadrzajId, ct)
            ?? throw new NotFoundException("Sadržaj korpice nije pronađen.");

        if (sadrzaj.Stavke.Count == 0)
            throw new DomainException("Prazan sadržaj se ne može objaviti.", "sadrzaj_prazan");

        // Artikal obrisan (DeleteProductHandler postavlja i IsDeleted i IsAvailable=false)
        // ili ručno obilježen nedostupnim između generisanja nacrta i objave. Bez ove
        // provjere bi objava tiho prošla, a pakerska lista (procurement istina po spec-u)
        // bi za taj red ispisala stvarnu količinu sa praznim nazivom — admin bi naručio
        // nepoznatu količinu nečeg nepoznatog.
        if (sadrzaj.Stavke.Any(s => s.Product is null || s.Product.IsDeleted || !s.Product.IsAvailable))
            throw new DomainException(
                "Nacrt sadrži artikal koji je obrisan ili više nije dostupan. Uklonite ga pa objavite ponovo.",
                "sadrzaj_sadrzi_nedostupan_artikal");

        foreach (var stavka in sadrzaj.Stavke)
            stavka.JedinicnaCijena = stavka.Product?.Price ?? stavka.JedinicnaCijena;

        sadrzaj.CiljnaVrijednost = sadrzaj.Stavke.Sum(s => s.Kolicina * s.JedinicnaCijena);

        // Zamjene su ponuda, ne obaveza: nedostupna zamjena se tiho izbacuje (kupac je ne bi
        // mogao ni uzeti), a ostalima se cijena snima ponovo, kao stavkama.
        foreach (var z in sadrzaj.Zamjene.ToList())
        {
            if (z.Product is null || z.Product.IsDeleted || !z.Product.IsAvailable)
                db.ZamjeneSadrzaja.Remove(z);
            else
                z.JedinicnaCijena = z.Product.Price;
        }

        sadrzaj.Predji(StatusSadrzaja.Objavljen);

        await db.SaveChangesAsync(ct);
        return sadrzaj.ToAdminDto(opcije.Value);
    }
}

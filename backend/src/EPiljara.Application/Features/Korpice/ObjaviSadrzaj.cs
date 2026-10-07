using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using EPiljara.Domain.Common;
using EPiljara.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Korpice;

/// <summary>
/// Nacrt → Objavljen. Ponovo snima jedinične cijene i zamrzava CiljnuVrijednost,
/// koja je od tog trenutka budžet kupčevog editora (P2).
/// </summary>
public record ObjaviSadrzajCommand(Guid SadrzajId) : IRequest<SadrzajDto>;

public class ObjaviSadrzajHandler(IAppDbContext db) : IRequestHandler<ObjaviSadrzajCommand, SadrzajDto>
{
    public async Task<SadrzajDto> Handle(ObjaviSadrzajCommand c, CancellationToken ct)
    {
        var sadrzaj = await db.SedmicniSadrzaji
            .Include(s => s.Stavke).ThenInclude(st => st.Product)
            .FirstOrDefaultAsync(s => s.Id == c.SadrzajId, ct)
            ?? throw new NotFoundException("Sadržaj korpice nije pronađen.");

        if (sadrzaj.Stavke.Count == 0)
            throw new DomainException("Prazan sadržaj se ne može objaviti.", "sadrzaj_prazan");

        foreach (var stavka in sadrzaj.Stavke)
            stavka.JedinicnaCijena = stavka.Product?.Price ?? stavka.JedinicnaCijena;

        sadrzaj.CiljnaVrijednost = sadrzaj.Stavke.Sum(s => s.Kolicina * s.JedinicnaCijena);
        sadrzaj.Predji(StatusSadrzaja.Objavljen);

        await db.SaveChangesAsync(ct);
        return sadrzaj.ToDto();
    }
}

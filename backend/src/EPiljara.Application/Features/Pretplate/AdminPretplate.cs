using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using EPiljara.Application.Features.Korpice;
using EPiljara.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EPiljara.Application.Features.Pretplate;

/// <summary>
/// Spisak pretplata za nabavku. Odgovara na pitanje koje se do sada moglo postaviti tek kad
/// narudžbe nastanu: koliko je korpica obećano za narednu sedmicu, i koliko njih čeka sadržaj
/// koji još nije objavljen (rizik R2 iz speca — pretplatnik ostaje bez isporuke ako sadržaj
/// zakasni, a to se vidi samo ovdje).
/// </summary>
public record GetAdminPretplateQuery(string? Status) : IRequest<AdminPretplateDto>;

public class GetAdminPretplateHandler(
    IAppDbContext db, IOptions<KorpiceOpcije> korpiceOpcije, IOptions<PretplateOpcije> pretplateOpcije)
    : IRequestHandler<GetAdminPretplateQuery, AdminPretplateDto>
{
    public async Task<AdminPretplateDto> Handle(GetAdminPretplateQuery q, CancellationToken ct)
    {
        var upit = db.Pretplate
            .Include(p => p.Product)
            .Include(p => p.User)
            .Include(p => p.DeliveryZone)
            .Include(p => p.DeliverySlot)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q.Status)
            && Enum.TryParse<StatusPretplate>(q.Status, ignoreCase: true, out var status))
            upit = upit.Where(p => p.Status == status);

        var sve = await upit.ToListAsync(ct);

        var (dan, vrijemeOd, cutoff) = await SedmicaHelper.UcitajRokAsync(db, korpiceOpcije.Value, ct);
        var (godina, sedmica) = SedmicaHelper.SedmicaZaNarudzbu(dan, vrijemeOd, cutoff);

        // Koje korpice imaju spreman sadržaj za ciljnu sedmicu. Zaključan je i dalje spreman —
        // isti uslov koji primjenjuje posao koji pravi narudžbe.
        var spremne = await db.SedmicniSadrzaji
            .Where(s => s.Godina == godina && s.Sedmica == sedmica
                        && (s.Status == StatusSadrzaja.Objavljen || s.Status == StatusSadrzaja.Zakljucan))
            .Select(s => s.KorpicaId)
            .ToListAsync(ct);

        var aktivne = sve.Where(p => p.Status == StatusPretplate.Aktivna).ToList();

        return new AdminPretplateDto
        {
            Godina = godina,
            Sedmica = sedmica,
            BrojAktivnih = aktivne.Count,
            CekajuSadrzaj = aktivne.Count(p => !spremne.Contains(p.ProductId)),
            Stavke = sve
                .OrderBy(p => p.Status)
                .ThenBy(p => p.User?.Name)
                .Select(p => new AdminPretplataDto
                {
                    Id = p.Id,
                    Status = p.Status.ToString(),
                    Kupac = p.User?.Name ?? string.Empty,
                    Email = p.User?.Email,
                    Telefon = p.User?.Phone,
                    Korpica = p.Product?.Name ?? string.Empty,
                    Cijena = PretplatnaCijena.Izracunaj(p.Product?.Price ?? 0m, p.Plan, pretplateOpcije.Value),
                    BrojIsporuka = p.BrojIsporuka,
                    Plan = p.Plan.ToString(),
                    PreostaloObaveze = PretplatnaCijena.PreostaloObaveze(p.ObavezaIsporuka, p.BrojIsporuka),
                    Zona = p.DeliveryZone?.Name,
                    Adresa = p.DeliveryAddress,
                    PoslednjaGodina = p.PoslednjaGodina,
                    PoslednjaSedmica = p.PoslednjaSedmica,
                    SadrzajSpreman = spremne.Contains(p.ProductId),
                    ObavijestPreskocenaGodina = p.ObavijestPreskocenaGodina,
                    ObavijestPreskocenaSedmica = p.ObavijestPreskocenaSedmica,
                })
                .ToList(),
        };
    }
}

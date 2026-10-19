using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using EPiljara.Domain.Common;
using EPiljara.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Admin;

/// <summary>
/// Pakovanje sedmice (admin `/admin/pakovanje`): šta tačno ide u SVAKU kutiju. Pakerska lista
/// (`AdminKorpice`) je zbir za nabavku; ovdje je ista sedmica razložena po narudžbi — sadržaj
/// korpice onakav kakav ga je kupac ostavio (sa izmjenama) i dodaci iz Špajza — uz zbir Špajz
/// artikala, koji pakerska lista ne sabira jer nisu dio sadržaja korpice.
/// </summary>
public record GetPakovanjeQuery(int Godina, int Sedmica) : IRequest<PakovanjeDto>;

public class GetPakovanjeHandler(IAppDbContext db) : IRequestHandler<GetPakovanjeQuery, PakovanjeDto>
{
    public async Task<PakovanjeDto> Handle(GetPakovanjeQuery q, CancellationToken ct)
    {
        var narudzbe = await db.Orders.AsNoTracking()
            .Include(o => o.Items).ThenInclude(i => i.KorpicaStavke)
            .Include(o => o.DeliverySlot)
            .Include(o => o.DeliveryZone)
            .Where(o => o.Godina == q.Godina && o.Sedmica == q.Sedmica && o.Status != OrderStatus.Cancelled)
            .ToListAsync(ct);

        // Korpica se prepoznaje po tome da ima sadržaj ILI da je proizvod korpica — korpica čiji
        // sadržaj još nije upisan i dalje je kutija, ne dodatak iz Špajza.
        var productIds = narudzbe.SelectMany(o => o.Items).Select(i => i.ProductId).Distinct().ToList();
        var korpiceIds = (await db.Products.IgnoreQueryFilters()
                .Where(p => productIds.Contains(p.Id) && p.Vrsta == VrstaProizvoda.Korpica)
                .Select(p => p.Id).ToListAsync(ct))
            .ToHashSet();

        bool JeKorpica(Domain.Entities.OrderItem i) => korpiceIds.Contains(i.ProductId) || i.KorpicaStavke.Count > 0;

        var dto = narudzbe
            .OrderBy(o => o.DeliverySlot?.DayOfWeek ?? 7)
            .ThenBy(o => o.DeliveryZone?.Name)
            .ThenBy(o => o.Code)
            .Select(o => new PakovanjeNarudzbaDto
            {
                Id = o.Id,
                Code = o.Code,
                Status = o.Status.ToString(),
                Kupac = o.CustomerName,
                Telefon = o.CustomerPhone,
                Adresa = o.DeliveryAddress,
                Zona = o.DeliveryZone?.Name,
                DanIsporuke = o.DeliverySlot?.DayOfWeek,
                Termin = o.DeliverySlot is { } s ? $"{s.TimeFrom}–{s.TimeTo}" : null,
                Napomena = o.Note,
                Pretplatna = o.PretplataId != null,
                Ukupno = o.TotalAmount,
                Korpice = o.Items.Where(JeKorpica).Select(i => new PakovanjeKorpicaDto
                {
                    Naziv = i.ProductName,
                    Komada = i.Quantity,
                    Sadrzaj = i.KorpicaStavke
                        .OrderBy(s => s.DodaoKupac).ThenBy(s => s.ProductName)
                        .Select(s => new PakovanjeStavkaDto
                        {
                            Naziv = s.ProductName,
                            Kolicina = s.Kolicina,
                            Jedinica = s.Unit.ToApi(),
                            DodaoKupac = s.DodaoKupac,
                        }).ToList(),
                }).ToList(),
                Dodaci = o.Items.Where(i => !JeKorpica(i)).OrderBy(i => i.ProductName).Select(i => new PakovanjeStavkaDto
                {
                    Naziv = i.ProductName,
                    Kolicina = i.Quantity,
                    Jedinica = i.Unit.ToApi(),
                }).ToList(),
            })
            .ToList();

        var zbirDodataka = narudzbe.SelectMany(o => o.Items).Where(i => !JeKorpica(i))
            .GroupBy(i => new { i.ProductId, i.ProductName, i.Unit })
            .OrderBy(g => g.Key.ProductName)
            .Select(g => new PakovanjeStavkaDto
            {
                Naziv = g.Key.ProductName,
                Kolicina = g.Sum(i => i.Quantity),
                Jedinica = g.Key.Unit.ToApi(),
            })
            .ToList();

        return new PakovanjeDto
        {
            Godina = q.Godina,
            Sedmica = q.Sedmica,
            Narudzbe = dto,
            ZbirDodataka = zbirDodataka,
        };
    }
}

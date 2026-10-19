using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using EPiljara.Domain.Common;
using EPiljara.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Admin;

/// <summary>
/// Kupci (admin `/admin/kupci`): ko su, koliko naručuju, imaju li pretplatu i koliko poena.
/// Do sada se kupac vidio samo kroz pojedinačnu narudžbu ili ekran poena.
/// </summary>
public record GetAdminKupciQuery(string? Pretraga) : IRequest<IReadOnlyList<AdminKupacDto>>;

public class GetAdminKupciHandler(IAppDbContext db) : IRequestHandler<GetAdminKupciQuery, IReadOnlyList<AdminKupacDto>>
{
    private const int Najvise = 200;

    public async Task<IReadOnlyList<AdminKupacDto>> Handle(GetAdminKupciQuery q, CancellationToken ct)
    {
        var upit = db.Users.Where(u => u.Role == UserRole.Customer);
        var t = q.Pretraga?.Trim().ToLower();
        if (!string.IsNullOrEmpty(t))
            upit = upit.Where(u => u.Name.ToLower().Contains(t) || u.Email.ToLower().Contains(t)
                                   || (u.Phone != null && u.Phone.Contains(t)));

        return await upit
            .Select(u => new AdminKupacDto
            {
                Id = u.Id,
                Ime = u.Name,
                Email = u.Email,
                Telefon = u.Phone,
                EmailPotvrdjen = u.EmailConfirmed,
                Registrovan = u.CreatedAt,
                BrojNarudzbi = db.Orders.Count(o => o.CustomerId == u.Id && o.Status != OrderStatus.Cancelled),
                Isporuceno = db.Orders.Where(o => o.CustomerId == u.Id && o.Status == OrderStatus.Delivered)
                    .Sum(o => (decimal?)o.TotalAmount) ?? 0,
                ZadnjaNarudzba = db.Orders.Where(o => o.CustomerId == u.Id).Max(o => (DateTime?)o.CreatedAt),
                AktivnePretplate = db.Pretplate.Count(p => p.UserId == u.Id && p.Status != StatusPretplate.Otkazana),
                Poeni = db.PoeniStavke.Where(s => s.UserId == u.Id).Sum(s => (int?)s.Iznos) ?? 0,
            })
            .OrderByDescending(k => k.ZadnjaNarudzba ?? k.Registrovan)
            .Take(Najvise)
            .ToListAsync(ct);
    }
}

public record GetAdminKupacQuery(Guid Id) : IRequest<AdminKupacDetaljDto>;

public class GetAdminKupacHandler(IAppDbContext db) : IRequestHandler<GetAdminKupacQuery, AdminKupacDetaljDto>
{
    public async Task<AdminKupacDetaljDto> Handle(GetAdminKupacQuery q, CancellationToken ct)
    {
        var u = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == q.Id && x.Role == UserRole.Customer, ct)
            ?? throw new NotFoundException("Kupac nije pronađen.");

        var narudzbe = await db.Orders.AsNoTracking()
            .Where(o => o.CustomerId == u.Id)
            .OrderByDescending(o => o.CreatedAt)
            .Take(50)
            .Select(o => new AdminKupacNarudzbaDto
            {
                Id = o.Id, Code = o.Code, Status = o.Status.ToString(), Ukupno = o.TotalAmount,
                Kreirana = o.CreatedAt, Godina = o.Godina, Sedmica = o.Sedmica, Pretplatna = o.PretplataId != null,
            })
            .ToListAsync(ct);

        var pretplate = await db.Pretplate.AsNoTracking()
            .Where(p => p.UserId == u.Id)
            .Select(p => new AdminKupacPretplataDto
            {
                Id = p.Id, Korpica = p.Product != null ? p.Product.Name : "", Status = p.Status.ToString(),
                Plan = p.Plan.ToString(), BrojIsporuka = p.BrojIsporuka,
            })
            .ToListAsync(ct);

        var adrese = await db.Addresses.AsNoTracking()
            .Where(a => a.UserId == u.Id)
            .OrderByDescending(a => a.IsDefault)
            .Select(a => a.FullAddress)
            .ToListAsync(ct);

        return new AdminKupacDetaljDto
        {
            Id = u.Id,
            Ime = u.Name,
            Email = u.Email,
            Telefon = u.Phone,
            EmailPotvrdjen = u.EmailConfirmed,
            Registrovan = u.CreatedAt,
            Poeni = await db.PoeniStavke.Where(s => s.UserId == u.Id).SumAsync(s => s.Iznos, ct),
            Adrese = adrese,
            Narudzbe = narudzbe,
            Pretplate = pretplate,
        };
    }
}

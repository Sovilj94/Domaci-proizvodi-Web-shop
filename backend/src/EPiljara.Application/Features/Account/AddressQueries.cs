using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using EPiljara.Application.Mapping;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Account;

public record GetAccountAddressesQuery : IRequest<IReadOnlyList<AddressDto>>;

public class GetAccountAddressesHandler(IAppDbContext db, ICurrentUser current)
    : IRequestHandler<GetAccountAddressesQuery, IReadOnlyList<AddressDto>>
{
    public async Task<IReadOnlyList<AddressDto>> Handle(GetAccountAddressesQuery q, CancellationToken ct)
    {
        // Glavna prva, pa najskorije dodane — klijent uzima prvu kao predloženu.
        var addresses = await db.Addresses
            .Include(a => a.DeliveryZone)
            .Where(a => a.UserId == current.Id)
            .OrderByDescending(a => a.IsDefault)
            .ThenByDescending(a => a.CreatedAt)
            .ToListAsync(ct);

        return addresses.Select(a => a.ToDto()).ToList();
    }
}

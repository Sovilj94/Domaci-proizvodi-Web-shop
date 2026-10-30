using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EPiljara.Application.Features.Korpice;

/* ---------- „Ne stavljaj mi": kupčev trajni spisak ---------- */

public record GetIzbjegnutiArtikliQuery : IRequest<IReadOnlyList<IzbjegnutArtikalDto>>;

public class GetIzbjegnutiArtikliHandler(IAppDbContext db, ICurrentUser current)
    : IRequestHandler<GetIzbjegnutiArtikliQuery, IReadOnlyList<IzbjegnutArtikalDto>>
{
    public async Task<IReadOnlyList<IzbjegnutArtikalDto>> Handle(GetIzbjegnutiArtikliQuery q, CancellationToken ct)
    {
        if (current.Id is not { } kupac) throw new ForbiddenException("Prijavi se.");
        return await IzbjegnutiArtikliUpit.ZaKupcaAsync(db, kupac, ct);
    }
}

public record DodajIzbjegnutiArtikalCommand(Guid ProductId) : IRequest<IReadOnlyList<IzbjegnutArtikalDto>>;

public class DodajIzbjegnutiArtikalHandler(IAppDbContext db, ICurrentUser current)
    : IRequestHandler<DodajIzbjegnutiArtikalCommand, IReadOnlyList<IzbjegnutArtikalDto>>
{
    public async Task<IReadOnlyList<IzbjegnutArtikalDto>> Handle(DodajIzbjegnutiArtikalCommand cmd, CancellationToken ct)
    {
        if (current.Id is not { } kupac) throw new ForbiddenException("Prijavi se.");
        if (!await db.Products.AnyAsync(p => p.Id == cmd.ProductId, ct))
            throw new NotFoundException("Artikal nije pronađen.");
        if (!await db.IzbjegnutiArtikli.AnyAsync(x => x.UserId == kupac && x.ProductId == cmd.ProductId, ct))
        {
            db.IzbjegnutiArtikli.Add(new IzbjegnutArtikal { UserId = kupac, ProductId = cmd.ProductId });
            await db.SaveChangesAsync(ct);
        }
        return await IzbjegnutiArtikliUpit.ZaKupcaAsync(db, kupac, ct);
    }
}

public record UkloniIzbjegnutiArtikalCommand(Guid ProductId) : IRequest<IReadOnlyList<IzbjegnutArtikalDto>>;

public class UkloniIzbjegnutiArtikalHandler(IAppDbContext db, ICurrentUser current)
    : IRequestHandler<UkloniIzbjegnutiArtikalCommand, IReadOnlyList<IzbjegnutArtikalDto>>
{
    public async Task<IReadOnlyList<IzbjegnutArtikalDto>> Handle(UkloniIzbjegnutiArtikalCommand cmd, CancellationToken ct)
    {
        if (current.Id is not { } kupac) throw new ForbiddenException("Prijavi se.");
        var red = await db.IzbjegnutiArtikli.FirstOrDefaultAsync(x => x.UserId == kupac && x.ProductId == cmd.ProductId, ct);
        if (red is not null)
        {
            db.IzbjegnutiArtikli.Remove(red);
            await db.SaveChangesAsync(ct);
        }
        return await IzbjegnutiArtikliUpit.ZaKupcaAsync(db, kupac, ct);
    }
}

public static class IzbjegnutiArtikliUpit
{
    public static async Task<IReadOnlyList<IzbjegnutArtikalDto>> ZaKupcaAsync(IAppDbContext db, Guid kupac, CancellationToken ct) =>
        await db.IzbjegnutiArtikli
            .Where(x => x.UserId == kupac)
            .Join(db.Products.IgnoreQueryFilters(), x => x.ProductId, p => p.Id,
                (x, p) => new IzbjegnutArtikalDto { ProductId = p.Id, Naziv = p.Name, ImageUrl = p.ImageUrl })
            .OrderBy(x => x.Naziv)
            .ToListAsync(ct);
}

/* ---------- „Vrati kako je bilo" ---------- */

/// <summary>
/// Kupčeva korpica se vraća na sadržaj sedmice — kao da je tek naručena, uključujući
/// zamjene za artikle sa spiska „ne stavljaj mi". Iste provjere kao zamjena (rok, status).
/// </summary>
public record VratiMojuKorpicuCommand(Guid OrderItemId) : IRequest<MojaKorpicaDetaljiDto>;

public class VratiMojuKorpicuHandler(
    IAppDbContext db, ICurrentUser current, IOptions<KorpiceOpcije> opcije, IMediator mediator)
    : IRequestHandler<VratiMojuKorpicuCommand, MojaKorpicaDetaljiDto>
{
    public async Task<MojaKorpicaDetaljiDto> Handle(VratiMojuKorpicuCommand cmd, CancellationToken ct)
    {
        var (red, korpica, sadrzaj) = await MojaKorpicaPravila.UcitajZaIzmjenuAsync(db, current, cmd.OrderItemId, opcije.Value, ct);

        var redovi = await SnapshotKorpice.SablonAsync(db, red.Id, sadrzaj, ct);
        var mjesec = SedmicaHelper.DatumIsporuke(red.Order!.Godina, red.Order.Sedmica, DayOfWeek.Monday, "00:00").Month;
        var zalihe = await Zalihe.UcitajAsync(db, red.Order.Godina, red.Order.Sedmica, ct, red.Id);
        await AutoZamjena.PrimijeniIzbjegnuteAsync(
            db, redovi, red.Id, sadrzaj, korpica, red.UnitPrice, current.Id!.Value, mjesec, opcije.Value, ct,
            p => zalihe.Slobodno(p) / red.Quantity);

        // Brisanje kroz DbSet, ne Clear() na kolekciji — vidi IzmijeniMojuKorpicuHandler.
        db.StavkeKorpiceNarudzbe.RemoveRange(red.KorpicaStavke);
        db.StavkeKorpiceNarudzbe.AddRange(redovi);
        await db.SaveChangesAsync(ct);

        return await mediator.Send(new GetMojaKorpicaQuery(red.Id), ct);
    }
}

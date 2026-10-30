using EPiljara.Application.Common.Interfaces;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Admin;

/// <summary>Blok sadržaja kako ga vidi sajt i admin. Prazna polja su odsutna na žici.</summary>
public record BlokSadrzajaDto
{
    public Guid Id { get; init; }
    public string Sekcija { get; init; } = string.Empty;
    public int Redoslijed { get; init; }
    public string? Ikona { get; init; }
    public string? Naslov { get; init; }
    public string? Tekst { get; init; }
    public string? Dugme { get; init; }
    public string? Link { get; init; }
    public string? SlikaUrl { get; init; }
    public string? Boja { get; init; }
    public string? BojaTeksta { get; init; }
    public string? NaslovEn { get; init; }
    public string? TekstEn { get; init; }
    public string? DugmeEn { get; init; }
    public bool Objavljen { get; init; }
}

internal static class BlokMapa
{
    public static BlokSadrzajaDto UDto(BlokSadrzaja b) => new()
    {
        Id = b.Id, Sekcija = b.Sekcija, Redoslijed = b.Redoslijed, Ikona = b.Ikona, Naslov = b.Naslov,
        Tekst = b.Tekst, Dugme = b.Dugme, Link = b.Link, SlikaUrl = b.SlikaUrl, Boja = b.Boja,
        BojaTeksta = b.BojaTeksta, NaslovEn = b.NaslovEn, TekstEn = b.TekstEn, DugmeEn = b.DugmeEn,
        Objavljen = b.Objavljen,
    };
}

/* ---------- javno ---------- */

/// <summary>Svi objavljeni blokovi, po sekciji pa redoslijedu. Sajt ih grupiše sam.</summary>
public record GetBlokoviSadrzajaQuery : IRequest<IReadOnlyList<BlokSadrzajaDto>>;

public class GetBlokoviSadrzajaHandler(IAppDbContext db)
    : IRequestHandler<GetBlokoviSadrzajaQuery, IReadOnlyList<BlokSadrzajaDto>>
{
    public async Task<IReadOnlyList<BlokSadrzajaDto>> Handle(GetBlokoviSadrzajaQuery q, CancellationToken ct) =>
        (await db.BlokoviSadrzaja.AsNoTracking()
            .Where(b => b.Objavljen)
            .OrderBy(b => b.Sekcija).ThenBy(b => b.Redoslijed)
            .ToListAsync(ct))
        .Select(BlokMapa.UDto).ToList();
}

/* ---------- admin ---------- */

/// <summary>Svi blokovi jedne sekcije, i neobjavljeni.</summary>
public record GetAdminBlokoviQuery(string Sekcija) : IRequest<IReadOnlyList<BlokSadrzajaDto>>;

public class GetAdminBlokoviHandler(IAppDbContext db)
    : IRequestHandler<GetAdminBlokoviQuery, IReadOnlyList<BlokSadrzajaDto>>
{
    public async Task<IReadOnlyList<BlokSadrzajaDto>> Handle(GetAdminBlokoviQuery q, CancellationToken ct)
    {
        if (!SekcijeSadrzaja.Sve.Contains(q.Sekcija)) throw new NotFoundException("Nepoznata sekcija.");
        return (await db.BlokoviSadrzaja.AsNoTracking()
                .Where(b => b.Sekcija == q.Sekcija)
                .OrderBy(b => b.Redoslijed)
                .ToListAsync(ct))
            .Select(BlokMapa.UDto).ToList();
    }
}

public record BlokUnos(
    string? Ikona, string? Naslov, string? Tekst, string? Dugme, string? Link, string? SlikaUrl,
    string? Boja, string? BojaTeksta, string? NaslovEn, string? TekstEn, string? DugmeEn, bool Objavljen = true);

/// <summary>
/// Sekcija se čuva cijela odjednom: redoslijed je redoslijed liste. Admin tako dodaje, briše i
/// pomjera blokove jednim snimanjem, bez međustanja sa dva ista redna broja.
/// </summary>
public record SacuvajSekcijuCommand(string Sekcija, IReadOnlyList<BlokUnos> Blokovi)
    : IRequest<IReadOnlyList<BlokSadrzajaDto>>;

public class SacuvajSekcijuValidator : AbstractValidator<SacuvajSekcijuCommand>
{
    public SacuvajSekcijuValidator()
    {
        RuleFor(x => x.Sekcija).Must(s => SekcijeSadrzaja.Sve.Contains(s)).WithMessage("Nepoznata sekcija.");
        RuleFor(x => x.Blokovi.Count).LessThanOrEqualTo(30).OverridePropertyName("blokovi")
            .WithMessage("Najviše 30 blokova u sekciji.");
        RuleForEach(x => x.Blokovi).ChildRules(b =>
        {
            b.RuleFor(x => x).Must(x => !string.IsNullOrWhiteSpace(x.Naslov) || !string.IsNullOrWhiteSpace(x.SlikaUrl))
                .OverridePropertyName("naslov").WithMessage("Blok mora imati naslov ili sliku.");
            b.RuleFor(x => x.Ikona).MaximumLength(40).WithMessage("Ikona može imati najviše 40 znakova.");
            b.RuleFor(x => x.Naslov).MaximumLength(200).WithMessage("Naslov može imati najviše 200 znakova.");
            b.RuleFor(x => x.Tekst).MaximumLength(1000).WithMessage("Tekst može imati najviše 1000 znakova.");
            b.RuleFor(x => x.Dugme).MaximumLength(60).WithMessage("Dugme može imati najviše 60 znakova.");
            b.RuleFor(x => x.Link).MaximumLength(300).WithMessage("Link može imati najviše 300 znakova.");
            b.RuleFor(x => x.SlikaUrl).MaximumLength(300).WithMessage("Adresa slike može imati najviše 300 znakova.");
            b.RuleFor(x => x.Boja).Matches("^#[0-9A-Fa-f]{6}$").When(x => !string.IsNullOrWhiteSpace(x.Boja))
                .WithMessage("Boja mora biti u obliku #RRGGBB.");
            b.RuleFor(x => x.BojaTeksta).Matches("^#[0-9A-Fa-f]{6}$").When(x => !string.IsNullOrWhiteSpace(x.BojaTeksta))
                .WithMessage("Boja teksta mora biti u obliku #RRGGBB.");
            b.RuleFor(x => x.NaslovEn).MaximumLength(200).WithMessage("Naslov (EN) može imati najviše 200 znakova.");
            b.RuleFor(x => x.TekstEn).MaximumLength(1000).WithMessage("Tekst (EN) može imati najviše 1000 znakova.");
            b.RuleFor(x => x.DugmeEn).MaximumLength(60).WithMessage("Dugme (EN) može imati najviše 60 znakova.");
        });
    }
}

public class SacuvajSekcijuHandler(IAppDbContext db)
    : IRequestHandler<SacuvajSekcijuCommand, IReadOnlyList<BlokSadrzajaDto>>
{
    private static string? O(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    public async Task<IReadOnlyList<BlokSadrzajaDto>> Handle(SacuvajSekcijuCommand cmd, CancellationToken ct)
    {
        // Brisanje kroz DbSet pa novi redovi — vidi IzmijeniMojuKorpicuHandler za razlog.
        db.BlokoviSadrzaja.RemoveRange(await db.BlokoviSadrzaja.Where(b => b.Sekcija == cmd.Sekcija).ToListAsync(ct));
        var novi = cmd.Blokovi.Select((b, i) => new BlokSadrzaja
        {
            Sekcija = cmd.Sekcija, Redoslijed = i, Ikona = O(b.Ikona), Naslov = O(b.Naslov), Tekst = O(b.Tekst),
            Dugme = O(b.Dugme), Link = O(b.Link), SlikaUrl = O(b.SlikaUrl), Boja = O(b.Boja),
            BojaTeksta = O(b.BojaTeksta), NaslovEn = O(b.NaslovEn), TekstEn = O(b.TekstEn), DugmeEn = O(b.DugmeEn),
            Objavljen = b.Objavljen,
        }).ToList();
        db.BlokoviSadrzaja.AddRange(novi);
        await db.SaveChangesAsync(ct);
        return novi.Select(BlokMapa.UDto).ToList();
    }
}

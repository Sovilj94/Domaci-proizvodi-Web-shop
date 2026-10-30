using EPiljara.Application.Common;
using EPiljara.Application.Common.Interfaces;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Slike;

/// <summary>Pravila za slike u bazi — dozvoljeni tipovi, folderi i veličina.</summary>
public static class SlikePravila
{
    public const string Prefiks = "/slike/";
    public const int MaxBajtova = 5 * 1024 * 1024;

    public static readonly IReadOnlyDictionary<string, string> Tipovi = new Dictionary<string, string>
    {
        ["image/jpeg"] = "jpg",
        ["image/png"] = "png",
        ["image/webp"] = "webp",
    };

    /// <summary>Folderi ispod <c>/slike/</c>; prate ono što slika prikazuje.</summary>
    public static readonly IReadOnlySet<string> Folderi =
        new HashSet<string> { "products", "izrezi", "categories", "producers", "korpice", "recepti", "izgled" };

    public static string Url(string kljuc) => Prefiks + kljuc;

    /// <summary>Tip po ekstenziji, za uvoz fajlova; null za ono što nije slika.</summary>
    public static string? TipZaEkstenziju(string ekstenzija) =>
        ekstenzija.TrimStart('.').ToLowerInvariant() switch
        {
            "jpg" or "jpeg" => "image/jpeg",
            "png" => "image/png",
            "webp" => "image/webp",
            _ => null,
        };
}

/// <summary>Sadržaj slike za odgovor — bajtovi, tip i verzija za keš (ETag).</summary>
public record SlikaSadrzaj(byte[] Podaci, string ContentType, DateTime Kreirana, string ETag);

public record SlikaDto(string Url, string Kljuc, int Velicina);

/* ---------- Javno ---------- */

public record GetSlikaQuery(string Kljuc) : IRequest<SlikaSadrzaj>;

public class GetSlikaHandler(IAppDbContext db) : IRequestHandler<GetSlikaQuery, SlikaSadrzaj>
{
    public async Task<SlikaSadrzaj> Handle(GetSlikaQuery q, CancellationToken ct)
    {
        var kljuc = (q.Kljuc ?? "").Trim().TrimStart('/');
        var slika = await db.Slike.AsNoTracking().FirstOrDefaultAsync(s => s.Kljuc == kljuc, ct)
            ?? throw new NotFoundException("Slika", kljuc);
        return new SlikaSadrzaj(slika.Podaci, slika.ContentType, slika.Kreirana, $"\"{slika.Id:N}\"");
    }
}

/* ---------- Admin: upload ---------- */

/// <param name="Folder">Jedan od <see cref="SlikePravila.Folderi"/>.</param>
/// <param name="Naziv">Izvorno ime fajla; od njega ide čitljiv dio ključa.</param>
public record SacuvajSlikuCommand(string Folder, string Naziv, string ContentType, byte[] Podaci) : IRequest<SlikaDto>;

public class SacuvajSlikuValidator : AbstractValidator<SacuvajSlikuCommand>
{
    public SacuvajSlikuValidator()
    {
        RuleFor(x => x.Folder).Must(f => SlikePravila.Folderi.Contains(f ?? ""))
            .OverridePropertyName("folder").WithMessage("Nepoznat folder za sliku.");
        RuleFor(x => x.ContentType).Must(t => SlikePravila.Tipovi.ContainsKey(t ?? ""))
            .OverridePropertyName("fajl").WithMessage("Slika mora biti JPG, PNG ili WEBP.");
        RuleFor(x => x.Podaci).Must(p => p is { Length: > 0 })
            .OverridePropertyName("fajl").WithMessage("Fajl je prazan.");
        RuleFor(x => x.Podaci).Must(p => p is null || p.Length <= SlikePravila.MaxBajtova)
            .OverridePropertyName("fajl").WithMessage("Slika može imati najviše 5 MB.");
    }
}

public class SacuvajSlikuHandler(IAppDbContext db) : IRequestHandler<SacuvajSlikuCommand, SlikaDto>
{
    public async Task<SlikaDto> Handle(SacuvajSlikuCommand c, CancellationToken ct)
    {
        // Uvijek novi ključ: zamjena slike mijenja URL, pa ni pregledač ni CDN ne vrate staru.
        var ime = Slug.From(Path.GetFileNameWithoutExtension(c.Naziv ?? ""));
        if (ime.Length == 0) ime = "slika";
        if (ime.Length > 60) ime = ime[..60].TrimEnd('-');
        var kljuc = $"{c.Folder}/{ime}-{Guid.NewGuid().ToString("N")[..8]}.{SlikePravila.Tipovi[c.ContentType]}";

        db.Slike.Add(new Slika
        {
            Kljuc = kljuc,
            ContentType = c.ContentType,
            Podaci = c.Podaci,
            Velicina = c.Podaci.Length,
        });
        await db.SaveChangesAsync(ct);
        return new SlikaDto(SlikePravila.Url(kljuc), kljuc, c.Podaci.Length);
    }
}

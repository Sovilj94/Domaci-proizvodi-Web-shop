using EPiljara.Application.Common;
using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using EPiljara.Application.Mapping;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Recepti;

internal static class ReceptPravila
{
    /// <summary>Koraci se čuvaju kao jedan tekst; na žicu idu kao lista bez praznih redova.</summary>
    public static IReadOnlyList<string> Koraci(string tekst) =>
        tekst.Split('\n').Select(r => r.Trim()).Where(r => r.Length > 0).ToList();

    /// <summary>
    /// Javni recept nudi proizvod samo ako je običan i nije obrisan. Korpica u sastojku bi
    /// otvorila put do korpice mimo /korpice i roka; obrisan proizvod ne smije u korpu.
    /// (Obrisani ionako ne prolaze globalni filter — provjera je i ovdje radi jasnoće.)
    /// </summary>
    public static bool SmijeUPrikaz(Product? p) => p is { Vrsta: VrstaProizvoda.Obican, IsDeleted: false };

    public static TezinaRecepta ParsirajTezinu(string tezina) =>
        Enum.Parse<TezinaRecepta>(tezina.Trim(), ignoreCase: true);
}

internal static class ReceptMapiranje
{
    public static ReceptKratkoDto ToKratkoDto(this Recept r) => new()
    {
        Id = r.Id,
        Naslov = r.Naslov,
        Slug = r.Slug,
        Opis = r.Opis,
        SlikaUrl = r.SlikaUrl,
        VrijemeMinuta = r.VrijemeMinuta,
        Porcije = r.Porcije,
        Tezina = r.Tezina.ToString(),
    };

    public static ReceptDto ToDto(this Recept r) => new()
    {
        Id = r.Id,
        Naslov = r.Naslov,
        Slug = r.Slug,
        Opis = r.Opis,
        SlikaUrl = r.SlikaUrl,
        VrijemeMinuta = r.VrijemeMinuta,
        Porcije = r.Porcije,
        Tezina = r.Tezina.ToString(),
        Koraci = ReceptPravila.Koraci(r.Koraci),
        Objavljen = r.Objavljen,
        Sastojci = r.Sastojci.OrderBy(s => s.Redoslijed).Select(s => new SastojakDto
        {
            Naziv = s.Naziv,
            Kolicina = s.Kolicina,
            Proizvod = ReceptPravila.SmijeUPrikaz(s.Product) ? s.Product!.ToDto() : null,
        }).ToList(),
    };

    public static ReceptAdminDto ToAdminDto(this Recept r) => new()
    {
        Id = r.Id,
        Naslov = r.Naslov,
        Slug = r.Slug,
        Opis = r.Opis,
        SlikaUrl = r.SlikaUrl,
        VrijemeMinuta = r.VrijemeMinuta,
        Porcije = r.Porcije,
        Tezina = r.Tezina.ToString(),
        Koraci = ReceptPravila.Koraci(r.Koraci),
        Objavljen = r.Objavljen,
        Kreiran = r.Kreiran,
        Sastojci = r.Sastojci.OrderBy(s => s.Redoslijed).Select(s => new SastojakAdminDto
        {
            Naziv = s.Naziv,
            Kolicina = s.Kolicina,
            ProductId = s.ProductId,
        }).ToList(),
    };
}

/* ---------- Javno ---------- */

public record GetReceptiQuery : IRequest<IReadOnlyList<ReceptKratkoDto>>;

public class GetReceptiHandler(IAppDbContext db) : IRequestHandler<GetReceptiQuery, IReadOnlyList<ReceptKratkoDto>>
{
    public async Task<IReadOnlyList<ReceptKratkoDto>> Handle(GetReceptiQuery q, CancellationToken ct)
    {
        var recepti = await db.Recepti.Where(r => r.Objavljen).ToListAsync(ct);
        // Sortiranje u memoriji, kao i drugdje u projektu — isto se ponaša na SQLite i Postgresu.
        return recepti.OrderByDescending(r => r.Kreiran).Select(r => r.ToKratkoDto()).ToList();
    }
}

public record GetReceptBySlugQuery(string Slug) : IRequest<ReceptDto>;

public class GetReceptBySlugHandler(IAppDbContext db) : IRequestHandler<GetReceptBySlugQuery, ReceptDto>
{
    public async Task<ReceptDto> Handle(GetReceptBySlugQuery q, CancellationToken ct)
    {
        // Obrisan proizvod (globalni filter) u Include dolazi kao null — sastojak ostaje bez dugmeta.
        var r = await db.Recepti
            .Include(x => x.Sastojci).ThenInclude(s => s.Product).ThenInclude(p => p!.Category)
            .Include(x => x.Sastojci).ThenInclude(s => s.Product).ThenInclude(p => p!.Producer)
            .FirstOrDefaultAsync(x => x.Slug == q.Slug && x.Objavljen, ct)
            ?? throw new NotFoundException("Recept nije pronađen.");
        return r.ToDto();
    }
}

/* ---------- Admin ---------- */

public record GetAdminRecepteQuery : IRequest<IReadOnlyList<ReceptAdminDto>>;

public class GetAdminRecepteHandler(IAppDbContext db) : IRequestHandler<GetAdminRecepteQuery, IReadOnlyList<ReceptAdminDto>>
{
    public async Task<IReadOnlyList<ReceptAdminDto>> Handle(GetAdminRecepteQuery q, CancellationToken ct)
    {
        var recepti = await db.Recepti.Include(r => r.Sastojci).ToListAsync(ct);
        return recepti.OrderByDescending(r => r.Kreiran).Select(r => r.ToAdminDto()).ToList();
    }
}

public record SastojakInput(string Naziv, string? Kolicina, Guid? ProductId);

public record ReceptInput(
    string Naslov, string? Opis, string? SlikaUrl, int VrijemeMinuta, int Porcije,
    string Tezina, string Koraci, bool Objavljen, IReadOnlyList<SastojakInput>? Sastojci);

public class SastojakInputValidator : AbstractValidator<SastojakInput>
{
    public SastojakInputValidator()
    {
        RuleFor(x => (x.Naziv ?? "").Trim()).Length(1, 100)
            .WithName("Naziv").OverridePropertyName("naziv")
            .WithMessage("Naziv sastojka mora imati od 1 do 100 znakova.");
        RuleFor(x => (x.Kolicina ?? "").Trim()).MaximumLength(40)
            .WithName("Kolicina").OverridePropertyName("kolicina")
            .WithMessage("Količina može imati najviše 40 znakova.");
    }
}

public class ReceptInputValidator : AbstractValidator<ReceptInput>
{
    public ReceptInputValidator()
    {
        // Provjera ide nad trimovanom vrijednošću — handler upisuje trimovano.
        RuleFor(x => (x.Naslov ?? "").Trim()).Length(3, 120)
            .WithName("Naslov").OverridePropertyName("naslov")
            .WithMessage("Naslov mora imati od 3 do 120 znakova.");
        RuleFor(x => (x.Opis ?? "").Trim()).MaximumLength(500)
            .WithName("Opis").OverridePropertyName("opis")
            .WithMessage("Opis može imati najviše 500 znakova.");
        RuleFor(x => (x.SlikaUrl ?? "").Trim()).MaximumLength(500)
            .WithName("SlikaUrl").OverridePropertyName("slikaUrl")
            .WithMessage("Adresa slike može imati najviše 500 znakova.");
        RuleFor(x => x.VrijemeMinuta).InclusiveBetween(1, 600)
            .WithMessage("Vrijeme pripreme mora biti između 1 i 600 minuta.");
        RuleFor(x => x.Porcije).InclusiveBetween(1, 20)
            .WithMessage("Broj porcija mora biti između 1 i 20.");
        RuleFor(x => x.Tezina).Must(v => Enum.TryParse<TezinaRecepta>(v, true, out var t) && Enum.IsDefined(t))
            .WithMessage("Težina mora biti Lako, Srednje ili Zahtjevno.");
        RuleFor(x => (x.Koraci ?? "").Trim()).Length(10, 8000)
            .WithName("Koraci").OverridePropertyName("koraci")
            .WithMessage("Koraci moraju imati od 10 do 8000 znakova.");
        RuleFor(x => x.Sastojci).Must(s => s is null || s.Count <= 40)
            .WithMessage("Recept može imati najviše 40 sastojaka.");
        RuleForEach(x => x.Sastojci).NotNull().WithMessage("Sastojak nije ispravan.")
            .SetValidator(new SastojakInputValidator());
    }
}

internal static class ReceptUpis
{
    /// <summary>Svaki ProductId iz ulaza mora postojati — jednim upitom.</summary>
    public static async Task ProvjeriProizvodeAsync(IAppDbContext db, ReceptInput input, CancellationToken ct,
        IReadOnlyCollection<Guid>? vecVezani = null)
    {
        var ids = (input.Sastojci ?? []).Where(s => s.ProductId.HasValue)
            .Select(s => s.ProductId!.Value).Distinct().ToList();
        if (ids.Count == 0) return;
        // Već vezani proizvodi se prihvataju i kad su u međuvremenu obrisani (soft-delete),
        // inače se takav recept ne bi mogao ni sačuvati; novi ids moraju postojati.
        var provjeri = ids.Where(id => vecVezani is null || !vecVezani.Contains(id)).ToList();
        if (provjeri.Count == 0) return;
        var postoje = await db.Products.Where(p => provjeri.Contains(p.Id)).Select(p => p.Id).ToListAsync(ct);
        if (postoje.Count != provjeri.Count)
            throw new DomainException("Sastojak pokazuje na proizvod koji ne postoji.", "proizvod_ne_postoji");
    }

    public static void Upisi(Recept r, ReceptInput input)
    {
        r.Naslov = input.Naslov.Trim();
        r.Opis = (input.Opis ?? "").Trim();
        r.SlikaUrl = string.IsNullOrWhiteSpace(input.SlikaUrl) ? null : input.SlikaUrl.Trim();
        r.VrijemeMinuta = input.VrijemeMinuta;
        r.Porcije = input.Porcije;
        r.Tezina = ReceptPravila.ParsirajTezinu(input.Tezina);
        r.Koraci = input.Koraci.Trim();
        r.Objavljen = input.Objavljen;
    }

    public static List<ReceptSastojak> NoviSastojci(Guid receptId, ReceptInput input) =>
        (input.Sastojci ?? []).Select((s, i) => new ReceptSastojak
        {
            ReceptId = receptId,
            Naziv = s.Naziv.Trim(),
            Kolicina = (s.Kolicina ?? "").Trim(),
            ProductId = s.ProductId,
            Redoslijed = i,
        }).ToList();
}

public record CreateReceptCommand(ReceptInput Input) : IRequest<ReceptAdminDto>;

public class CreateReceptValidator : AbstractValidator<CreateReceptCommand>
{
    public CreateReceptValidator()
    {
        RuleFor(x => x.Input).NotNull().WithMessage("Neispravan zahtjev.");
        RuleFor(x => x.Input).SetValidator(new ReceptInputValidator()).When(x => x.Input is not null);
    }
}

public class CreateReceptHandler(IAppDbContext db) : IRequestHandler<CreateReceptCommand, ReceptAdminDto>
{
    public async Task<ReceptAdminDto> Handle(CreateReceptCommand cmd, CancellationToken ct)
    {
        await ReceptUpis.ProvjeriProizvodeAsync(db, cmd.Input, ct);

        var r = new Recept();
        ReceptUpis.Upisi(r, cmd.Input);
        r.Slug = await UniqueSlugAsync(r.Naslov, ct);
        r.Sastojci = ReceptUpis.NoviSastojci(r.Id, cmd.Input);
        db.Recepti.Add(r);
        await db.SaveChangesAsync(ct);
        return r.ToAdminDto();
    }

    private async Task<string> UniqueSlugAsync(string naslov, CancellationToken ct)
    {
        var baseSlug = Slug.From(naslov);
        if (baseSlug.Length == 0) baseSlug = "recept";
        var slug = baseSlug;
        var n = 2;
        while (await db.Recepti.AnyAsync(x => x.Slug == slug, ct))
            slug = $"{baseSlug}-{n++}";
        return slug;
    }
}

public record UpdateReceptCommand(Guid Id, ReceptInput Input) : IRequest<ReceptAdminDto>;

public class UpdateReceptValidator : AbstractValidator<UpdateReceptCommand>
{
    public UpdateReceptValidator()
    {
        RuleFor(x => x.Input).NotNull().WithMessage("Neispravan zahtjev.");
        RuleFor(x => x.Input).SetValidator(new ReceptInputValidator()).When(x => x.Input is not null);
    }
}

public class UpdateReceptHandler(IAppDbContext db) : IRequestHandler<UpdateReceptCommand, ReceptAdminDto>
{
    public async Task<ReceptAdminDto> Handle(UpdateReceptCommand cmd, CancellationToken ct)
    {
        var r = await db.Recepti.Include(x => x.Sastojci).FirstOrDefaultAsync(x => x.Id == cmd.Id, ct)
            ?? throw new NotFoundException("Recept nije pronađen.");
        var vezani = r.Sastojci.Where(s => s.ProductId.HasValue).Select(s => s.ProductId!.Value).ToList();
        await ReceptUpis.ProvjeriProizvodeAsync(db, cmd.Input, ct, vezani);

        // Naslov se mijenja, slug ne — javni linkovi ostaju živi (spec O15).
        ReceptUpis.Upisi(r, cmd.Input);
        db.ReceptSastojci.RemoveRange(r.Sastojci.ToList());
        var novi = ReceptUpis.NoviSastojci(r.Id, cmd.Input);
        db.ReceptSastojci.AddRange(novi);
        await db.SaveChangesAsync(ct);

        r.Sastojci = novi;
        return r.ToAdminDto();
    }
}

public record DeleteReceptCommand(Guid Id) : IRequest<Guid>;

public class DeleteReceptHandler(IAppDbContext db) : IRequestHandler<DeleteReceptCommand, Guid>
{
    public async Task<Guid> Handle(DeleteReceptCommand cmd, CancellationToken ct)
    {
        var r = await db.Recepti.Include(x => x.Sastojci).FirstOrDefaultAsync(x => x.Id == cmd.Id, ct)
            ?? throw new NotFoundException("Recept nije pronađen.");
        db.ReceptSastojci.RemoveRange(r.Sastojci.ToList());
        db.Recepti.Remove(r);
        await db.SaveChangesAsync(ct);
        return cmd.Id;
    }
}

using EPiljara.Application.Common;
using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Analize;

internal static class AnalizaMapiranje
{
    public static AnalizaDto ToDto(this Analiza a) => new()
    {
        Id = a.Id,
        ProducerId = a.ProducerId,
        Proizvodjac = a.Producer?.Name ?? string.Empty,
        Selo = a.Producer?.Village ?? string.Empty,
        ProductId = a.ProductId,
        Proizvod = a.Product?.Name,
        Slika = a.Product?.ImageUrl,
        Boja = BojaProizvoda.Za(a.Product?.Boja, a.Product?.Name ?? a.Uzorak),
        Uzorak = a.Uzorak,
        Laboratorija = a.Laboratorija,
        BrojIzvjestaja = a.BrojIzvjestaja,
        DatumUzorkovanja = a.DatumUzorkovanja,
        Ispitano = a.Ispitano,
        Rezultat = a.Rezultat.ToString(),
        Napomena = a.Napomena,
        PdfUrl = a.PdfUrl,
        Objavljena = a.Objavljena,
    };

    /// <summary>Jedini redoslijed analiza: najnovije uzorkovanje prvo, pa najnovije uneseno.</summary>
    public static List<Analiza> PoRedu(this IEnumerable<Analiza> q) =>
        q.OrderByDescending(a => a.DatumUzorkovanja).ThenByDescending(a => a.Kreiran).ToList();
}

/* ---------- Javno ---------- */

public record GetAnalizeQuery(Guid? Producer) : IRequest<IReadOnlyList<AnalizaDto>>;

public class GetAnalizeHandler(IAppDbContext db) : IRequestHandler<GetAnalizeQuery, IReadOnlyList<AnalizaDto>>
{
    public async Task<IReadOnlyList<AnalizaDto>> Handle(GetAnalizeQuery q, CancellationToken ct)
    {
        // Objavljene se vide sve, i one koje nisu uskladjene (spec O12).
        var upit = db.Analize.Include(a => a.Producer).Include(a => a.Product).Where(a => a.Objavljena);
        if (q.Producer.HasValue) upit = upit.Where(a => a.ProducerId == q.Producer.Value);
        // Sortiranje u memoriji, kao i drugdje u projektu — isto se ponaša na SQLite i Postgresu.
        var analize = await upit.ToListAsync(ct);
        return analize.PoRedu().Select(a => a.ToDto()).ToList();
    }
}

/* ---------- Admin ---------- */

public record GetAdminAnalizeQuery : IRequest<IReadOnlyList<AnalizaDto>>;

public class GetAdminAnalizeHandler(IAppDbContext db) : IRequestHandler<GetAdminAnalizeQuery, IReadOnlyList<AnalizaDto>>
{
    public async Task<IReadOnlyList<AnalizaDto>> Handle(GetAdminAnalizeQuery q, CancellationToken ct)
    {
        var analize = await db.Analize.Include(a => a.Producer).Include(a => a.Product).ToListAsync(ct);
        return analize.PoRedu().Select(a => a.ToDto()).ToList();
    }
}

public record AnalizaInput(
    Guid ProducerId, Guid? ProductId, string Uzorak, string Laboratorija, string? BrojIzvjestaja,
    DateOnly DatumUzorkovanja, string Ispitano, string Rezultat, string? Napomena, string? PdfUrl, bool Objavljena);

public class AnalizaInputValidator : AbstractValidator<AnalizaInput>
{
    public AnalizaInputValidator()
    {
        // Provjera ide nad trimovanom vrijednošću — handler upisuje trimovano.
        RuleFor(x => (x.Uzorak ?? "").Trim()).Length(2, 120)
            .WithName("Uzorak").OverridePropertyName("uzorak")
            .WithMessage("Uzorak mora imati od 2 do 120 znakova.");
        RuleFor(x => (x.Laboratorija ?? "").Trim()).Length(2, 120)
            .WithName("Laboratorija").OverridePropertyName("laboratorija")
            .WithMessage("Laboratorija mora imati od 2 do 120 znakova.");
        RuleFor(x => (x.BrojIzvjestaja ?? "").Trim()).MaximumLength(60)
            .WithName("BrojIzvjestaja").OverridePropertyName("brojIzvjestaja")
            .WithMessage("Broj izvještaja može imati najviše 60 znakova.");
        RuleFor(x => (x.Ispitano ?? "").Trim()).Length(2, 300)
            .WithName("Ispitano").OverridePropertyName("ispitano")
            .WithMessage("Ispitano mora imati od 2 do 300 znakova.");
        RuleFor(x => (x.Napomena ?? "").Trim()).MaximumLength(1000)
            .WithName("Napomena").OverridePropertyName("napomena")
            .WithMessage("Napomena može imati najviše 1000 znakova.");
        RuleFor(x => (x.PdfUrl ?? "").Trim()).MaximumLength(500)
            .WithName("PdfUrl").OverridePropertyName("pdfUrl")
            .WithMessage("Adresa PDF-a može imati najviše 500 znakova.");
        // Dan tolerancije zbog razlike vremenskih zona.
        RuleFor(x => x.DatumUzorkovanja).Must(d => d <= DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1))
            .WithName("DatumUzorkovanja").OverridePropertyName("datumUzorkovanja")
            .WithMessage("Datum uzorkovanja ne može biti u budućnosti.");
        // IsDefined je obavezan: TryParse sam prihvata i brojeve poput "99".
        RuleFor(x => x.Rezultat)
            .Must(v => Enum.TryParse<RezultatAnalize>(v, true, out var r) && Enum.IsDefined(r))
            .WithName("Rezultat").OverridePropertyName("rezultat")
            .WithMessage("Rezultat mora biti Uskladjeno ili NijeUskladjeno.");
    }
}

internal static class AnalizaUpis
{
    public static async Task ProvjeriVezeAsync(IAppDbContext db, AnalizaInput input, CancellationToken ct,
        Guid? vecVezaniProizvod = null)
    {
        if (!await db.Producers.AnyAsync(p => p.Id == input.ProducerId, ct))
            throw new DomainException("Proizvođač ne postoji.", "proizvodjac_ne_postoji");
        if (input.ProductId.HasValue && input.ProductId != vecVezaniProizvod && !await db.Products.AnyAsync(p => p.Id == input.ProductId.Value, ct))
            throw new DomainException("Proizvod ne postoji.", "proizvod_ne_postoji");
    }

    public static void Upisi(Analiza a, AnalizaInput input)
    {
        a.ProducerId = input.ProducerId;
        a.ProductId = input.ProductId;
        a.Uzorak = input.Uzorak.Trim();
        a.Laboratorija = input.Laboratorija.Trim();
        a.BrojIzvjestaja = (input.BrojIzvjestaja ?? "").Trim();
        a.DatumUzorkovanja = input.DatumUzorkovanja;
        a.Ispitano = input.Ispitano.Trim();
        a.Rezultat = Enum.Parse<RezultatAnalize>(input.Rezultat.Trim(), ignoreCase: true);
        a.Napomena = (input.Napomena ?? "").Trim();
        a.PdfUrl = string.IsNullOrWhiteSpace(input.PdfUrl) ? null : input.PdfUrl.Trim();
        a.Objavljena = input.Objavljena;
    }

    /// <summary>Nakon upisa učitava proizvođača i proizvod da DTO ima imena.</summary>
    public static async Task<AnalizaDto> UcitajDtoAsync(IAppDbContext db, Guid id, CancellationToken ct)
    {
        var a = await db.Analize.Include(x => x.Producer).Include(x => x.Product)
            .FirstAsync(x => x.Id == id, ct);
        return a.ToDto();
    }
}

public record CreateAnalizaCommand(AnalizaInput Input) : IRequest<AnalizaDto>;

public class CreateAnalizaValidator : AbstractValidator<CreateAnalizaCommand>
{
    public CreateAnalizaValidator()
    {
        RuleFor(x => x.Input).NotNull().WithMessage("Neispravan zahtjev.");
        RuleFor(x => x.Input).SetValidator(new AnalizaInputValidator()).When(x => x.Input is not null);
    }
}

public class CreateAnalizaHandler(IAppDbContext db) : IRequestHandler<CreateAnalizaCommand, AnalizaDto>
{
    public async Task<AnalizaDto> Handle(CreateAnalizaCommand cmd, CancellationToken ct)
    {
        await AnalizaUpis.ProvjeriVezeAsync(db, cmd.Input, ct);
        var a = new Analiza();
        AnalizaUpis.Upisi(a, cmd.Input);
        db.Analize.Add(a);
        await db.SaveChangesAsync(ct);
        return await AnalizaUpis.UcitajDtoAsync(db, a.Id, ct);
    }
}

public record UpdateAnalizaCommand(Guid Id, AnalizaInput Input) : IRequest<AnalizaDto>;

public class UpdateAnalizaValidator : AbstractValidator<UpdateAnalizaCommand>
{
    public UpdateAnalizaValidator()
    {
        RuleFor(x => x.Input).NotNull().WithMessage("Neispravan zahtjev.");
        RuleFor(x => x.Input).SetValidator(new AnalizaInputValidator()).When(x => x.Input is not null);
    }
}

public class UpdateAnalizaHandler(IAppDbContext db) : IRequestHandler<UpdateAnalizaCommand, AnalizaDto>
{
    public async Task<AnalizaDto> Handle(UpdateAnalizaCommand cmd, CancellationToken ct)
    {
        var a = await db.Analize.FirstOrDefaultAsync(x => x.Id == cmd.Id, ct)
            ?? throw new NotFoundException("Analiza nije pronađena.");
        // Već vezani proizvod prolazi i ako je u međuvremenu obrisan (soft-delete).
        await AnalizaUpis.ProvjeriVezeAsync(db, cmd.Input, ct, a.ProductId);
        AnalizaUpis.Upisi(a, cmd.Input);
        await db.SaveChangesAsync(ct);
        return await AnalizaUpis.UcitajDtoAsync(db, a.Id, ct);
    }
}

public record DeleteAnalizaCommand(Guid Id) : IRequest<Guid>;

public class DeleteAnalizaHandler(IAppDbContext db) : IRequestHandler<DeleteAnalizaCommand, Guid>
{
    public async Task<Guid> Handle(DeleteAnalizaCommand cmd, CancellationToken ct)
    {
        var a = await db.Analize.FirstOrDefaultAsync(x => x.Id == cmd.Id, ct)
            ?? throw new NotFoundException("Analiza nije pronađena.");
        db.Analize.Remove(a);
        await db.SaveChangesAsync(ct);
        return cmd.Id;
    }
}

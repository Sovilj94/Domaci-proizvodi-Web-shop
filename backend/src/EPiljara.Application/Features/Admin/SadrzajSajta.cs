using System.Text.Json;
using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Admin;

/// <summary>Kontakt i česta pitanja — javno čitanje (footer, /o-nama, početna) i admin izmjena.</summary>
public record GetSadrzajSajtaQuery : IRequest<SadrzajSajtaDto>;

public class GetSadrzajSajtaHandler(IAppDbContext db) : IRequestHandler<GetSadrzajSajtaQuery, SadrzajSajtaDto>
{
    public async Task<SadrzajSajtaDto> Handle(GetSadrzajSajtaQuery q, CancellationToken ct)
    {
        var red = await db.SadrzajSajta.AsNoTracking().FirstOrDefaultAsync(ct);
        return SadrzajSajtaMapa.UDto(red);
    }
}

internal static class SadrzajSajtaMapa
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static SadrzajSajtaDto UDto(Domain.Entities.SadrzajSajta? red) => new()
    {
        Telefon = red?.Telefon,
        Email = red?.Email,
        Adresa = red?.Adresa,
        Podrucje = red?.Podrucje,
        Pitanja = string.IsNullOrEmpty(red?.PitanjaJson)
            ? null
            : JsonSerializer.Deserialize<List<PitanjeDto>>(red.PitanjaJson, Json),
        Izmijenjeno = red?.Izmijenjeno,
    };

    public static string? UJson(IReadOnlyList<PitanjeDto>? pitanja) =>
        pitanja is null ? null : JsonSerializer.Serialize(pitanja, Json);
}

/// <summary>Prazno polje (ili `pitanja: null`) vraća sajt na ugrađeni tekst.</summary>
public record SacuvajSadrzajSajtaCommand(
    string? Telefon, string? Email, string? Adresa, string? Podrucje, IReadOnlyList<PitanjeDto>? Pitanja)
    : IRequest<SadrzajSajtaDto>;

public class SacuvajSadrzajSajtaValidator : AbstractValidator<SacuvajSadrzajSajtaCommand>
{
    public SacuvajSadrzajSajtaValidator()
    {
        RuleFor(x => x.Telefon).MaximumLength(40).WithMessage("Telefon može imati najviše 40 znakova.");
        RuleFor(x => x.Email).MaximumLength(120).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email))
            .WithMessage("Upiši ispravan e-mail.");
        RuleFor(x => x.Adresa).MaximumLength(200).WithMessage("Adresa može imati najviše 200 znakova.");
        RuleFor(x => x.Podrucje).MaximumLength(120).WithMessage("Područje može imati najviše 120 znakova.");
        RuleFor(x => x.Pitanja!.Count).LessThanOrEqualTo(30).When(x => x.Pitanja is not null)
            .OverridePropertyName("pitanja").WithMessage("Najviše 30 pitanja.");
        RuleForEach(x => x.Pitanja).ChildRules(p =>
        {
            p.RuleFor(x => x.Pitanje).NotEmpty().WithMessage("Pitanje ne smije biti prazno.")
                .MaximumLength(200).WithMessage("Pitanje može imati najviše 200 znakova.");
            p.RuleFor(x => x.Odgovor).NotEmpty().WithMessage("Odgovor ne smije biti prazan.")
                .MaximumLength(1500).WithMessage("Odgovor može imati najviše 1500 znakova.");
        }).When(x => x.Pitanja is not null);
    }
}

public class SacuvajSadrzajSajtaHandler(IAppDbContext db) : IRequestHandler<SacuvajSadrzajSajtaCommand, SadrzajSajtaDto>
{
    private static string? Ocisti(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    public async Task<SadrzajSajtaDto> Handle(SacuvajSadrzajSajtaCommand cmd, CancellationToken ct)
    {
        var red = await db.SadrzajSajta.FirstOrDefaultAsync(ct);
        if (red is null)
        {
            red = new Domain.Entities.SadrzajSajta();
            db.SadrzajSajta.Add(red);
        }

        red.Telefon = Ocisti(cmd.Telefon);
        red.Email = Ocisti(cmd.Email);
        red.Adresa = Ocisti(cmd.Adresa);
        red.Podrucje = Ocisti(cmd.Podrucje);
        red.PitanjaJson = SadrzajSajtaMapa.UJson(cmd.Pitanja?
            .Select(p => new PitanjeDto { Pitanje = p.Pitanje.Trim(), Odgovor = p.Odgovor.Trim() }).ToList());
        red.Izmijenjeno = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return SadrzajSajtaMapa.UDto(red);
    }
}

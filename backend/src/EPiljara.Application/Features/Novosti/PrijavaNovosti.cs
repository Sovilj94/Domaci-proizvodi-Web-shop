using EPiljara.Application.Common.Interfaces;
using EPiljara.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Novosti;

/// <summary>Footer „Prijavi se i budi u toku". Polje <c>Web</c> je honeypot: čovjek ga ne vidi, bot ga popuni.</summary>
public record PrijavaNovostiInput(string Email, string? Web);

public record PrijavaNovostiDto(bool Prijavljen = true);

public record PrijaviNaNovostiCommand(PrijavaNovostiInput Input) : IRequest<PrijavaNovostiDto>;

public class PrijaviNaNovostiValidator : AbstractValidator<PrijaviNaNovostiCommand>
{
    public PrijaviNaNovostiValidator()
    {
        RuleFor(x => x.Input).NotNull().WithMessage("Neispravan zahtjev.");
        When(x => x.Input is not null, () =>
        {
            // Namjerno nema pravila za Web: bot ne smije dobiti 400 zbog honeypota.
            RuleFor(x => (x.Input.Email ?? "").Trim())
                .NotEmpty().EmailAddress().MaximumLength(256)
                .OverridePropertyName("email")
                .WithMessage("Unesi ispravnu e-mail adresu.");
        });
    }
}

public class PrijaviNaNovostiHandler(IAppDbContext db) : IRequestHandler<PrijaviNaNovostiCommand, PrijavaNovostiDto>
{
    public async Task<PrijavaNovostiDto> Handle(PrijaviNaNovostiCommand cmd, CancellationToken ct)
    {
        // Honeypot i ponovljena prijava daju isti odgovor kao uspjeh: bot ne uči da je otkriven,
        // a forma ne otkriva da li je neka adresa već na listi.
        if (!string.IsNullOrWhiteSpace(cmd.Input.Web)) return new PrijavaNovostiDto();

        var email = cmd.Input.Email.Trim().ToLowerInvariant();
        if (await db.PrijaveNovosti.AnyAsync(p => p.Email == email, ct)) return new PrijavaNovostiDto();

        db.PrijaveNovosti.Add(new PrijavaNovosti { Email = email });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Dvije istovremene prijave iste adrese: jedinstveni indeks pusti jednu — ishod je isti.
        }
        return new PrijavaNovostiDto();
    }
}

public record PrijavaNovostiAdminDto(string Email, DateTime Kreirana);

public record GetAdminNovostiQuery : IRequest<IReadOnlyList<PrijavaNovostiAdminDto>>;

public class GetAdminNovostiHandler(IAppDbContext db)
    : IRequestHandler<GetAdminNovostiQuery, IReadOnlyList<PrijavaNovostiAdminDto>>
{
    public async Task<IReadOnlyList<PrijavaNovostiAdminDto>> Handle(GetAdminNovostiQuery q, CancellationToken ct) =>
        await db.PrijaveNovosti.OrderByDescending(p => p.Kreirana)
            .Select(p => new PrijavaNovostiAdminDto(p.Email, p.Kreirana)).ToListAsync(ct);
}

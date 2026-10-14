using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Utisci;

internal static class UtisakMapiranje
{
    public static UtisakDto ToDto(this Utisak u) => new()
    {
        Id = u.Id,
        Ime = u.Ime,
        Mjesto = u.Mjesto,
        Tekst = u.Tekst,
        Objavljen = u.Objavljen,
        Redoslijed = u.Redoslijed,
    };

    /// <summary>Jedini redoslijed utisaka — i početna i admin ih vide istim redom.</summary>
    public static IQueryable<Utisak> PoRedu(this IQueryable<Utisak> q) =>
        q.OrderBy(u => u.Redoslijed).ThenBy(u => u.Kreiran);
}

/* ---------- Javno ---------- */

public record GetUtisciQuery : IRequest<IReadOnlyList<UtisakDto>>;

public class GetUtisciHandler(IAppDbContext db) : IRequestHandler<GetUtisciQuery, IReadOnlyList<UtisakDto>>
{
    public async Task<IReadOnlyList<UtisakDto>> Handle(GetUtisciQuery q, CancellationToken ct)
    {
        var utisci = await db.Utisci.Where(u => u.Objavljen).PoRedu().ToListAsync(ct);
        return utisci.Select(u => u.ToDto()).ToList();
    }
}

/* ---------- Admin ---------- */

public record GetAdminUtisciQuery : IRequest<IReadOnlyList<UtisakDto>>;

public class GetAdminUtisciHandler(IAppDbContext db) : IRequestHandler<GetAdminUtisciQuery, IReadOnlyList<UtisakDto>>
{
    public async Task<IReadOnlyList<UtisakDto>> Handle(GetAdminUtisciQuery q, CancellationToken ct)
    {
        var utisci = await db.Utisci.PoRedu().ToListAsync(ct);
        return utisci.Select(u => u.ToDto()).ToList();
    }
}

public record UtisakInput(string Ime, string? Mjesto, string Tekst, bool Objavljen, int Redoslijed);

public class UtisakInputValidator : AbstractValidator<UtisakInput>
{
    public UtisakInputValidator()
    {
        // Provjera ide nad trimovanom vrijednošću — handler upisuje trimovano, pa bi
        // „  M  " inače prošlo validaciju i u bazu ušlo kao „M".
        RuleFor(x => (x.Ime ?? "").Trim()).Length(2, 60)
            .WithName("Ime").OverridePropertyName("ime")
            .WithMessage("Ime mora imati od 2 do 60 znakova.");
        RuleFor(x => (x.Mjesto ?? "").Trim()).MaximumLength(60)
            .WithName("Mjesto").OverridePropertyName("mjesto")
            .WithMessage("Mjesto može imati najviše 60 znakova.");
        RuleFor(x => (x.Tekst ?? "").Trim()).Length(10, 500)
            .WithName("Tekst").OverridePropertyName("tekst")
            .WithMessage("Utisak mora imati od 10 do 500 znakova.");
        RuleFor(x => x.Redoslijed).InclusiveBetween(0, 999)
            .WithMessage("Redoslijed mora biti između 0 i 999.");
    }
}

public record CreateUtisakCommand(UtisakInput Input) : IRequest<UtisakDto>;

public class CreateUtisakValidator : AbstractValidator<CreateUtisakCommand>
{
    public CreateUtisakValidator() => RuleFor(x => x.Input).SetValidator(new UtisakInputValidator());
}

public class CreateUtisakHandler(IAppDbContext db) : IRequestHandler<CreateUtisakCommand, UtisakDto>
{
    public async Task<UtisakDto> Handle(CreateUtisakCommand cmd, CancellationToken ct)
    {
        var u = new Utisak();
        Upisi(u, cmd.Input);
        db.Utisci.Add(u);
        await db.SaveChangesAsync(ct);
        return u.ToDto();
    }

    internal static void Upisi(Utisak u, UtisakInput input)
    {
        u.Ime = input.Ime.Trim();
        u.Mjesto = (input.Mjesto ?? "").Trim();
        u.Tekst = input.Tekst.Trim();
        u.Objavljen = input.Objavljen;
        u.Redoslijed = input.Redoslijed;
    }
}

public record UpdateUtisakCommand(Guid Id, UtisakInput Input) : IRequest<UtisakDto>;

public class UpdateUtisakValidator : AbstractValidator<UpdateUtisakCommand>
{
    public UpdateUtisakValidator() => RuleFor(x => x.Input).SetValidator(new UtisakInputValidator());
}

public class UpdateUtisakHandler(IAppDbContext db) : IRequestHandler<UpdateUtisakCommand, UtisakDto>
{
    public async Task<UtisakDto> Handle(UpdateUtisakCommand cmd, CancellationToken ct)
    {
        var u = await db.Utisci.FirstOrDefaultAsync(x => x.Id == cmd.Id, ct)
            ?? throw new NotFoundException("Utisak nije pronađen.");
        CreateUtisakHandler.Upisi(u, cmd.Input);
        await db.SaveChangesAsync(ct);
        return u.ToDto();
    }
}

public record DeleteUtisakCommand(Guid Id) : IRequest<Guid>;

public class DeleteUtisakHandler(IAppDbContext db) : IRequestHandler<DeleteUtisakCommand, Guid>
{
    public async Task<Guid> Handle(DeleteUtisakCommand cmd, CancellationToken ct)
    {
        var u = await db.Utisci.FirstOrDefaultAsync(x => x.Id == cmd.Id, ct)
            ?? throw new NotFoundException("Utisak nije pronađen.");
        db.Utisci.Remove(u);
        await db.SaveChangesAsync(ct);
        return cmd.Id;
    }
}

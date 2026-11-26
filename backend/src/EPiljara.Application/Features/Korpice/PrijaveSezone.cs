using EPiljara.Application.Common.Interfaces;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EPiljara.Application.Features.Korpice;

/// <summary>Polje <c>Web</c> je honeypot: čovjek ga ne vidi, bot ga popuni (kao kod prijave partnera).</summary>
public record PrijavaSezoneInput(string Email, string? Web);

/// <summary>
/// „Javi mi kad stigne voće" — javni upis bez prijave, iza politike <c>auth</c>. Dozvoljen samo za
/// voće dok je van sezone. Ponovljena prijava iste adrese je uspjeh bez novog reda, a odgovor nikad ne
/// otkriva da li je adresa već prijavljena.
/// </summary>
public record PrijaviNaSezonuCommand(string Tip, PrijavaSezoneInput Input, DateTime? Sada = null) : IRequest;

public class PrijaviNaSezonuValidator : AbstractValidator<PrijaviNaSezonuCommand>
{
    public PrijaviNaSezonuValidator()
    {
        RuleFor(x => x.Input).NotNull().WithMessage("Neispravan zahtjev.");
        // Namjerno nema pravila za Web: bot ne smije dobiti 400 zbog honeypota.
        RuleFor(x => x.Input.Email).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Unesi e-mail.")
            .MaximumLength(120).WithMessage("E-mail je predug.")
            .EmailAddress().WithMessage("E-mail nije ispravan.")
            .When(x => x.Input is not null);
    }
}

public class PrijaviNaSezonuHandler(IAppDbContext db, IOptions<KorpiceOpcije> opcije)
    : IRequestHandler<PrijaviNaSezonuCommand>
{
    public async Task Handle(PrijaviNaSezonuCommand c, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(c.Input.Web)) return;

        var tip = SedmicaHelper.IzSluga(c.Tip);
        if (tip != TipKorpice.Voce)
            throw new DomainException("Prijava je moguća samo za voće.", "prijava_nije_moguca");
        var mjesec = await SezonaVoca.MjesecNarudzbeAsync(db, opcije.Value, ct, c.Sada);
        if (!SezonaVoca.VanSezone(opcije.Value, tip, mjesec))
            throw new DomainException("Voće je već u ponudi.", "prijava_nije_moguca");

        var email = c.Input.Email.Trim().ToLowerInvariant();
        if (await PostojiAsync(email, tip.Value, ct)) return;

        db.PrijaveSezone.Add(new PrijavaSezone { Email = email, TipKorpice = tip.Value });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Trka dvije iste prijave: indeks je odbio drugu. Uspjeh samo ako red stvarno postoji.
            db.ChangeTracker.Clear();
            if (!await PostojiAsync(email, tip.Value, ct)) throw;
        }
    }

    private Task<bool> PostojiAsync(string email, TipKorpice tip, CancellationToken ct) =>
        db.PrijaveSezone.AnyAsync(p => p.Email == email && p.TipKorpice == tip && p.ObavijestenAt == null, ct);
}

/// <summary>
/// Šalje „Stiglo je voće" svim neobaviještenim prijavama te vrste i upisuje <c>ObavijestenAt</c> po
/// prijavi. Zove ga posao kad automatska objava javi prvu voćnu korpicu sezone. Neposlat mejl ostaje
/// neobaviješten i pokušava se pri sljedećem pozivu.
/// </summary>
public record ObavijestiPrijaveSezoneCommand(TipKorpice Tip) : IRequest<int>;

public class ObavijestiPrijaveSezoneHandler(
    IAppDbContext db, IEmailService email, ILogger<ObavijestiPrijaveSezoneHandler> log)
    : IRequestHandler<ObavijestiPrijaveSezoneCommand, int>
{
    public async Task<int> Handle(ObavijestiPrijaveSezoneCommand c, CancellationToken ct)
    {
        var prijave = await db.PrijaveSezone
            .Where(p => p.TipKorpice == c.Tip && p.ObavijestenAt == null)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync(ct);

        var poslato = 0;
        foreach (var p in prijave)
        {
            try
            {
                await email.SendSezonaKrenulaAsync(p.Email, ct);
                p.ObavijestenAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                poslato++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Obavještenje o početku sezone nije poslato prijavi {Id}.", p.Id);
            }
        }
        return poslato;
    }
}

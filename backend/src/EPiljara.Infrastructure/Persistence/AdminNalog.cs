using EPiljara.Application.Common.Interfaces;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Infrastructure.Persistence;

/// <summary>
/// Pravi admin nalog iz env varijabli (ADMIN_EMAIL, ADMIN_PASSWORD, ADMIN_NAME), nezavisno od
/// demo seeda — lozinka nikad nije u repou. Idempotentno: postojeći nalog sa tim emailom postaje
/// Admin, dobija potvrđen email i lozinku iz env-a, pa promjena ADMIN_PASSWORD i restart mijenjaju lozinku.
/// </summary>
public static class AdminNalog
{
    public const int MinDuzinaLozinke = 8;

    /// <returns>true ako je nalog napravljen ili ažuriran; false ako env nije postavljen.</returns>
    public static async Task<bool> ObezbijediAsync(AppDbContext db, IPasswordHasher hasher,
        string? email, string? lozinka, string? ime, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(lozinka)) return false;
        if (lozinka.Length < MinDuzinaLozinke)
            throw new InvalidOperationException($"ADMIN_PASSWORD mora imati bar {MinDuzinaLozinke} znakova.");

        var normalizovan = email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == normalizovan, ct);
        if (user is null)
        {
            user = new User { Email = normalizovan };
            db.Users.Add(user);
        }

        user.Name = string.IsNullOrWhiteSpace(ime) ? (user.Name is "" ? "Administrator" : user.Name) : ime.Trim();
        user.Role = UserRole.Admin;
        user.EmailConfirmed = true;
        user.EmailConfirmationToken = null;
        if (!hasher.Verify(lozinka, user.PasswordHash))
            user.PasswordHash = hasher.Hash(lozinka);

        await db.SaveChangesAsync(ct);
        return true;
    }
}

using EPiljara.Application.Common.Interfaces;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Unit = MediatR.Unit;

namespace EPiljara.Application.Features.Mejlovi;

public record EmailRedDto
{
    public Guid Id { get; init; }
    public string Primalac { get; init; } = string.Empty;
    public string? PrimalacIme { get; init; }
    public string Naslov { get; init; } = string.Empty;
    public string Vrsta { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public int Pokusaja { get; init; }
    public string? ZadnjaGreska { get; init; }
    public DateTime Kreirana { get; init; }
    public DateTime? Poslata { get; init; }
    public DateTime? SljedeciPokusaj { get; init; }
    public string? Transport { get; init; }
}

public record EmailDetaljDto : EmailRedDto
{
    public string Html { get; init; } = string.Empty;
    public string? DobavljacId { get; init; }
}

public record EmailPregledDto
{
    public IReadOnlyList<EmailRedDto> Poruke { get; init; } = [];
    public int NaCekanju { get; init; }
    public int Greske { get; init; }
    public int PoslatoDanas { get; init; }
    public int PoslatoMjesec { get; init; }
    public string Transport { get; init; } = string.Empty;
    public IReadOnlyList<string> Vrste { get; init; } = [];
}

/* ---------- Pregled reda ---------- */

public record GetEmailoviQuery(string? Status = null, string? Vrsta = null, string? Trazi = null) : IRequest<EmailPregledDto>;

public class GetEmailoviHandler(IAppDbContext db, IEmailTransport transport) : IRequestHandler<GetEmailoviQuery, EmailPregledDto>
{
    public async Task<EmailPregledDto> Handle(GetEmailoviQuery q, CancellationToken ct)
    {
        var upit = db.EmailPoruke.AsNoTracking();
        if (Enum.TryParse<StatusEmaila>(q.Status, true, out var status)) upit = upit.Where(m => m.Status == status);
        if (!string.IsNullOrWhiteSpace(q.Vrsta)) upit = upit.Where(m => m.Vrsta == q.Vrsta);
        if (!string.IsNullOrWhiteSpace(q.Trazi))
        {
            var t = q.Trazi.Trim().ToLower();
            upit = upit.Where(m => m.Primalac.ToLower().Contains(t) || m.Naslov.ToLower().Contains(t));
        }

        var danas = DateTime.UtcNow.Date;
        var mjesec = new DateTime(danas.Year, danas.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        return new EmailPregledDto
        {
            Poruke = await upit.OrderByDescending(m => m.Kreirana).Take(200).Select(m => new EmailRedDto
            {
                Id = m.Id, Primalac = m.Primalac, PrimalacIme = m.PrimalacIme, Naslov = m.Naslov, Vrsta = m.Vrsta,
                Status = m.Status.ToString(), Pokusaja = m.Pokusaja, ZadnjaGreska = m.ZadnjaGreska, Kreirana = m.Kreirana,
                Poslata = m.Poslata, SljedeciPokusaj = m.SljedeciPokusaj, Transport = m.Transport,
            }).ToListAsync(ct),
            NaCekanju = await db.EmailPoruke.CountAsync(m => m.Status == StatusEmaila.NaCekanju, ct),
            Greske = await db.EmailPoruke.CountAsync(m => m.Status == StatusEmaila.Greska, ct),
            PoslatoDanas = await db.EmailPoruke.CountAsync(m => m.Status == StatusEmaila.Poslato && m.Poslata >= danas, ct),
            PoslatoMjesec = await db.EmailPoruke.CountAsync(m => m.Status == StatusEmaila.Poslato && m.Poslata >= mjesec, ct),
            Transport = transport.Naziv,
            Vrste = await db.EmailPoruke.Select(m => m.Vrsta).Distinct().OrderBy(v => v).ToListAsync(ct),
        };
    }
}

public record GetEmailQuery(Guid Id) : IRequest<EmailDetaljDto>;

public class GetEmailHandler(IAppDbContext db) : IRequestHandler<GetEmailQuery, EmailDetaljDto>
{
    public async Task<EmailDetaljDto> Handle(GetEmailQuery q, CancellationToken ct) =>
        await db.EmailPoruke.AsNoTracking().Where(m => m.Id == q.Id).Select(m => new EmailDetaljDto
        {
            Id = m.Id, Primalac = m.Primalac, PrimalacIme = m.PrimalacIme, Naslov = m.Naslov, Vrsta = m.Vrsta,
            Status = m.Status.ToString(), Pokusaja = m.Pokusaja, ZadnjaGreska = m.ZadnjaGreska, Kreirana = m.Kreirana,
            Poslata = m.Poslata, SljedeciPokusaj = m.SljedeciPokusaj, Transport = m.Transport, Html = m.Html,
            DobavljacId = m.DobavljacId,
        }).FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Mejl nije pronađen.");
}

/// <summary>Mejl sa greškom (ili na čekanju) ide odmah ponovo, sa nultim brojačem pokušaja.</summary>
public record PonoviEmailCommand(Guid Id) : IRequest<EmailRedDto>;

public class PonoviEmailHandler(IAppDbContext db, IEmailSignal signal) : IRequestHandler<PonoviEmailCommand, EmailRedDto>
{
    public async Task<EmailRedDto> Handle(PonoviEmailCommand c, CancellationToken ct)
    {
        var m = await db.EmailPoruke.FirstOrDefaultAsync(x => x.Id == c.Id, ct)
                ?? throw new NotFoundException("Mejl nije pronađen.");
        if (m.Status == StatusEmaila.Poslato)
            throw new DomainException("Mejl je već poslat.", "vec_poslato");
        m.Status = StatusEmaila.NaCekanju;
        m.Pokusaja = 0;
        m.SljedeciPokusaj = null;
        await db.SaveChangesAsync(ct);
        signal.Probudi();
        return new EmailRedDto { Id = m.Id, Primalac = m.Primalac, Naslov = m.Naslov, Vrsta = m.Vrsta, Status = m.Status.ToString(), Kreirana = m.Kreirana };
    }
}

public record PosaljiProbniEmailCommand(string? Primalac) : IRequest<Unit>;

public class PosaljiProbniEmailHandler(IEmailService email, ICurrentUser current) : IRequestHandler<PosaljiProbniEmailCommand, Unit>
{
    public async Task<Unit> Handle(PosaljiProbniEmailCommand c, CancellationToken ct)
    {
        var na = string.IsNullOrWhiteSpace(c.Primalac) ? current.Email : c.Primalac.Trim();
        if (string.IsNullOrWhiteSpace(na) || !na.Contains('@'))
            throw new DomainException("Unesi adresu na koju da pošaljem probni mejl.", "nema_adrese");
        await email.SendProbniAsync(na, ct);
        return Unit.Value;
    }
}

/* ---------- Podešavanja obavještenja ---------- */

public record ObavjestenjaPodesavanjaDto
{
    public bool KupacPotvrdjena { get; init; }
    public bool KupacIsporucena { get; init; }
    public bool KupacNeuspjela { get; init; }
    public bool KupacOtkazana { get; init; }
    public bool RadnikNovaNarudzba { get; init; }
    public bool RadnikOdbijene { get; init; }
    public bool RadnikFiskalnaGreska { get; init; }
    public bool RadnikGorivo { get; init; }
    public bool VozacPlan { get; init; }
    public int VozacPlanSat { get; init; }
    public string RadniciPrimaoci { get; init; } = string.Empty;
    /// <summary>Kome stvarno idu obavještenja radnicima (lista ili, kad je prazna, admini).</summary>
    public IReadOnlyList<string> RadniciEfektivno { get; init; } = [];
}

public record GetObavjestenjaPodesavanjaQuery : IRequest<ObavjestenjaPodesavanjaDto>;

public class GetObavjestenjaPodesavanjaHandler(IAppDbContext db) : IRequestHandler<GetObavjestenjaPodesavanjaQuery, ObavjestenjaPodesavanjaDto>
{
    public async Task<ObavjestenjaPodesavanjaDto> Handle(GetObavjestenjaPodesavanjaQuery q, CancellationToken ct)
    {
        var p = await db.Podesavanja.AsNoTracking().FirstOrDefaultAsync(x => x.Id == Podesavanja.JediniId, ct)
                ?? throw new InvalidOperationException("U bazi nema reda Podesavanja.");
        return new ObavjestenjaPodesavanjaDto
        {
            KupacPotvrdjena = p.ObavKupacPotvrdjena, KupacIsporucena = p.ObavKupacIsporucena,
            KupacNeuspjela = p.ObavKupacNeuspjela, KupacOtkazana = p.ObavKupacOtkazana,
            RadnikNovaNarudzba = p.ObavRadnikNovaNarudzba, RadnikOdbijene = p.ObavRadnikOdbijene,
            RadnikFiskalnaGreska = p.ObavRadnikFiskalnaGreska, RadnikGorivo = p.ObavRadnikGorivo,
            VozacPlan = p.ObavVozacPlan, VozacPlanSat = p.ObavVozacPlanSat, RadniciPrimaoci = p.ObavRadniciPrimaoci,
            RadniciEfektivno = await ObavjestenjaPravila.RadniciAsync(db, p, ct),
        };
    }
}

public record SacuvajObavjestenjaPodesavanjaCommand(
    bool KupacPotvrdjena, bool KupacIsporucena, bool KupacNeuspjela, bool KupacOtkazana,
    bool RadnikNovaNarudzba, bool RadnikOdbijene, bool RadnikFiskalnaGreska, bool RadnikGorivo,
    bool VozacPlan, int VozacPlanSat, string? RadniciPrimaoci) : IRequest<ObavjestenjaPodesavanjaDto>;

public class SacuvajObavjestenjaPodesavanjaValidator : AbstractValidator<SacuvajObavjestenjaPodesavanjaCommand>
{
    public SacuvajObavjestenjaPodesavanjaValidator()
    {
        RuleFor(x => x.VozacPlanSat).InclusiveBetween(6, 23).WithMessage("Sat slanja plana mora biti od 6 do 23.");
        RuleFor(x => x.RadniciPrimaoci).MaximumLength(1000).WithMessage("Lista adresa je preduga (najviše 1000 znakova).");
        RuleForEach(x => ObavjestenjaPravila.Razdvoji(x.RadniciPrimaoci)).EmailAddress()
            .OverridePropertyName("RadniciPrimaoci").WithMessage("„{PropertyValue}\" nije ispravna e-mail adresa.");
    }
}

public class SacuvajObavjestenjaPodesavanjaHandler(IAppDbContext db)
    : IRequestHandler<SacuvajObavjestenjaPodesavanjaCommand, ObavjestenjaPodesavanjaDto>
{
    public async Task<ObavjestenjaPodesavanjaDto> Handle(SacuvajObavjestenjaPodesavanjaCommand c, CancellationToken ct)
    {
        var p = await db.Podesavanja.FirstOrDefaultAsync(x => x.Id == Podesavanja.JediniId, ct)
                ?? throw new InvalidOperationException("U bazi nema reda Podesavanja.");
        p.ObavKupacPotvrdjena = c.KupacPotvrdjena;
        p.ObavKupacIsporucena = c.KupacIsporucena;
        p.ObavKupacNeuspjela = c.KupacNeuspjela;
        p.ObavKupacOtkazana = c.KupacOtkazana;
        p.ObavRadnikNovaNarudzba = c.RadnikNovaNarudzba;
        p.ObavRadnikOdbijene = c.RadnikOdbijene;
        p.ObavRadnikFiskalnaGreska = c.RadnikFiskalnaGreska;
        p.ObavRadnikGorivo = c.RadnikGorivo;
        p.ObavVozacPlan = c.VozacPlan;
        p.ObavVozacPlanSat = c.VozacPlanSat;
        p.ObavRadniciPrimaoci = string.Join(", ", ObavjestenjaPravila.Razdvoji(c.RadniciPrimaoci));
        await db.SaveChangesAsync(ct);
        return await new GetObavjestenjaPodesavanjaHandler(db).Handle(new GetObavjestenjaPodesavanjaQuery(), ct);
    }
}

/* ---------- Ručno slanje grupi (kampanja) ---------- */

public static class KampanjaGrupe
{
    public static readonly IReadOnlyDictionary<string, string> Nazivi = new Dictionary<string, string>
    {
        ["sedmica"] = "Kupci sa narudžbom za sedmicu",
        ["pretplatnici"] = "Aktivni pretplatnici",
        ["kupci"] = "Svi kupci sa nalogom",
        ["novosti"] = "Prijavljeni na novosti",
        ["vozaci"] = "Vozači",
        ["radnici"] = "Radnici (primaoci obavještenja)",
    };

    /// <summary>Primaoci grupe, bez duplih adresa. „kupci" i „novosti" su informativni: bez odjavljenih, sa linkom za odjavu.</summary>
    public static async Task<(List<PrimalacKampanje> Primaoci, string Opis)> UcitajAsync(
        IAppDbContext db, string grupa, int? godina, int? sedmica, CancellationToken ct)
    {
        List<PrimalacKampanje> lista;
        var opis = Nazivi.TryGetValue(grupa, out var n) ? n : throw new DomainException("Nepoznata grupa primalaca.", "grupa_nepoznata");
        switch (grupa)
        {
            case "sedmica":
                if (godina is null || sedmica is null) throw new DomainException("Izaberi sedmicu.", "nema_sedmice");
                opis = $"{n} {sedmica}/{godina}";
                lista = (await db.Orders.AsNoTracking()
                        .Where(o => o.Godina == godina && o.Sedmica == sedmica && o.Status != OrderStatus.Cancelled
                                    && o.CustomerEmail != null && o.CustomerEmail != "")
                        .Select(o => new { o.CustomerEmail, o.CustomerName, o.Pismo }).ToListAsync(ct))
                    .Select(o => new PrimalacKampanje(o.CustomerEmail!, o.CustomerName, false, o.Pismo)).ToList();
                break;
            case "pretplatnici":
                lista = (await db.Pretplate.AsNoTracking().Where(p => p.Status == StatusPretplate.Aktivna && p.User != null)
                        .Select(p => new { p.User!.Email, p.User.Name, p.User.Pismo }).ToListAsync(ct))
                    .Select(u => new PrimalacKampanje(u.Email, u.Name, false, u.Pismo)).ToList();
                break;
            case "kupci":
                lista = (await db.Users.AsNoTracking()
                        .Where(u => u.Role == UserRole.Customer && u.EmailConfirmed && !u.OdjavljenOdObavjestenja && u.Email != "")
                        .Select(u => new { u.Email, u.Name, u.Pismo }).ToListAsync(ct))
                    .Select(u => new PrimalacKampanje(u.Email, u.Name, true, u.Pismo)).ToList();
                break;
            case "novosti":
                var odjavljeni = db.Users.Where(u => u.OdjavljenOdObavjestenja).Select(u => u.Email);
                lista = (await db.PrijaveNovosti.AsNoTracking().Where(p => !odjavljeni.Contains(p.Email))
                        .Select(p => p.Email).ToListAsync(ct))
                    .Select(e => new PrimalacKampanje(e, null, true)).ToList();
                break;
            case "vozaci":
                lista = (await (from d in db.Drivers.AsNoTracking()
                                join u in db.Users.AsNoTracking() on d.UserId equals u.Id
                                where d.Aktivan && u.Email != ""
                                select new { u.Email, d.Name }).ToListAsync(ct))
                    .Select(u => new PrimalacKampanje(u.Email, u.Name, false)).ToList();
                break;
            default: // radnici
                lista = (await ObavjestenjaPravila.RadniciAsync(db, await ObavjestenjaPravila.PodesavanjaAsync(db, ct), ct))
                    .Select(e => new PrimalacKampanje(e, null, false)).ToList();
                break;
        }
        return (lista.GroupBy(x => x.Email.Trim().ToLowerInvariant()).Select(g => g.First() with { Email = g.Key }).ToList(), opis);
    }
}

public record KampanjaPregledDto(string Opis, int Broj, IReadOnlyList<string> Primjeri, bool SaOdjavom);

public record PregledKampanjeQuery(string Grupa, int? Godina, int? Sedmica) : IRequest<KampanjaPregledDto>;

public class PregledKampanjeHandler(IAppDbContext db) : IRequestHandler<PregledKampanjeQuery, KampanjaPregledDto>
{
    public async Task<KampanjaPregledDto> Handle(PregledKampanjeQuery q, CancellationToken ct)
    {
        var (lista, opis) = await KampanjaGrupe.UcitajAsync(db, q.Grupa, q.Godina, q.Sedmica, ct);
        return new KampanjaPregledDto(opis, lista.Count,
            lista.Take(8).Select(p => p.Ime is null ? p.Email : $"{p.Ime} <{p.Email}>").ToList(), lista.Any(p => p.Odjava));
    }
}

public record KampanjaDto(Guid Id, string Naslov, string GrupaOpis, int BrojPrimalaca, DateTime Kreirana, int Poslato, int Greske);

public record PosaljiKampanjuCommand(string Grupa, int? Godina, int? Sedmica, string Naslov, string Tekst) : IRequest<KampanjaDto>;

public class PosaljiKampanjuValidator : AbstractValidator<PosaljiKampanjuCommand>
{
    public PosaljiKampanjuValidator()
    {
        RuleFor(x => x.Naslov).NotEmpty().MaximumLength(200).WithMessage("Naslov je obavezan (do 200 znakova).");
        RuleFor(x => x.Tekst).NotEmpty().MaximumLength(10000).WithMessage("Tekst je obavezan (do 10.000 znakova).");
    }
}

public class PosaljiKampanjuHandler(IAppDbContext db, IEmailService email, ICurrentUser current)
    : IRequestHandler<PosaljiKampanjuCommand, KampanjaDto>
{
    public async Task<KampanjaDto> Handle(PosaljiKampanjuCommand c, CancellationToken ct)
    {
        var (lista, opis) = await KampanjaGrupe.UcitajAsync(db, c.Grupa, c.Godina, c.Sedmica, ct);
        if (lista.Count == 0) throw new DomainException("U ovoj grupi nema nijednog primaoca.", "nema_primalaca");
        var k = new EmailKampanja
        {
            Naslov = c.Naslov.Trim(), Tekst = c.Tekst.Trim(), Grupa = c.Grupa, GrupaOpis = opis,
            BrojPrimalaca = lista.Count, PoslaoId = current.Id,
        };
        db.EmailKampanje.Add(k);
        await db.SaveChangesAsync(ct);
        await email.SendKampanjaAsync(k, lista, ct);
        return new KampanjaDto(k.Id, k.Naslov, k.GrupaOpis, k.BrojPrimalaca, k.Kreirana, 0, 0);
    }
}

public record GetKampanjeQuery : IRequest<IReadOnlyList<KampanjaDto>>;

public class GetKampanjeHandler(IAppDbContext db) : IRequestHandler<GetKampanjeQuery, IReadOnlyList<KampanjaDto>>
{
    public async Task<IReadOnlyList<KampanjaDto>> Handle(GetKampanjeQuery q, CancellationToken ct) =>
        await db.EmailKampanje.AsNoTracking().OrderByDescending(k => k.Kreirana).Take(30)
            .Select(k => new KampanjaDto(k.Id, k.Naslov, k.GrupaOpis, k.BrojPrimalaca, k.Kreirana,
                db.EmailPoruke.Count(m => m.KampanjaId == k.Id && m.Status == StatusEmaila.Poslato),
                db.EmailPoruke.Count(m => m.KampanjaId == k.Id && m.Status == StatusEmaila.Greska)))
            .ToListAsync(ct);
}

/* ---------- Odjava (link iz mejla, bez prijave) ---------- */

public record OdjavaSaObavjestenjaCommand(string Email, string Potpis) : IRequest<Unit>;

public class OdjavaSaObavjestenjaHandler(IAppDbContext db, IOdjavaPotpis potpis) : IRequestHandler<OdjavaSaObavjestenjaCommand, Unit>
{
    public async Task<Unit> Handle(OdjavaSaObavjestenjaCommand c, CancellationToken ct)
    {
        var email = (c.Email ?? string.Empty).Trim().ToLowerInvariant();
        if (email.Length == 0 || !potpis.Provjeri(email, c.Potpis ?? string.Empty))
            throw new DomainException("Link za odjavu nije ispravan.", "odjava_neispravna");
        foreach (var u in await db.Users.Where(u => u.Email == email).ToListAsync(ct))
            u.OdjavljenOdObavjestenja = true;
        db.PrijaveNovosti.RemoveRange(await db.PrijaveNovosti.Where(p => p.Email == email).ToListAsync(ct));
        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

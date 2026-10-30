using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.Features.Catalog;
using EPiljara.Application.Features.Korpice;
using EPiljara.Application.Features.Poeni;
using EPiljara.Application.Features.Pretplate;
using EPiljara.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Admin;

/// <summary>Poslovna pravila kako ih admin vidi i mijenja (`/admin/settings`).</summary>
public record PodesavanjaDto
{
    public decimal KoeficijentVrijednosti { get; init; }
    public decimal MinimalnaMarza { get; init; }
    public int BrojZamjena { get; init; }
    public int MinArtikalaMala { get; init; }
    public int MinArtikalaSrednja { get; init; }
    public int MinArtikalaVelika { get; init; }
    public int RokSatiRezerva { get; init; }
    /// <summary>0 = nedjelja … 6 = subota.</summary>
    public int DanIsporukeRezerva { get; init; }
    public string VrijemeIsporukeRezerva { get; init; } = string.Empty;
    public decimal PopustNedjeljna { get; init; }
    public decimal PopustMjesecna { get; init; }
    public decimal PopustTriMjeseca { get; init; }
    public int ObavezaMjesecna { get; init; }
    public int ObavezaTriMjeseca { get; init; }
    public int SatiUnaprijed { get; init; }
    public int AlarmSatiPrijeRoka { get; init; }
    public int MaksPauzaSedmica { get; init; }
    public int PoenaPoKM { get; init; }
    public decimal VrijednostPoenaKM { get; init; }
    public int NagradaPreporuka { get; init; }
    public decimal PragPriKraju { get; init; }
}

/// <summary>Red <c>Podesavanja</c> → opcije koje handleri čitaju kroz <c>IOptions</c>.</summary>
public static class PodesavanjaMapa
{
    public static KorpiceOpcije Korpice(Domain.Entities.Podesavanja p) => new()
    {
        KoeficijentVrijednosti = p.KoeficijentVrijednosti,
        MinimalnaMarza = p.MinimalnaMarza,
        BrojZamjena = p.BrojZamjena,
        RokSati = p.RokSatiRezerva,
        DanIsporukeRezerva = p.DanIsporukeRezerva,
        VrijemeIsporukeRezerva = p.VrijemeIsporukeRezerva,
        MinimalanBrojArtikala = new()
        {
            [VelicinaKorpice.Mala] = p.MinArtikalaMala,
            [VelicinaKorpice.Srednja] = p.MinArtikalaSrednja,
            [VelicinaKorpice.Velika] = p.MinArtikalaVelika,
        },
    };

    public static PretplateOpcije Pretplate(Domain.Entities.Podesavanja p) => new()
    {
        PopustNedjeljna = p.PopustNedjeljna,
        PopustMjesecna = p.PopustMjesecna,
        PopustTriMjeseca = p.PopustTriMjeseca,
        ObavezaMjesecna = p.ObavezaMjesecna,
        ObavezaTriMjeseca = p.ObavezaTriMjeseca,
        SatiUnaprijed = p.SatiUnaprijed,
        AlarmSatiPrijeRoka = p.AlarmSatiPrijeRoka,
        MaksPauzaSedmica = p.MaksPauzaSedmica,
    };

    public static PoeniOpcije Poeni(Domain.Entities.Podesavanja p) => new()
    {
        PoenaPoKM = p.PoenaPoKM,
        VrijednostPoenaKM = p.VrijednostPoenaKM,
        NagradaPreporuka = p.NagradaPreporuka,
    };

    public static ProdavnicaOpcije Prodavnica(Domain.Entities.Podesavanja p) => new() { PragPriKraju = p.PragPriKraju };

    public static PodesavanjaDto UDto(Domain.Entities.Podesavanja p) => new()
    {
        KoeficijentVrijednosti = p.KoeficijentVrijednosti, MinimalnaMarza = p.MinimalnaMarza, BrojZamjena = p.BrojZamjena,
        MinArtikalaMala = p.MinArtikalaMala, MinArtikalaSrednja = p.MinArtikalaSrednja, MinArtikalaVelika = p.MinArtikalaVelika,
        RokSatiRezerva = p.RokSatiRezerva, DanIsporukeRezerva = (int)p.DanIsporukeRezerva,
        VrijemeIsporukeRezerva = p.VrijemeIsporukeRezerva,
        PopustNedjeljna = p.PopustNedjeljna, PopustMjesecna = p.PopustMjesecna, PopustTriMjeseca = p.PopustTriMjeseca,
        ObavezaMjesecna = p.ObavezaMjesecna, ObavezaTriMjeseca = p.ObavezaTriMjeseca, SatiUnaprijed = p.SatiUnaprijed,
        AlarmSatiPrijeRoka = p.AlarmSatiPrijeRoka, MaksPauzaSedmica = p.MaksPauzaSedmica,
        PoenaPoKM = p.PoenaPoKM, VrijednostPoenaKM = p.VrijednostPoenaKM, NagradaPreporuka = p.NagradaPreporuka,
        PragPriKraju = p.PragPriKraju,
    };
}

/// <summary>
/// Jedan red <c>Podesavanja</c> po zahtjevu (scope), učitan pri prvom čitanju. Bez reda baca —
/// pravilo bez vrijednosti iz baze ne smije tiho postati nula.
/// </summary>
public sealed class IzvorPodesavanja(IAppDbContext db)
{
    private Domain.Entities.Podesavanja? _red;

    public Domain.Entities.Podesavanja Red => _red ??= db.Podesavanja.AsNoTracking()
            .FirstOrDefault(p => p.Id == Domain.Entities.Podesavanja.JediniId)
        ?? throw new InvalidOperationException(
            "U bazi nema reda Podesavanja — poslovna pravila se ne mogu pročitati. Vidi DEPLOY.md → Podaci.");
}

/* ---------- admin ---------- */

public record GetPodesavanjaQuery : IRequest<PodesavanjaDto>;

public class GetPodesavanjaHandler(IzvorPodesavanja izvor) : IRequestHandler<GetPodesavanjaQuery, PodesavanjaDto>
{
    public Task<PodesavanjaDto> Handle(GetPodesavanjaQuery q, CancellationToken ct) =>
        Task.FromResult(PodesavanjaMapa.UDto(izvor.Red));
}

public record SacuvajPodesavanjaCommand(PodesavanjaDto Podesavanja) : IRequest<PodesavanjaDto>;

public class SacuvajPodesavanjaValidator : AbstractValidator<SacuvajPodesavanjaCommand>
{
    public SacuvajPodesavanjaValidator()
    {
        RuleFor(x => x.Podesavanja.KoeficijentVrijednosti).InclusiveBetween(1m, 3m)
            .WithMessage("Koeficijent vrijednosti mora biti između 1 i 3.");
        RuleFor(x => x.Podesavanja.MinimalnaMarza).InclusiveBetween(0m, 0.9m)
            .WithMessage("Minimalna marža mora biti između 0 i 90%.");
        RuleFor(x => x.Podesavanja.BrojZamjena).InclusiveBetween(0, 20).WithMessage("Broj zamjena mora biti od 0 do 20.");
        RuleFor(x => x.Podesavanja.MinArtikalaMala).InclusiveBetween(1, 30).WithMessage("Najmanje artikala: od 1 do 30.");
        RuleFor(x => x.Podesavanja.MinArtikalaSrednja).InclusiveBetween(1, 30).WithMessage("Najmanje artikala: od 1 do 30.");
        RuleFor(x => x.Podesavanja.MinArtikalaVelika).InclusiveBetween(1, 30).WithMessage("Najmanje artikala: od 1 do 30.");
        RuleFor(x => x.Podesavanja.RokSatiRezerva).InclusiveBetween(0, 240).WithMessage("Rok mora biti od 0 do 240 sati.");
        RuleFor(x => x.Podesavanja.DanIsporukeRezerva).InclusiveBetween(0, 6).WithMessage("Nepoznat dan.");
        RuleFor(x => x.Podesavanja.VrijemeIsporukeRezerva).Matches("^([01][0-9]|2[0-3]):[0-5][0-9]$")
            .WithMessage("Vrijeme mora biti u obliku HH:MM.");
        RuleFor(x => x.Podesavanja.PopustNedjeljna).InclusiveBetween(0m, 0.9m).WithMessage("Popust mora biti između 0 i 90%.");
        RuleFor(x => x.Podesavanja.PopustMjesecna).InclusiveBetween(0m, 0.9m).WithMessage("Popust mora biti između 0 i 90%.");
        RuleFor(x => x.Podesavanja.PopustTriMjeseca).InclusiveBetween(0m, 0.9m).WithMessage("Popust mora biti između 0 i 90%.");
        RuleFor(x => x.Podesavanja.ObavezaMjesecna).InclusiveBetween(1, 52).WithMessage("Obaveza mora biti od 1 do 52 isporuke.");
        RuleFor(x => x.Podesavanja.ObavezaTriMjeseca).InclusiveBetween(1, 52).WithMessage("Obaveza mora biti od 1 do 52 isporuke.");
        RuleFor(x => x.Podesavanja.SatiUnaprijed).InclusiveBetween(0, 240).WithMessage("Od 0 do 240 sati.");
        RuleFor(x => x.Podesavanja.AlarmSatiPrijeRoka).InclusiveBetween(0, 240).WithMessage("Od 0 do 240 sati.");
        RuleFor(x => x.Podesavanja.MaksPauzaSedmica).InclusiveBetween(1, 52).WithMessage("Pauza: od 1 do 52 sedmice.");
        RuleFor(x => x.Podesavanja.PoenaPoKM).InclusiveBetween(0, 100).WithMessage("Poeni po KM: od 0 do 100.");
        RuleFor(x => x.Podesavanja.VrijednostPoenaKM).InclusiveBetween(0m, 10m).WithMessage("Vrijednost poena: od 0 do 10 KM.");
        RuleFor(x => x.Podesavanja.NagradaPreporuka).InclusiveBetween(0, 10000).WithMessage("Nagrada: od 0 do 10000 poena.");
        RuleFor(x => x.Podesavanja.PragPriKraju).InclusiveBetween(0m, 10000m).WithMessage("Prag mora biti od 0 do 10000.");
    }
}

public class SacuvajPodesavanjaHandler(IAppDbContext db) : IRequestHandler<SacuvajPodesavanjaCommand, PodesavanjaDto>
{
    public async Task<PodesavanjaDto> Handle(SacuvajPodesavanjaCommand cmd, CancellationToken ct)
    {
        var p = await db.Podesavanja.FirstOrDefaultAsync(x => x.Id == Domain.Entities.Podesavanja.JediniId, ct)
            ?? throw new InvalidOperationException("U bazi nema reda Podesavanja.");
        var d = cmd.Podesavanja;
        p.KoeficijentVrijednosti = d.KoeficijentVrijednosti;
        p.MinimalnaMarza = d.MinimalnaMarza;
        p.BrojZamjena = d.BrojZamjena;
        p.MinArtikalaMala = d.MinArtikalaMala;
        p.MinArtikalaSrednja = d.MinArtikalaSrednja;
        p.MinArtikalaVelika = d.MinArtikalaVelika;
        p.RokSatiRezerva = d.RokSatiRezerva;
        p.DanIsporukeRezerva = (DayOfWeek)d.DanIsporukeRezerva;
        p.VrijemeIsporukeRezerva = d.VrijemeIsporukeRezerva;
        p.PopustNedjeljna = d.PopustNedjeljna;
        p.PopustMjesecna = d.PopustMjesecna;
        p.PopustTriMjeseca = d.PopustTriMjeseca;
        p.ObavezaMjesecna = d.ObavezaMjesecna;
        p.ObavezaTriMjeseca = d.ObavezaTriMjeseca;
        p.SatiUnaprijed = d.SatiUnaprijed;
        p.AlarmSatiPrijeRoka = d.AlarmSatiPrijeRoka;
        p.MaksPauzaSedmica = d.MaksPauzaSedmica;
        p.PoenaPoKM = d.PoenaPoKM;
        p.VrijednostPoenaKM = d.VrijednostPoenaKM;
        p.NagradaPreporuka = d.NagradaPreporuka;
        p.PragPriKraju = d.PragPriKraju;
        await db.SaveChangesAsync(ct);
        return PodesavanjaMapa.UDto(p);
    }
}

using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using EPiljara.Application.Features.Korpice;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EPiljara.Application.Features.Nabavka;

/// <summary>
/// Pravila otkupnog lista koja dijele komande ovdje i admin izmjene narudžbi.
/// Stavke se uvijek uzimaju iz pakerske liste (<see cref="GetPakerskaListaHandler"/>), da
/// nabavka, pakovanje i otkupni list ne mogu da se raziđu.
/// </summary>
public static class OtkupniListPravila
{
    /// <summary>„OL-2026-0001": brojač po godini isporuke (n = redni broj lista te godine).</summary>
    public static string Broj(int godina, int n) => $"OL-{godina}-{n:D4}";

    /// <summary>
    /// Označava otkupne listove sedmice kao zastarjele — admin je mijenjao zaključanu narudžbu.
    /// Bez SaveChanges: pozivalac čuva zajedno sa svojom izmjenom.
    /// </summary>
    public static async Task OznaciZastaroAsync(IAppDbContext db, int godina, int sedmica, CancellationToken ct)
    {
        var listovi = await db.OtkupniListovi
            .Where(l => l.Godina == godina && l.Sedmica == sedmica && !l.Zastario)
            .ToListAsync(ct);
        foreach (var l in listovi) l.Zastario = true;
    }

    /// <summary>Redovi nabavke te sedmice, grupisani po gazdinstvu (bez artikala bez gazdinstva).</summary>
    internal static async Task<Dictionary<Guid, List<StavkaPakerskeDto>>> RedoviPoGazdinstvuAsync(
        IAppDbContext db, IOptions<KorpiceOpcije> opcije, int godina, int sedmica, CancellationToken ct)
    {
        var lista = await new GetPakerskaListaHandler(db, opcije)
            .Handle(new GetPakerskaListaQuery(godina, sedmica), ct);
        return lista.Stavke
            .Where(s => s.ProizvodjacId is not null && s.Kolicina > 0)
            .GroupBy(s => s.ProizvodjacId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    internal static List<OtkupniListStavka> Stavke(Guid listId, IEnumerable<StavkaPakerskeDto> redovi) =>
        redovi
            .OrderBy(r => r.Artikal, StringComparer.Create(new System.Globalization.CultureInfo("sr-Latn"), true))
            .Select((r, i) => new OtkupniListStavka
            {
                OtkupniListId = listId,
                ProductId = r.ProductId,
                Naziv = r.Artikal,
                Jedinica = UnitExtensions.ParseUnit(r.Jedinica),
                Naruceno = r.Kolicina,
                Cijena = r.NabavnaCijena is > 0 ? r.NabavnaCijena : null,
                Redoslijed = i + 1,
            })
            .ToList();

    internal static async Task<OtkupniListDto> UcitajDtoAsync(IAppDbContext db, Guid id, CancellationToken ct)
    {
        var l = await db.OtkupniListovi
            .AsNoTracking()
            .Include(x => x.Producer)
            .Include(x => x.Stavke)
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Otkupni list nije pronađen.");
        return ToDto(l);
    }

    internal static OtkupniListDto ToDto(OtkupniList l)
    {
        var stavke = l.Stavke.OrderBy(s => s.Redoslijed).ToList();
        var saCijenom = stavke.Where(s => s.Cijena is not null).ToList();
        var primljene = saCijenom.Where(s => s.Primljeno is not null).ToList();
        var p = l.Producer;
        return new OtkupniListDto
        {
            Id = l.Id,
            Broj = l.Broj,
            Godina = l.Godina,
            Sedmica = l.Sedmica,
            Datum = l.Datum,
            Zastario = l.Zastario,
            Napomena = l.Napomena,
            Prodavac = new OtkupniListProdavacDto
            {
                Id = l.ProducerId,
                Naziv = p?.Name ?? string.Empty,
                Selo = p?.Village ?? string.Empty,
                Telefon = p?.Phone,
                NosilacIme = p?.NosilacIme,
                Bpg = p?.Bpg,
                Jmbg = p?.Jmbg,
                Adresa = p?.Adresa,
                ZiroRacun = p?.ZiroRacun,
                Banka = p?.Banka,
            },
            Stavke = stavke.Select(s => new OtkupniListStavkaDto
            {
                Id = s.Id,
                ProductId = s.ProductId,
                Naziv = s.Naziv,
                Jedinica = s.Jedinica.ToApi(),
                Naruceno = s.Naruceno,
                Cijena = s.Cijena,
                Primljeno = s.Primljeno,
            }).ToList(),
            Iznos = saCijenom.Count == 0 ? null : Math.Round(saCijenom.Sum(s => s.Naruceno * s.Cijena!.Value), 2),
            IznosPrimljeno = primljene.Count == 0 ? null : Math.Round(primljene.Sum(s => s.Primljeno!.Value * s.Cijena!.Value), 2),
        };
    }
}

/// <summary>
/// Pravi otkupne listove koji nedostaju za zaključanu sedmicu: jedan po gazdinstvu koje ima
/// robu u nabavci. Idempotentno — gazdinstvo koje već ima list se preskače. Vraća broj novih.
/// Zove ga pozadinski posao odmah poslije zaključavanja, a admin ručno ako je potrebno.
/// </summary>
public record NapraviOtkupneListoveCommand(int Godina, int Sedmica) : IRequest<int>;

public class NapraviOtkupneListoveHandler(IAppDbContext db, IOptions<KorpiceOpcije> opcije)
    : IRequestHandler<NapraviOtkupneListoveCommand, int>
{
    public async Task<int> Handle(NapraviOtkupneListoveCommand c, CancellationToken ct)
    {
        var zakljucana = await db.Orders.AnyAsync(o => o.Godina == c.Godina && o.Sedmica == c.Sedmica
                                                       && o.Status != OrderStatus.Cancelled
                                                       && o.ZakljucanaU != null, ct);
        if (!zakljucana)
            throw new ConflictException(
                "Narudžbe ove sedmice još nisu zaključane — otkupni listovi nastaju poslije roka.",
                "sedmica_nije_zakljucana");

        var poGazdinstvu = await OtkupniListPravila.RedoviPoGazdinstvuAsync(db, opcije, c.Godina, c.Sedmica, ct);
        var postoje = await db.OtkupniListovi
            .Where(l => l.Godina == c.Godina && l.Sedmica == c.Sedmica)
            .Select(l => l.ProducerId)
            .ToListAsync(ct);
        var nova = poGazdinstvu.Keys.Except(postoje).ToList();
        if (nova.Count == 0) return 0;

        var imena = await db.Producers
            .Where(p => nova.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name, ct);
        var vecIma = await db.OtkupniListovi.CountAsync(l => l.Godina == c.Godina, ct);
        var sada = DateTime.UtcNow;
        var redni = 0;
        foreach (var producerId in nova.OrderBy(id => imena.GetValueOrDefault(id) ?? string.Empty))
        {
            var list = new OtkupniList
            {
                Broj = OtkupniListPravila.Broj(c.Godina, vecIma + ++redni),
                ProducerId = producerId,
                Godina = c.Godina,
                Sedmica = c.Sedmica,
                Datum = sada,
            };
            list.Stavke = OtkupniListPravila.Stavke(list.Id, poGazdinstvu[producerId]);
            db.OtkupniListovi.Add(list);
        }

        await db.SaveChangesAsync(ct);
        return nova.Count;
    }
}

public record GetOtkupniListoviQuery(int Godina, int Sedmica) : IRequest<IReadOnlyList<OtkupniListDto>>;

public class GetOtkupniListoviHandler(IAppDbContext db)
    : IRequestHandler<GetOtkupniListoviQuery, IReadOnlyList<OtkupniListDto>>
{
    public async Task<IReadOnlyList<OtkupniListDto>> Handle(GetOtkupniListoviQuery q, CancellationToken ct)
    {
        var listovi = await db.OtkupniListovi
            .AsNoTracking()
            .Include(l => l.Producer)
            .Include(l => l.Stavke)
            .Where(l => l.Godina == q.Godina && l.Sedmica == q.Sedmica)
            .ToListAsync(ct);
        return listovi.OrderBy(l => l.Broj).Select(OtkupniListPravila.ToDto).ToList();
    }
}

/// <summary>
/// Ponovo puni količine iz nabavke (poslije admin izmjene zaključanih narudžbi). Dozvoljeno samo
/// dok nijedna stavka nema primljenu količinu — potpisan prijem se ne prepisuje.
/// </summary>
public record OsvjeziOtkupniListCommand(Guid Id) : IRequest<OtkupniListDto>;

public class OsvjeziOtkupniListHandler(IAppDbContext db, IOptions<KorpiceOpcije> opcije)
    : IRequestHandler<OsvjeziOtkupniListCommand, OtkupniListDto>
{
    public async Task<OtkupniListDto> Handle(OsvjeziOtkupniListCommand c, CancellationToken ct)
    {
        var list = await db.OtkupniListovi
            .Include(l => l.Stavke)
            .FirstOrDefaultAsync(l => l.Id == c.Id, ct)
            ?? throw new NotFoundException("Otkupni list nije pronađen.");
        if (list.Stavke.Any(s => s.Primljeno is not null))
            throw new ConflictException(
                "Primljene količine su već unesene — otkupni list se više ne osvježava.", "otkupni_list_primljen");

        var poGazdinstvu = await OtkupniListPravila.RedoviPoGazdinstvuAsync(db, opcije, list.Godina, list.Sedmica, ct);
        db.OtkupniListStavke.RemoveRange(list.Stavke);
        foreach (var s in OtkupniListPravila.Stavke(list.Id, poGazdinstvu.GetValueOrDefault(list.ProducerId) ?? []))
            db.OtkupniListStavke.Add(s);
        list.Zastario = false;
        await db.SaveChangesAsync(ct);

        return await OtkupniListPravila.UcitajDtoAsync(db, list.Id, ct);
    }
}

public record PrimljenoStavkaInput(Guid StavkaId, decimal? Primljeno);
public record UnesiPrimljenoInput(IReadOnlyList<PrimljenoStavkaInput> Stavke);

/// <summary>Upisuje stvarno primljene količine. Samo evidencija — ne mijenja stanje ni narudžbe.</summary>
public record UnesiPrimljenoCommand(Guid Id, UnesiPrimljenoInput Input) : IRequest<OtkupniListDto>;

public class UnesiPrimljenoValidator : AbstractValidator<UnesiPrimljenoCommand>
{
    public UnesiPrimljenoValidator()
    {
        RuleFor(x => x.Input).NotNull();
        RuleFor(x => x.Input.Stavke).NotNull().When(x => x.Input is not null);
        RuleForEach(x => x.Input.Stavke)
            .Must(s => s.Primljeno is null or >= 0)
            .WithMessage("Primljena količina ne može biti negativna.")
            .When(x => x.Input?.Stavke is not null);
    }
}

public class UnesiPrimljenoHandler(IAppDbContext db) : IRequestHandler<UnesiPrimljenoCommand, OtkupniListDto>
{
    public async Task<OtkupniListDto> Handle(UnesiPrimljenoCommand c, CancellationToken ct)
    {
        var list = await db.OtkupniListovi
            .Include(l => l.Stavke)
            .FirstOrDefaultAsync(l => l.Id == c.Id, ct)
            ?? throw new NotFoundException("Otkupni list nije pronađen.");
        var stavke = list.Stavke.ToDictionary(s => s.Id);
        foreach (var u in c.Input.Stavke)
        {
            if (!stavke.TryGetValue(u.StavkaId, out var s))
                throw new DomainException("Stavka ne pripada ovom otkupnom listu.", "stavka_nepoznata");
            s.Primljeno = u.Primljeno;
        }
        await db.SaveChangesAsync(ct);
        return await OtkupniListPravila.UcitajDtoAsync(db, list.Id, ct);
    }
}

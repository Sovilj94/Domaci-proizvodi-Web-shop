using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EPiljara.Application.Features.Korpice;

/* ---------- pregled sedmice ---------- */

public record GetSadrzajPregledQuery(int Godina, int Sedmica)
    : IRequest<IReadOnlyList<SadrzajPregledDto>>;

public class GetSadrzajPregledHandler(IAppDbContext db, IOptions<KorpiceOpcije> opcije)
    : IRequestHandler<GetSadrzajPregledQuery, IReadOnlyList<SadrzajPregledDto>>
{
    public async Task<IReadOnlyList<SadrzajPregledDto>> Handle(
        GetSadrzajPregledQuery q, CancellationToken ct)
    {
        // IsAvailable filter mora da se slaže sa GenerisiSedmicniSadrzajHandler-om (koji
        // preskače nedostupne korpice) — inače admin vidi red sa dugmetom "Generiši" koje,
        // kad se klikne, vrati praznu listu i ništa se ne desi (vidi I4, finalni review).
        var korpice = await db.Products
            .Where(p => p.Vrsta == VrstaProizvoda.Korpica && p.IsAvailable)
            .OrderBy(p => p.TipKorpice).ThenBy(p => p.Velicina)
            .ToListAsync(ct);

        var sadrzaji = await db.SedmicniSadrzaji
            .Include(s => s.Stavke)
            .Where(s => s.Godina == q.Godina && s.Sedmica == q.Sedmica)
            .ToListAsync(ct);

        return korpice.Select(k =>
        {
            var s = sadrzaji.FirstOrDefault(x => x.KorpicaId == k.Id);
            return new SadrzajPregledDto
            {
                SadrzajId = s?.Id,
                KorpicaId = k.Id,
                KorpicaNaziv = k.Name,
                Tip = SedmicaHelper.Slug(k.TipKorpice ?? TipKorpice.Povrce),
                Velicina = (k.Velicina ?? VelicinaKorpice.Mala).ToString().ToLowerInvariant(),
                Status = s?.Status.ToString() ?? "Nema",
                BrojArtikala = s?.Stavke.Count ?? 0,
                Vrijednost = s?.Stavke.Sum(x => x.Kolicina * x.JedinicnaCijena) ?? 0m,
                CiljGenerisanja = k.Price * opcije.Value.KoeficijentVrijednosti,
            };
        }).ToList();
    }
}

/* ---------- jedan sadržaj za admin editor ---------- */

/// <summary>
/// Vraća sadržaj bez obzira na status — za razliku od javnog upita, koji Nacrt odbija.
/// Admin editor bi bez ovoga imao prazan ekran.
/// </summary>
public record GetSadrzajZaIzmjenuQuery(Guid Id) : IRequest<SadrzajDto>;

public class GetSadrzajZaIzmjenuHandler(IAppDbContext db)
    : IRequestHandler<GetSadrzajZaIzmjenuQuery, SadrzajDto>
{
    public async Task<SadrzajDto> Handle(GetSadrzajZaIzmjenuQuery q, CancellationToken ct)
    {
        var sadrzaj = await db.SedmicniSadrzaji
            .Include(s => s.Stavke).ThenInclude(st => st.Product).ThenInclude(p => p!.Producer)
            .FirstOrDefaultAsync(s => s.Id == q.Id, ct)
            ?? throw new NotFoundException("Sadržaj korpice nije pronađen.");

        return sadrzaj.ToDto();
    }
}

/* ---------- izmjena nacrta ---------- */

public record StavkaUnos(Guid ProductId, decimal Kolicina);

/// <summary>Zamjenjuje stavke nacrta u cjelosti. Objavljen sadržaj se ne dira.</summary>
public record IzmijeniNacrtCommand(Guid SadrzajId, IReadOnlyList<StavkaUnos> Stavke)
    : IRequest<SadrzajDto>;

public class IzmijeniNacrtHandler(IAppDbContext db)
    : IRequestHandler<IzmijeniNacrtCommand, SadrzajDto>
{
    public async Task<SadrzajDto> Handle(IzmijeniNacrtCommand c, CancellationToken ct)
    {
        var sadrzaj = await db.SedmicniSadrzaji
            .Include(s => s.Stavke)
            .FirstOrDefaultAsync(s => s.Id == c.SadrzajId, ct)
            ?? throw new NotFoundException("Sadržaj korpice nije pronađen.");

        if (sadrzaj.Status != StatusSadrzaja.Nacrt)
            throw new ConflictException(
                "Objavljen sadržaj se ne može mijenjati.", "sadrzaj_nije_nacrt");

        // Isti ProductId dvaput u zahtjevu bi, bez ove provjere, prošao postojanje-provjeru
        // ispod (ona koristi Distinct()) ali pri insertu ostavio DVA reda za jedan artikal
        // (vidi petlju ispod koja ide kroz c.Stavke, ne kroz artikli) — duplikat koji se
        // dalje vidi kao dva reda sa istim React key-om i u javnom modalu i u editoru.
        // Odbijamo, ne spajamo tiho: spajanje bi promijenilo količinu koju je operater
        // unio bez da to bilo gdje kaže.
        if (c.Stavke.Select(s => s.ProductId).Distinct().Count() != c.Stavke.Count)
            throw new DomainException(
                "Isti artikal je dodat više puta u istom nacrtu.", "stavka_duplirana");

        var idjevi = c.Stavke.Select(s => s.ProductId).Distinct().ToList();
        var artikli = await db.Products.Where(p => idjevi.Contains(p.Id)).ToListAsync(ct);

        if (artikli.Count != idjevi.Count)
            throw new NotFoundException("Jedan ili više artikala nije pronađen.");

        if (artikli.Any(a => a.Vrsta == VrstaProizvoda.Korpica))
            throw new DomainException(
                "Korpica ne može biti stavka druge korpice.", "korpica_u_korpici");

        if (c.Stavke.Any(s => s.Kolicina <= 0))
            throw new DomainException(
                "Količina mora biti veća od nule.", "kolicina_nevalidna");

        // Artikli na komade (jaja, tegle...) se ne mogu naručiti u djelovima — "0,3 kom"
        // nema smisla. Kg/L mogu biti frakcioni. Isto pravilo kao generator
        // (KoracZaokruzivanja u GenerisiSedmicniSadrzajHandler), ovdje za ručni unos.
        if (c.Stavke.Any(s =>
                artikli.First(a => a.Id == s.ProductId).Unit == EPiljara.Domain.Enums.Unit.Kom
                && s.Kolicina != Math.Floor(s.Kolicina)))
            throw new DomainException(
                "Količina za artikal na komade mora biti cio broj.", "kolicina_mora_biti_cio_broj");

        // Clear() je dovoljno za brisanje: StavkaSadrzaja → SedmicniSadrzaj je obavezna
        // veza (DeleteBehavior.Cascade), pa EF pri DetectChanges sam obriše osirotjele
        // stavke. Dodatni db.StavkeSadrzaja.RemoveRange(...) prije ovoga dupliciralo je
        // komandu brisanja za isti red i izazivalo DbUpdateConcurrencyException.
        sadrzaj.Stavke.Clear();

        // Nove stavke se dodaju i preko DbSet-a, ne samo preko navigacije. sadrzaj je
        // ovdje već praćen entitet (učitan upitom iznad), pa EF-ova automatska fixup-logika
        // za entitet dohvaćen samo preko sadrzaj.Stavke.Add(...) pokušava da pogodi da li je
        // nov na osnovu toga da li mu je ključ "default" — a StavkaSadrzaja.Id već ima
        // konkretan Guid (Entity.Id = Guid.NewGuid() pri konstrukciji), pa ga EF pogrešno
        // tretira kao Modified umjesto Added i generiše UPDATE za red koji ne postoji
        // (DbUpdateConcurrencyException: affected 0 row(s)). Explicit Add() to rješava.
        foreach (var unos in c.Stavke)
        {
            var artikal = artikli.First(a => a.Id == unos.ProductId);
            var stavka = new StavkaSadrzaja
            {
                ProductId = artikal.Id,
                Kolicina = unos.Kolicina,
                JedinicnaCijena = artikal.Price,
            };
            sadrzaj.Stavke.Add(stavka);
            db.StavkeSadrzaja.Add(stavka);
        }

        await db.SaveChangesAsync(ct);
        return sadrzaj.ToDto();
    }
}

/* ---------- pakerska lista ---------- */

public record GetPakerskaListaQuery(int Godina, int Sedmica) : IRequest<PakerskaListaDto>;

public class GetPakerskaListaHandler(IAppDbContext db)
    : IRequestHandler<GetPakerskaListaQuery, PakerskaListaDto>
{
    public async Task<PakerskaListaDto> Handle(GetPakerskaListaQuery q, CancellationToken ct)
    {
        // Od P2b lista sabira SADRŽAJ NARUDŽBI, ne šablon. Prije toga je sabirala
        // StavkeSadrzaja — redove jedne kutije — pa je četrdeset narudžbi i jedna davale isti
        // broj, i nabavka po toj listi je bila za jednu korpicu.
        //
        // Nema IgnoreQueryFilters() ovdje: StavkaKorpiceNarudzbe, OrderItem i Order nemaju
        // globalni query filter (jedini u šemi je Product-ov !IsDeleted, AppDbContext.cs:91),
        // pa ga ovaj Include lanac (OrderItem → Order) ne dira. Zaštita za soft-obrisan
        // artikal je ispod, na upitu nad Products (vidi komentar tamo).
        var redovi = await db.StavkeKorpiceNarudzbe
            .Include(s => s.OrderItem).ThenInclude(i => i!.Order)
            .Where(s => s.OrderItem!.Order!.Godina == q.Godina
                        && s.OrderItem.Order.Sedmica == q.Sedmica
                        && s.OrderItem.Order.Status != OrderStatus.Cancelled)
            .ToListAsync(ct);

        var brojNarudzbi = redovi
            .Select(s => s.OrderItem!.OrderId)
            .Distinct()
            .Count();

        // Artikal se dohvata posebno, jer StavkaKorpiceNarudzbe namjerno nema navigaciju na
        // Product — snapshot naziva živi na samom redu, a proizvođač i selo se čitaju iz
        // trenutnog kataloga jer su podatak o nabavci, ne o prodaji.
        var idjevi = redovi.Select(s => s.ProductId).Distinct().ToList();
        var artikli = await db.Products
            .IgnoreQueryFilters()
            .Include(p => p.Producer)
            .Where(p => idjevi.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        var stavke = redovi
            .GroupBy(s => s.ProductId)
            .Select(g =>
            {
                artikli.TryGetValue(g.Key, out var p);
                var prvi = g.First();
                return new StavkaPakerskeDto
                {
                    Artikal = p?.Name ?? prvi.ProductName,
                    Kolicina = g.Sum(x => x.Kolicina * x.OrderItem!.Quantity),
                    Jedinica = (p?.Unit ?? prvi.Unit).ToApi(),
                    Proizvodjac = p?.Producer?.Name ?? string.Empty,
                    Selo = p?.Producer?.Village ?? string.Empty,
                    ArtikalObrisan = p is null || p.IsDeleted,
                };
            })
            .OrderBy(r => r.Proizvodjac).ThenBy(r => r.Artikal)
            .ToList();

        return new PakerskaListaDto { BrojNarudzbi = brojNarudzbi, Stavke = stavke };
    }
}

using System.Globalization;
using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using EPiljara.Application.Features.Korpice;
using EPiljara.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EPiljara.Application.Features.Admin;

/// <summary>
/// Šta admin treba da uradi — početni ekran panela i brojke uz stavke menija. Svaki zadatak
/// nosi link na mjesto gdje se rješava; zadatak sa brojem 0 se ne vraća.
/// </summary>
public record GetAdminZadaciQuery : IRequest<AdminZadaciDto>;

public class GetAdminZadaciHandler(IAppDbContext db, IOptions<KorpiceOpcije> opcije)
    : IRequestHandler<GetAdminZadaciQuery, AdminZadaciDto>
{
    public async Task<AdminZadaciDto> Handle(GetAdminZadaciQuery q, CancellationToken ct)
    {
        var zadaci = new List<ZadatakDto>();

        var naCekanju = await db.Orders.CountAsync(o => o.Status == OrderStatus.Pending, ct);
        if (naCekanju > 0)
            zadaci.Add(new ZadatakDto
            {
                Kljuc = "narudzbe_na_cekanju", Nivo = "hitno", Broj = naCekanju,
                Naslov = Mnozina(naCekanju, "narudžba čeka", "narudžbe čekaju", "narudžbi čeka") + " potvrdu",
                Opis = "Kupac ne zna da je narudžba primljena dok je ne potvrdiš.",
                Link = "/admin/orders?status=Pending", Akcija = "Potvrdi",
            });

        // Korpice: sedmica koju checkout upravo prodaje (poslije roka je to naredna).
        var (dan, vrijemeOd, cutoff) = await SedmicaHelper.UcitajRokAsync(db, opcije.Value, ct);
        var (godina, sedmica) = SedmicaHelper.SedmicaZaNarudzbu(dan, vrijemeOd, cutoff);
        var isporuka = SedmicaHelper.DatumIsporuke(godina, sedmica, dan, vrijemeOd);
        var korpice = await db.Products
            .Where(p => p.Vrsta == VrstaProizvoda.Korpica && p.IsAvailable)
            .Select(p => p.Id)
            .ToListAsync(ct);
        var sadrzaji = await db.SedmicniSadrzaji
            .Where(s => s.Godina == godina && s.Sedmica == sedmica && korpice.Contains(s.KorpicaId))
            .Select(s => s.Status)
            .ToListAsync(ct);
        var bezSadrzaja = korpice.Count - sadrzaji.Count;
        var nacrti = sadrzaji.Count(s => s == StatusSadrzaja.Nacrt);
        var neobjavljeno = bezSadrzaja + nacrti;
        if (neobjavljeno > 0)
            zadaci.Add(new ZadatakDto
            {
                Kljuc = "sadrzaj_korpica", Nivo = "hitno", Broj = neobjavljeno,
                Naslov = $"Sadržaj korpica za sedmicu {sedmica} nije objavljen ({neobjavljeno} od {korpice.Count})",
                Opis = (bezSadrzaja > 0 ? $"{bezSadrzaja} bez sadržaja, " : "") + $"{nacrti} u nacrtu. " +
                       $"Isporuka {isporuka.ToString("dd.MM.", CultureInfo.InvariantCulture)} — kupci ne vide šta je u kutiji dok se ne objavi.",
                // Stranica korpica bira sedmicu po datumu (?datum=), kao i ostatak admina.
                Link = $"/admin/korpice?datum={ISOWeek.ToDateTime(godina, sedmica, DayOfWeek.Thursday):yyyy-MM-dd}",
                Akcija = bezSadrzaja > 0 ? "Generiši" : "Pregledaj i objavi",
            });

        var potvrdjene = await db.Orders.CountAsync(o => o.Status == OrderStatus.Confirmed, ct);
        if (potvrdjene > 0)
            zadaci.Add(new ZadatakDto
            {
                Kljuc = "za_pakovanje", Nivo = "info", Broj = potvrdjene,
                Naslov = Mnozina(potvrdjene, "potvrđena narudžba", "potvrđene narudžbe", "potvrđenih narudžbi") + " za pakovanje",
                Opis = "Pakerska lista i dodjela vozača.",
                Link = "/admin/pakovanje", Akcija = "Pakovanje",
            });

        // Artikli u sezoni bez zalihe — generator ih preskače, pa korpice ostaju tanje.
        var mjesec = ISOWeek.ToDateTime(godina, sedmica, DayOfWeek.Thursday).Month;
        var obicni = await db.Products
            .Where(p => p.Vrsta == VrstaProizvoda.Obican && p.IsAvailable)
            .Select(p => new { p.Name, p.Stock, p.MozeUKorpicu, p.Mjeseci, p.NabavnaCijena })
            .ToListAsync(ct);
        var bezZalihe = obicni
            .Where(p => p.Stock <= 0 && (p.Mjeseci.Length == 0 || p.Mjeseci.Contains(mjesec)))
            .Select(p => p.Name).OrderBy(n => n).ToList();
        if (bezZalihe.Count > 0)
            zadaci.Add(new ZadatakDto
            {
                Kljuc = "bez_zalihe", Nivo = "upozorenje", Broj = bezZalihe.Count,
                Naslov = Mnozina(bezZalihe.Count, "artikal u sezoni nema", "artikla u sezoni nemaju", "artikala u sezoni nema") + " zalihu",
                Opis = string.Join(", ", bezZalihe.Take(5)) + (bezZalihe.Count > 5 ? " …" : "") + " — ne mogu se kupiti ni ući u korpicu.",
                Link = "/admin/products?filter=bez-zalihe", Akcija = "Dopuni zalihu",
            });

        var bezNabavne = obicni.Count(p => p.MozeUKorpicu && p.NabavnaCijena is null or <= 0);
        if (bezNabavne > 0)
            zadaci.Add(new ZadatakDto
            {
                Kljuc = "bez_nabavne", Nivo = "upozorenje", Broj = bezNabavne,
                Naslov = Mnozina(bezNabavne, "artikal za korpice nema", "artikla za korpice nemaju", "artikala za korpice nema") + " nabavnu cijenu",
                Opis = "Generator im ne može provjeriti maržu.",
                Link = "/admin/products?filter=bez-nabavne", Akcija = "Unesi nabavne",
            });

        var prijave = await db.PartnerPrijave.CountAsync(p => p.Status == StatusPrijave.Nova, ct);
        if (prijave > 0)
            zadaci.Add(new ZadatakDto
            {
                Kljuc = "prijave_partnera", Nivo = "info", Broj = prijave,
                Naslov = Mnozina(prijave, "nova prijava", "nove prijave", "novih prijava") + " domaćina",
                Opis = "Javi se domaćinu i označi prijavu kao obrađenu.",
                Link = "/admin/partneri", Akcija = "Pogledaj",
            });

        return new AdminZadaciDto
        {
            Godina = godina,
            Sedmica = sedmica,
            Zadaci = zadaci,
            Meni = new Dictionary<string, int>
            {
                ["narudzbe"] = naCekanju,
                ["korpice"] = neobjavljeno,
                ["proizvodi"] = bezZalihe.Count,
                ["partneri"] = prijave,
            },
        };
    }

    /// <summary>„1 narudžba čeka", „3 narudžbe čekaju", „5 narudžbi čeka" (11–14 kao 5).</summary>
    private static string Mnozina(int n, string jedan, string dvaDoCetiri, string pet)
    {
        var oblik = (n % 10, n % 100) switch
        {
            (1, not 11) => jedan,
            (>= 2 and <= 4, < 12 or > 14) => dvaDoCetiri,
            _ => pet,
        };
        return $"{n} {oblik}";
    }
}

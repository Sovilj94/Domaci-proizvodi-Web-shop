using EPiljara.Application.Common.Interfaces;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Unit = EPiljara.Domain.Enums.Unit;

namespace EPiljara.Application.Features.Korpice;

/// <summary>
/// Jedno mjesto gdje sadržaj sedmičnog šablona dospije na stavku narudžbe.
///
/// Narudžba u ovom sistemu nastaje na DVA mjesta — <c>CreateOrderHandler</c> (prijavljen
/// kupac) i <c>ConfirmGuestOrderHandler</c> (klik na link iz mejla). Prvi ju je verzija P2b
/// naučila da zalijepi sadržaj korpice, drugi nije, pa je korpica gosta bila nevidljiva
/// svakoj pakerskoj listi — tačno nabavna greška zbog koje P2b uopšte postoji. Zato ovo
/// NIJE kopiran blok nego jedan poziv: treća putanja koja ikad napravi narudžbu (pretplate
/// u P3) mora platiti isti račun, a ne otkriti to kroz nenaručenu robu.
///
/// <c>DbSeeder</c> je svjesno izostavljen — demo narudžbe ne trebaju sadržaj korpice.
/// </summary>
public static class SnapshotKorpice
{
    /// <summary>
    /// Ako je <paramref name="proizvod"/> korpica, kopira redove objavljenog sedmičnog
    /// sadržaja za (<paramref name="godina"/>, <paramref name="sedmica"/>) u
    /// <see cref="OrderItem.KorpicaStavke"/>, sa <c>DodaoKupac = false</c>. Za običan
    /// artikal ne radi ništa.
    ///
    /// Snima se, ne referencira: admin koji kasnije izmijeni šablon ne smije promijeniti ono
    /// što je kupac kupio.
    ///
    /// Baca <see cref="DomainException"/> sa kodom <c>korpica_bez_sadrzaja</c> kad za tu
    /// sedmicu nema objavljenog sadržaja — prodaja kutije koju ne možemo napuniti je gora
    /// od odbijene narudžbe.
    ///
    /// Za prijavljenog kupca (<paramref name="kupacId"/>) se artikli sa njegovog spiska „ne
    /// stavljaj mi" odmah mijenjaju zamjenom — vidi <see cref="AutoZamjena.PrimijeniIzbjegnuteAsync"/>.
    /// Cijena kutije (<see cref="OrderItem.UnitPrice"/>) mora biti postavljena prije poziva.
    /// </summary>
    public static async Task PricvrstiAsync(
        IAppDbContext db,
        OrderItem stavka,
        Product proizvod,
        int godina,
        int sedmica,
        Guid? kupacId,
        KorpiceOpcije opcije,
        CancellationToken ct)
    {
        if (proizvod.Vrsta != VrstaProizvoda.Korpica) return;

        var sadrzaj = await db.SedmicniSadrzaji
            .Include(s => s.Stavke)
            .Include(s => s.Zamjene)
            .FirstOrDefaultAsync(
                s => s.KorpicaId == proizvod.Id
                     && s.Godina == godina
                     && s.Sedmica == sedmica
                     && s.Status == StatusSadrzaja.Objavljen, ct)
            ?? throw new DomainException(
                $"Za korpicu {proizvod.Name} još nema objavljenog sadržaja za tu sedmicu.",
                "korpica_bez_sadrzaja");

        var redovi = await SablonAsync(db, stavka.Id, sadrzaj, ct);
        if (kupacId is not null)
        {
            var mjesec = SedmicaHelper.DatumIsporuke(godina, sedmica, DayOfWeek.Monday, "00:00").Month;
            await AutoZamjena.PrimijeniIzbjegnuteAsync(
                db, redovi, stavka.Id, sadrzaj, proizvod, stavka.UnitPrice, kupacId.Value, mjesec, opcije, ct);
        }
        foreach (var r in redovi) stavka.KorpicaStavke.Add(r);
    }

    /// <summary>Redovi narudžbe tačno kako ih šablon sedmice propisuje (DodaoKupac = false).</summary>
    public static async Task<List<StavkaKorpiceNarudzbe>> SablonAsync(
        IAppDbContext db, Guid orderItemId, SedmicniSadrzaj sadrzaj, CancellationToken ct)
    {
        var redovi = new List<StavkaKorpiceNarudzbe>();
        foreach (var ss in sadrzaj.Stavke)
        {
            // IgnoreQueryFilters: artikal je mogao biti soft-obrisan (IsDeleted) između
            // objave sadržaja i ove narudžbe. Snapshot bilježi šta je stvarno bilo u
            // kutiji — obrisan-iz-kataloga artikal i dalje ima naziv i jedinicu; samo
            // potpuno nestao red (hard delete, u praksi se ne dešava) padne na ??.
            var artikal = await db.Products.IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.Id == ss.ProductId, ct);
            redovi.Add(new StavkaKorpiceNarudzbe
            {
                OrderItemId = orderItemId,
                ProductId = ss.ProductId,
                ProductName = artikal?.Name ?? string.Empty,
                Unit = artikal?.Unit ?? Unit.Kom,
                Kolicina = ss.Kolicina,
                JedinicnaCijena = ss.JedinicnaCijena,
                DodaoKupac = false,
            });
        }
        return redovi;
    }
}

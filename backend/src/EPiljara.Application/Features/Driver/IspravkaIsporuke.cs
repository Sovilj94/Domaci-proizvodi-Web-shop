using EPiljara.Application.Common.Interfaces;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Driver;

/// <summary>
/// Poništava posljedice isporuke kad vozač ispravi „Primopredaja izvršena" u „Neuspjela dostava"
/// (13.11.2026: „moze se desiti da slucajno pritisne jedno ili drugo"). Zove se PRIJE
/// <see cref="Order.PonistiIsporuku"/> i prije SaveChanges, isto kao BrojacIsporuka i PoeniPravila,
/// da sve ode u jedan upis. Ponovna isporuka poslije ispravke ponovo pokreće ista pravila, pa se
/// brojač, poeni i račun vraćaju sami.
/// </summary>
public static class IspravkaIsporuke
{
    private static readonly VrstaPoena[] PoeniIsporuke =
        [VrstaPoena.Isporuka, VrstaPoena.PreporukaNovi, VrstaPoena.PreporukaPreporucio];

    public static async Task PonistiAsync(IAppDbContext db, Order order, CancellationToken ct)
    {
        if (order.Status != OrderStatus.Delivered) return;

        // Pretplata: isporuka koja se nije desila ne smije približiti kupca popustu.
        if (order.PretplataId is Guid pretplataId)
        {
            var pretplata = await db.Pretplate.FirstOrDefaultAsync(p => p.Id == pretplataId, ct);
            if (pretplata is { BrojIsporuka: > 0 }) pretplata.BrojIsporuka--;
        }

        // Poeni za isporuku i nagrada za preporuku (data uz prvu isporuku) se brišu; ispravka je
        // istog dana, pa ih kupac praktično nije stigao potrošiti. Povrat i korekcije ostaju.
        var poeni = await db.PoeniStavke
            .Where(s => s.OrderId == order.Id && PoeniIsporuke.Contains(s.Vrsta))
            .ToListAsync(ct);
        db.PoeniStavke.RemoveRange(poeni);

        // Fiskalni račun: dok nije ni pokušan, samo se briše; poslan (ili u pokušajima) dobija
        // refundaciju, koju posao šalje tek kad je prometni izdat.
        var promet = await db.FiskalniRacuni
            .Where(r => r.OrderId == order.Id && r.Vrsta == VrstaFiskalnogRacuna.Promet
                        && !r.Refundiran)
            .OrderByDescending(r => r.Kreiran)
            .FirstOrDefaultAsync(ct);
        if (promet is null) return;

        if (promet.Status == StatusFiskalnogRacuna.NaCekanju && promet.Pokusaja == 0)
        {
            db.FiskalniRacuni.Remove(promet);
            return;
        }

        promet.Refundiran = true;
        db.FiskalniRacuni.Add(new FiskalniRacun
        {
            OrderId = order.Id,
            Vrsta = VrstaFiskalnogRacuna.Refundacija,
            Iznos = promet.Iznos,
            NacinPlacanja = promet.NacinPlacanja,
            StavkeJson = promet.StavkeJson,
            RefundiraniRacunId = promet.Id,
        });
    }
}

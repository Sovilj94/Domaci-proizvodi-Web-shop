using EPiljara.Domain.Common;

namespace EPiljara.Application.Features.Korpice;

/// <summary>
/// Kutija ne smije vrijediti više od cilja: novo se dodaje samo iz onoga što se oslobodi
/// vađenjem ili smanjivanjem nečeg drugog. Kutija koja je već preko cilja (stari podatak,
/// zaokruživanje generatora) smije samo da se smanjuje — nikad da poraste.
/// Važi za admina: sedmični nacrt i korpicu jednog kupca. Kupac ionako samo mijenja
/// artikal za artikal iste vrijednosti (vidi <see cref="IzmijeniMojuKorpicuHandler"/>).
/// </summary>
public static class GranicaKutije
{
    // Pola feninga: zbir decimala sa klijenta i sa servera se ne smije razići na zaokruživanju.
    private const decimal Tolerancija = 0.005m;

    public static void Provjeri(decimal staraVrijednost, decimal novaVrijednost, decimal cilj)
    {
        if (cilj <= 0m) return;
        var granica = Math.Max(cilj, staraVrijednost);
        if (novaVrijednost <= granica + Tolerancija) return;

        var slobodno = Math.Max(0m, granica - staraVrijednost);
        throw new DomainException(
            $"Korpica bi vrijedila {novaVrijednost:0.00} KM, a smije najviše {granica:0.00} KM. " +
            (slobodno > 0m
                ? $"Za dodavanje je slobodno još {slobodno:0.00} KM — za više prvo izvadi ili smanji neki artikal."
                : "Da bi nešto dodao, prvo izvadi ili smanji neki artikal."),
            "korpica_preko_cilja");
    }
}

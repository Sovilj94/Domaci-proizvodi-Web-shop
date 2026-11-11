using EPiljara.Application.Features.Geocoding;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;

namespace EPiljara.Application.Features.Orders;

/// <summary>
/// Cijena dostave i minimum narudžbe — jedno pravilo za cijeli grad (odluka vlasnika 11.11.2026.):
/// narudžba koja sadrži bar jednu korpicu ima dostavu po <see cref="DostavaOpcije.DostavaSaKorpicom"/>,
/// ostale po <see cref="DostavaOpcije.DostavaBezKorpice"/>. Iznosi su u <c>Podesavanja</c>.
/// Naselje na cijenu ne utiče.
/// </summary>
public static class DostavaPravila
{
    public static bool ImaKorpicu(IEnumerable<Product> proizvodi) =>
        proizvodi.Any(p => p.Vrsta == VrstaProizvoda.Korpica);

    public static decimal Cijena(bool imaKorpicu, DostavaOpcije o) =>
        imaKorpicu ? o.DostavaSaKorpicom : o.DostavaBezKorpice;

    public static void ProvjeriMinimum(decimal subtotal, DostavaOpcije o)
    {
        if (subtotal < o.MinimalnaNarudzba)
            throw new DomainException(
                $"Minimalna narudžba je {o.MinimalnaNarudzba:0.##} KM.", "below_minimum");
    }
}

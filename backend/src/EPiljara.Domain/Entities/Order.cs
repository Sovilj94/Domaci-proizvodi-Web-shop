using EPiljara.Domain.Common;
using EPiljara.Domain.Enums;

namespace EPiljara.Domain.Entities;

public class Order : Entity
{
    public string Code { get; set; } = string.Empty; // e.g. "EP-2607-0042"

    /// <summary>
    /// Trajni ključ za praćenje narudžbe bez naloga. Ne ističe i ne briše se —
    /// razlikuj ga od tokena zahtjeva gosta, koji živi 15 minuta i služi da
    /// narudžba uopšte nastane. Ide u mejl i nigdje drugdje.
    /// </summary>
    public string PublicToken { get; set; } = string.Empty;

    /// <summary>Set when a registered customer placed the order; null for guest checkout.</summary>
    public Guid? CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerPhone { get; set; }
    public string? CustomerEmail { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    public decimal Subtotal { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// Snimak primijenjenog promo koda — prikaz narudžbe ne zavisi od tabele kodova.
    /// <c>Popust</c> je već uračunat u <c>TotalAmount</c>.
    /// </summary>
    public Guid? PromoKodId { get; set; }
    public string? PromoKod { get; set; }
    public decimal Popust { get; set; }

    /// <summary>Poeni potrošeni na ovu narudžbu i njihova vrijednost u KM; već su u TotalAmount.</summary>
    public int IskoristeniPoeni { get; set; }
    public decimal PoeniPopust { get; set; }

    public Guid DeliverySlotId { get; set; }
    public DeliverySlot? DeliverySlot { get; set; }

    public Guid DeliveryZoneId { get; set; }
    public DeliveryZone? DeliveryZone { get; set; }

    public string DeliveryAddress { get; set; } = string.Empty;
    public string? Note { get; set; }

    /// <summary>
    /// Geografska širina i dužina adrese dostave. null kad geokodiranje nije uspjelo —
    /// takva narudžba se i dalje može dodijeliti ruti, samo nema pin na mapi vozača.
    /// </summary>
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// ISO 8601 godina i sedmica DATUMA ISPORUKE — ne datuma narudžbe. Korpica kupljena u
    /// ovoj narudžbi nosi sadržaj te sedmice, a pakerska lista po njoj grupiše.
    ///
    /// Narudžbama otprije P2b migracija je upisala sedmicu izvedenu iz CreatedAt. Za njih je
    /// vrijednost PRIBLIŽNA — ko je naručio u nedjelju za četvrtak naredne sedmice ima upisanu
    /// pogrešnu. To se prihvata jer nijedna od njih ne nosi sadržaj korpice koji bi o tu
    /// sedmicu visio; polje im je samo metapodatak.
    /// </summary>
    public int Godina { get; set; }
    public int Sedmica { get; set; }

    /// <summary>
    /// Trenutak (UTC) kad je vozač zatvorio stanicu — status prešao u Delivered ili
    /// FailedDelivery. null za sve narudžbe od prije uvođenja ovog polja i za one koje
    /// još nisu isporučene; to je normalno stanje, ne greška.
    /// </summary>
    public DateTime? ZatvorenaU { get; set; }

    /// <summary>
    /// Pretplata iz koje je ova narudžba nastala. `null` znači da ju je napravio čovjek —
    /// kupac na checkoutu, gost preko potvrde mejlom, ili admin. Nije samo metapodatak: po
    /// njemu <c>BrojacIsporuka</c> zna koju pretplatu da uveća kad narudžba bude isporučena,
    /// i po njemu ekran pretplate prikazuje njenu istoriju.
    ///
    /// Namjerno bez navigacije ka <c>Pretplata</c>: broj isporuka se računa upitom, a
    /// kolekcija narudžbi na pretplati bi se učitavala svaki put kad se pretplata dohvati.
    /// </summary>
    public Guid? PretplataId { get; set; }

    /// <summary>Pismo kupca u trenutku narudžbe — njime idu mejl i Viber.</summary>
    public Pismo Pismo { get; set; } = Pismo.Latinica;

    /// <summary>
    /// Ocjena isporuke. FK je na ocjeni (nema kolone na narudžbi); <c>ToDto</c> čita ovu
    /// navigaciju, pa upiti koji pune <c>mozeSeOcijeniti</c> moraju je <c>Include</c>-ovati.
    /// </summary>
    public OcjenaIsporuke? Ocjena { get; set; }

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();

    /// <summary>Allowed forward transitions for the order status state machine.</summary>
    public static readonly IReadOnlyDictionary<OrderStatus, OrderStatus[]> Transitions =
        new Dictionary<OrderStatus, OrderStatus[]>
        {
            [OrderStatus.Pending] = [OrderStatus.Confirmed, OrderStatus.Cancelled],
            [OrderStatus.Confirmed] = [OrderStatus.Packing, OrderStatus.Cancelled],
            [OrderStatus.Packing] = [OrderStatus.OutForDelivery],
            [OrderStatus.OutForDelivery] = [OrderStatus.Delivered, OrderStatus.FailedDelivery],
            [OrderStatus.Delivered] = [],
            [OrderStatus.FailedDelivery] = [OrderStatus.OutForDelivery, OrderStatus.Cancelled],
            [OrderStatus.Cancelled] = [],
        };

    public bool CanTransitionTo(OrderStatus next) =>
        Status == next || Transitions[Status].Contains(next);

    public void TransitionTo(OrderStatus next)
    {
        if (!CanTransitionTo(next))
            throw new DomainException(
                $"Prelaz statusa {Status} → {next} nije dozvoljen.", "invalid_transition");
        Status = next;
    }
}

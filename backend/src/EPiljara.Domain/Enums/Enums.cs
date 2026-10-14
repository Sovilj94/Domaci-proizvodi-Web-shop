namespace EPiljara.Domain.Enums;

public enum OrderStatus
{
    Pending,
    Confirmed,
    Packing,
    OutForDelivery,
    Delivered,
    FailedDelivery,
    Cancelled,
}

public enum RouteStatus
{
    Planned,
    Optimized,
    InProgress,
    Completed,
}

/// <summary>Sale unit. Serialized to the API as "kg" / "kom" / "l".</summary>
public enum Unit
{
    Kg,
    Kom,
    L,
}

public enum UserRole
{
    Customer,
    Admin,
    Driver,
}

/// <summary>Da li je artikal obična roba ili korpica (pakovanje sa sedmičnim sadržajem).</summary>
public enum VrstaProizvoda
{
    Obican,
    Korpica,
}

public enum TipKorpice
{
    Povrce,
    Voce,
    Mlijecna,
    Suhomesnata,
    Domacinska,
}

public enum VelicinaKorpice
{
    Mala,
    Srednja,
    Velika,
}

/// <summary>Stanje sedmičnog sadržaja. Prelazi idu samo naprijed — vidi SedmicniSadrzaj.Prelazi.</summary>
public enum StatusSadrzaja
{
    Nacrt,
    Objavljen,
    Zakljucan,
}

/// <summary>Težina pripreme recepta. Na žicu ide kao ime vrijednosti („Lako", „Srednje", „Zahtjevno").</summary>
public enum TezinaRecepta
{
    Lako,
    Srednje,
    Zahtjevno,
}

/// <summary>Ishod laboratorijske analize. Na žicu ide kao ime vrijednosti ("Uskladjeno", "NijeUskladjeno").</summary>
public enum RezultatAnalize
{
    Uskladjeno,
    NijeUskladjeno,
}

public static class UnitExtensions
{
    public static string ToApi(this Unit unit) => unit switch
    {
        Unit.Kg => "kg",
        Unit.Kom => "kom",
        Unit.L => "l",
        _ => "kom",
    };

    public static Unit ParseUnit(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "kg" => Unit.Kg,
        "l" => Unit.L,
        _ => Unit.Kom,
    };
}

/// <summary>
/// Stanje pretplate. Iz Otkazana se ne izlazi — vidi Pretplata.Prelazi.
/// </summary>
public enum StatusPretplate
{
    Aktivna,
    Pauzirana,
    Otkazana,
}

/// <summary>Stanje obrade prijave partnera. Na žicu ide kao ime vrijednosti ("Nova", "UObradi", "Prihvacena", "Odbijena").</summary>
public enum StatusPrijave
{
    Nova,
    UObradi,
    Prihvacena,
    Odbijena,
}

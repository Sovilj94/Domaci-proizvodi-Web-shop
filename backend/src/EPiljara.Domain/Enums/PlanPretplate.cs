namespace EPiljara.Domain.Enums;

/// <summary>
/// Plan pretplate (R2, po uzoru na farmit NEDELJNA · MESEČNA · 3 MESECA). Plaća se pouzećem
/// pri svakoj isporuci; plan određuje popust i obavezu (vidi PretplatnaCijena). Čuva se kao tekst.
/// </summary>
public enum PlanPretplate
{
    Nedjeljna,
    Mjesecna,
    TriMjeseca,
}

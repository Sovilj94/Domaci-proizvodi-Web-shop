using EPiljara.Domain.Common;

namespace EPiljara.Domain.Entities;

/// <summary>E-mail iz footera „Prijavi se i budi u toku". Jedan red po adresi (jedinstven indeks).</summary>
public class PrijavaNovosti : Entity
{
    public string Email { get; set; } = string.Empty;
    public DateTime Kreirana { get; set; } = DateTime.UtcNow;
}

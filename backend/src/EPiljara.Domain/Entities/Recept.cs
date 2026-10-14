using EPiljara.Domain.Common;
using EPiljara.Domain.Enums;

namespace EPiljara.Domain.Entities;

/// <summary>
/// Recept sa sastojcima koji se mogu vezati za proizvode iz ponude (spec P5). Unosi ga admin;
/// javno se vide samo objavljeni.
/// </summary>
public class Recept : Entity
{
    public string Naslov { get; set; } = string.Empty;
    /// <summary>Jedinstven; nastaje pri kreiranju i ne mijenja se kad se naslov promijeni (O15).</summary>
    public string Slug { get; set; } = string.Empty;
    public string Opis { get; set; } = string.Empty;
    public string? SlikaUrl { get; set; }
    public int VrijemeMinuta { get; set; }
    public int Porcije { get; set; }
    public TezinaRecepta Tezina { get; set; } = TezinaRecepta.Lako;
    /// <summary>Jedan tekst, korak po redu; na žicu ide razbijen po redovima.</summary>
    public string Koraci { get; set; } = string.Empty;
    public bool Objavljen { get; set; }
    public DateTime Kreiran { get; set; } = DateTime.UtcNow;

    public List<ReceptSastojak> Sastojci { get; set; } = [];
}

/// <summary>Sastojak recepta; <see cref="ProductId"/> je opcion (so, biber… nemaju proizvod).</summary>
public class ReceptSastojak : Entity
{
    public Guid ReceptId { get; set; }
    public Recept? Recept { get; set; }
    public string Naziv { get; set; } = string.Empty;
    /// <summary>Slobodan tekst („200 g", „1/2 glavice"); prazan kad nije bitno.</summary>
    public string Kolicina { get; set; } = string.Empty;
    public Guid? ProductId { get; set; }
    public Product? Product { get; set; }
    public int Redoslijed { get; set; }
}

using System.Globalization;
using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.Features.Korpice;
using EPiljara.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EPiljara.Application.Tests;

/// <summary>
/// Demo katalog iz seedera mora puniti korpice povrća i voća svih veličina u svakom mjesecu —
/// uz sezone iz docs/sezonski-kalendar.md (Velika traži 9 artikala i u februaru).
/// </summary>
public class SezonskaPonudaTests
{
    private sealed class LazniHasher : IPasswordHasher
    {
        public string Hash(string password) => password;
        public bool Verify(string password, string hash) => password == hash;
    }

    private static readonly KorpiceOpcije O = new();

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    [InlineData(5)] [InlineData(6)] [InlineData(7)] [InlineData(8)]
    [InlineData(9)] [InlineData(10)] [InlineData(11)] [InlineData(12)]
    public async Task Seedovan_katalog_puni_korpice_povrca_i_voca_u_svakom_mjesecu(int mjesec)
    {
        using var t = new TestDb();
        await DbSeeder.SeedAsync(t.Db, new LazniHasher());

        // Sredina mjeseca: četvrtak te ISO sedmice je sigurno u istom mjesecu.
        var dan = new DateTime(2027, mjesec, 15);
        var rez = await new GenerisiSedmicniSadrzajHandler(t.Db, Options.Create(O))
            .Handle(new GenerisiSedmicniSadrzajCommand(ISOWeek.GetYear(dan), ISOWeek.GetWeekOfYear(dan), null), default);

        rez.Should().HaveCount(6, "prodaju se korpice povrća i voća u tri veličine");
        foreach (var r in rez)
        {
            var korpica = await t.Db.Products.SingleAsync(p => p.Id == r.KorpicaId);
            r.Upozorenje.Should().BeNull($"{r.KorpicaNaziv} u mjesecu {mjesec}");
            r.BrojArtikala.Should().BeGreaterThanOrEqualTo(O.MinimalanBrojArtikala[korpica.Velicina!.Value], r.KorpicaNaziv);
            r.Vrijednost.Should().BeGreaterThanOrEqualTo(r.CiljGenerisanja * 0.95m, r.KorpicaNaziv);
            r.Marza.Should().BeGreaterThanOrEqualTo(O.MinimalnaMarza, r.KorpicaNaziv);
            r.BezNabavneCijene.Should().BeEmpty(r.KorpicaNaziv);
        }
    }

    [Fact]
    public async Task Sezonska_ponuda_se_dodaje_jednom_i_na_vec_seedovanoj_bazi()
    {
        using var t = new TestDb();
        await DbSeeder.SeedAsync(t.Db, new LazniHasher());
        var broj = await t.Db.Products.CountAsync();

        await DbSeeder.SeedAsync(t.Db, new LazniHasher());

        (await t.Db.Products.CountAsync()).Should().Be(broj);
        (await t.Db.Products.SingleAsync(p => p.Slug == "cvekla")).Mjeseci.Should().NotContain(5);
        (await t.Db.Producers.CountAsync(p => p.Name == "Voćnjak Đurić")).Should().Be(1);
    }
}

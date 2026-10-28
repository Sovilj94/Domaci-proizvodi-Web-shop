using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.Features.Korpice;
using EPiljara.Domain.Enums;
using EPiljara.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EPiljara.Application.Tests;

public class SeedSadrzajKorpicaTests
{
    private sealed class LazniHasher : IPasswordHasher
    {
        public string Hash(string password) => password;
        public bool Verify(string password, string hash) => password == hash;
    }

    private static readonly DateTime Sada = new(2026, 10, 28, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Objavljuje_tekucu_i_narednu_sedmicu_za_svaku_prodajnu_korpicu_i_ne_ponavlja()
    {
        using var t = new TestDb();
        await DbSeeder.SeedAsync(t.Db, new LazniHasher());
        var prodajne = await t.Db.Products.CountAsync(p => p.Vrsta == VrstaProizvoda.Korpica && p.IsAvailable);

        var prvi = await DbSeeder.SeedSadrzajKorpicaAsync(t.Db, new KorpiceOpcije(), Sada);
        var drugi = await DbSeeder.SeedSadrzajKorpicaAsync(t.Db, new KorpiceOpcije(), Sada);

        prvi.Should().Be(prodajne * 2);
        drugi.Should().Be(0);
        var sadrzaji = await t.Db.SedmicniSadrzaji.Include(s => s.Stavke).ToListAsync();
        sadrzaji.Should().HaveCount(prodajne * 2);
        sadrzaji.Should().OnlyContain(s => s.Status == StatusSadrzaja.Objavljen && s.Stavke.Count >= 5);
    }

    [Fact]
    public async Task Kupac_vidi_sadrzaj_korpice_umjesto_greske()
    {
        using var t = new TestDb();
        await DbSeeder.SeedAsync(t.Db, new LazniHasher());
        await DbSeeder.SeedSadrzajKorpicaAsync(t.Db, new KorpiceOpcije());
        var slug = (await t.Db.Products.FirstAsync(p => p.Vrsta == VrstaProizvoda.Korpica && p.IsAvailable)).Slug;

        var sadrzaj = await new GetSadrzajKorpiceHandler(t.Db, Options.Create(new KorpiceOpcije()))
            .Handle(new GetSadrzajKorpiceQuery(slug), default);

        sadrzaj.Stavke.Should().NotBeEmpty();
    }
}

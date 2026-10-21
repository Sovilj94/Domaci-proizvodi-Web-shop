using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using EPiljara.Application.Mapping;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Korpice;

/// <summary>
/// Admin uređuje korpicu jednog kupca (kupac je zvao: „bez luka, više jaja"). Za razliku od
/// kupčevog editora, admin zadaje i količine i nije vezan granicama budžeta ni rokom — on je
/// posljednja instanca; ekran mu pokazuje vrijednost naspram cilja i maržu. Dok narudžba nije
/// krenula u dostavu (Na čekanju, Potvrđena, Pakovanje).
/// </summary>
public static class AdminKorpicaNarudzbePravila
{
    public static readonly OrderStatus[] StatusiKojiDozvoljavaju =
        [OrderStatus.Pending, OrderStatus.Confirmed, OrderStatus.Packing];

    public static async Task<AdminKorpicaNarudzbeDto> SastaviAsync(IAppDbContext db, Guid orderItemId, CancellationToken ct)
    {
        var red = await db.OrderItems
            .Include(i => i.Order)
            .Include(i => i.KorpicaStavke)
            .FirstOrDefaultAsync(i => i.Id == orderItemId, ct)
            ?? throw new NotFoundException("Korpica nije pronađena.");
        var korpica = await MojaKorpicaPravila.UcitajKorpicuAsync(db, red, ct);
        var order = red.Order!;

        var sadrzaj = await db.SedmicniSadrzaji
            .Include(s => s.Stavke)
            .FirstOrDefaultAsync(s => s.KorpicaId == red.ProductId && s.Godina == order.Godina && s.Sedmica == order.Sedmica, ct);
        var nabavne = await AutoZamjena.NabavneAsync(db, red.KorpicaStavke, ct);

        var vrijednost = Math.Round(red.KorpicaStavke.Sum(s => s.Kolicina * s.JedinicnaCijena), 2);
        var nabavno = Math.Round(red.KorpicaStavke.Sum(s => s.Kolicina * nabavne.GetValueOrDefault(s.ProductId)), 2);
        var izbjegnuti = order.CustomerId is { } kupac
            ? await IzbjegnutiArtikliUpit.ZaKupcaAsync(db, kupac, ct)
            : [];

        return new AdminKorpicaNarudzbeDto
        {
            OrderItemId = red.Id,
            KodNarudzbe = order.Code,
            StatusNarudzbe = order.Status.ToString(),
            Kupac = order.CustomerName,
            Korpica = korpica.Name,
            Godina = order.Godina,
            Sedmica = order.Sedmica,
            BrojKutija = red.Quantity,
            CijenaKorpice = red.UnitPrice,
            Cilj = sadrzaj?.CiljnaVrijednost ?? 0,
            Vrijednost = vrijednost,
            NabavnaVrijednost = nabavno,
            Marza = nabavne.Count == 0 || red.UnitPrice <= 0 ? null : Math.Round((red.UnitPrice - nabavno) / red.UnitPrice, 4),
            MozeSeMijenjati = StatusiKojiDozvoljavaju.Contains(order.Status),
            Stavke = red.KorpicaStavke
                .OrderBy(s => s.ProductName)
                .Select(s => new AdminStavkaKorpiceNarudzbeDto
                {
                    ProductId = s.ProductId,
                    Naziv = s.ProductName,
                    Jedinica = s.Unit.ToApi(),
                    Kolicina = s.Kolicina,
                    JedinicnaCijena = s.JedinicnaCijena,
                    NabavnaCijena = nabavne.TryGetValue(s.ProductId, out var n) ? n : null,
                    Izmijenjeno = s.DodaoKupac,
                })
                .ToList(),
            Izbjegnuti = izbjegnuti,
        };
    }
}

public record GetAdminKorpicaNarudzbeQuery(Guid OrderItemId) : IRequest<AdminKorpicaNarudzbeDto>;

public class GetAdminKorpicaNarudzbeHandler(IAppDbContext db)
    : IRequestHandler<GetAdminKorpicaNarudzbeQuery, AdminKorpicaNarudzbeDto>
{
    public Task<AdminKorpicaNarudzbeDto> Handle(GetAdminKorpicaNarudzbeQuery q, CancellationToken ct) =>
        AdminKorpicaNarudzbePravila.SastaviAsync(db, q.OrderItemId, ct);
}

public record IzmijeniKorpicuNarudzbeCommand(Guid OrderItemId, IReadOnlyList<StavkaUnos> Stavke)
    : IRequest<AdminKorpicaNarudzbeDto>;

public class IzmijeniKorpicuNarudzbeValidator : AbstractValidator<IzmijeniKorpicuNarudzbeCommand>
{
    public IzmijeniKorpicuNarudzbeValidator()
    {
        RuleFor(x => x.Stavke).NotEmpty().WithMessage("Korpica ne može ostati prazna.");
        RuleForEach(x => x.Stavke).Must(s => s.Kolicina > 0).WithMessage("Količina mora biti veća od 0.");
        RuleFor(x => x.Stavke)
            .Must(l => l.Select(s => s.ProductId).Distinct().Count() == l.Count)
            .WithMessage("Isti artikal je naveden dvaput.");
    }
}

public class IzmijeniKorpicuNarudzbeHandler(IAppDbContext db)
    : IRequestHandler<IzmijeniKorpicuNarudzbeCommand, AdminKorpicaNarudzbeDto>
{
    public async Task<AdminKorpicaNarudzbeDto> Handle(IzmijeniKorpicuNarudzbeCommand cmd, CancellationToken ct)
    {
        var red = await db.OrderItems
            .Include(i => i.Order)
            .Include(i => i.KorpicaStavke)
            .FirstOrDefaultAsync(i => i.Id == cmd.OrderItemId, ct)
            ?? throw new NotFoundException("Korpica nije pronađena.");
        await MojaKorpicaPravila.UcitajKorpicuAsync(db, red, ct);
        var order = red.Order!;

        if (!AdminKorpicaNarudzbePravila.StatusiKojiDozvoljavaju.Contains(order.Status))
            throw new ConflictException(
                "Narudžba je već krenula u dostavu, sadržaj se više ne mijenja.", "korpica_u_dostavi");

        var sadrzaj = await db.SedmicniSadrzaji
            .Include(s => s.Stavke)
            .Include(s => s.Zamjene)
            .FirstOrDefaultAsync(s => s.KorpicaId == red.ProductId && s.Godina == order.Godina && s.Sedmica == order.Sedmica, ct);

        var idjevi = cmd.Stavke.Select(s => s.ProductId).ToList();
        var artikli = await db.Products.IgnoreQueryFilters()
            .Where(p => idjevi.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);
        var stare = red.KorpicaStavke.ToDictionary(s => s.ProductId);

        var nove = new List<StavkaKorpiceNarudzbe>();
        foreach (var u in cmd.Stavke)
        {
            stare.TryGetValue(u.ProductId, out var stara);
            artikli.TryGetValue(u.ProductId, out var p);
            if (stara is null && (p is null || p.IsDeleted))
                throw new DomainException("Artikal nije pronađen.", "artikal_nije_pronadjen");
            if (p is not null && p.Vrsta == VrstaProizvoda.Korpica)
                throw new DomainException($"{p.Name} je korpica — ne može u drugu korpicu.", "korpica_u_korpici");

            var jedinica = stara?.Unit ?? p!.Unit;
            if (jedinica == EPiljara.Domain.Enums.Unit.Kom && u.Kolicina != Math.Floor(u.Kolicina))
                throw new DomainException($"{stara?.ProductName ?? p!.Name} se daje na komad.", "kolicina_na_komad");

            // Cijena istim redom kao kupčeve izmjene: snimak na narudžbi, pa šablon, pa zamjena, pa katalog.
            var cijena = stara?.JedinicnaCijena
                         ?? sadrzaj?.Stavke.FirstOrDefault(s => s.ProductId == u.ProductId)?.JedinicnaCijena
                         ?? sadrzaj?.Zamjene.FirstOrDefault(z => z.ProductId == u.ProductId)?.JedinicnaCijena
                         ?? p!.Price;

            nove.Add(new StavkaKorpiceNarudzbe
            {
                OrderItemId = red.Id,
                ProductId = u.ProductId,
                ProductName = stara?.ProductName ?? p!.Name,
                Unit = jedinica,
                Kolicina = u.Kolicina,
                JedinicnaCijena = cijena,
                DodaoKupac = sadrzaj is null || sadrzaj.Stavke.All(s => s.ProductId != u.ProductId),
            });
        }

        db.StavkeKorpiceNarudzbe.RemoveRange(red.KorpicaStavke);
        db.StavkeKorpiceNarudzbe.AddRange(nove);
        await db.SaveChangesAsync(ct);

        return await AdminKorpicaNarudzbePravila.SastaviAsync(db, red.Id, ct);
    }
}

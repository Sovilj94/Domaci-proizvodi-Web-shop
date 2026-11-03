using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using EPiljara.Application.Features.Korpice;
using EPiljara.Domain.Common;
using MediatR;
using Microsoft.Extensions.Options;

namespace EPiljara.Application.Features.Admin.Poslovanje;

/// <summary>Analitika mjeseca. Bez parametara: tekući mjesec (po Sarajevu).</summary>
public record GetPoslovanjeQuery(int? Godina = null, int? Mjesec = null) : IRequest<PoslovanjeMjesecaDto>;

public class GetPoslovanjeHandler(IAppDbContext db, IOptions<KorpiceOpcije> opcije)
    : IRequestHandler<GetPoslovanjeQuery, PoslovanjeMjesecaDto>
{
    public async Task<PoslovanjeMjesecaDto> Handle(GetPoslovanjeQuery q, CancellationToken ct)
    {
        var sada = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, SedmicaHelper.Zona);
        var godina = q.Godina ?? sada.Year;
        var mjesec = q.Mjesec ?? sada.Month;
        if (mjesec is < 1 or > 12)
            throw new DomainException("Mjesec mora biti od 1 do 12.", "mjesec_neispravan");

        var (dan, _, _) = await SedmicaHelper.UcitajRokAsync(db, opcije.Value, ct);
        var (pg, pm) = PoslovanjeKalendar.Prethodni(godina, mjesec);
        var ove = PoslovanjeKalendar.SedmiceMjeseca(godina, mjesec, dan);
        var prosle = PoslovanjeKalendar.SedmiceMjeseca(pg, pm, dan);

        var narudzbe = await PoslovanjePodaci.NarudzbeAsync(db, [.. ove, .. prosle], ct);
        var uMjesecu = narudzbe.Where(o => ove.Contains((o.Godina, o.Sedmica))).ToList();
        var uProslom = narudzbe.Where(o => prosle.Contains((o.Godina, o.Sedmica))).ToList();
        var isporucene = PoslovanjeRacun.Isporucene(uMjesecu).ToList();
        var prosleIsporucene = PoslovanjeRacun.Isporucene(uProslom).ToList();

        var racun = new PoslovanjeRacun(await PoslovanjePodaci.KatalogAsync(db, ct));
        return new PoslovanjeMjesecaDto
        {
            Godina = godina,
            Mjesec = mjesec,
            Prihod = racun.Prihod(isporucene),
            Prethodni = racun.Prihod(prosleIsporucene),
        };
    }
}

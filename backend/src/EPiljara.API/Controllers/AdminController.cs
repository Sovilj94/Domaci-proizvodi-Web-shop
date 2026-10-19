using EPiljara.Application.Features.Novosti;
using EPiljara.Application.Features.Admin;
using EPiljara.Application.Features.Analize;
using EPiljara.Application.Features.Ocjene;
using EPiljara.Application.Features.Recepti;
using EPiljara.Application.Features.PromoKodovi;
using EPiljara.Application.Features.Utisci;
using EPiljara.Application.Features.Korpice;
using EPiljara.Application.Features.Partneri;
using EPiljara.Application.Features.Pretplate;
using EPiljara.Application.Features.Poeni;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EPiljara.API.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : ApiControllerBase
{
    [HttpGet("admin/dashboard")]
    public async Task<IActionResult> Dashboard([FromQuery] DateTime? date) =>
        Envelope(await Mediator.Send(new GetDashboardQuery(date)));

    /// <param name="unassigned">Kad je true, <paramref name="date"/> je dan DOSTAVE i vraćaju
    /// se samo narudžbe koje tog dana nisu ni na čijoj ruti.</param>
    [HttpGet("admin/orders")]
    public async Task<IActionResult> Orders(
        [FromQuery] string? status, [FromQuery] DateTime? date, [FromQuery] bool unassigned = false,
        [FromQuery] int? godina = null, [FromQuery] int? sedmica = null, [FromQuery] string? pretraga = null) =>
        EnvelopeList(await Mediator.Send(new GetAdminOrdersQuery(status, date, unassigned, godina, sedmica, pretraga)));

    [HttpPatch("admin/orders/{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateStatusRequest body) =>
        Envelope(await Mediator.Send(new UpdateOrderStatusCommand(id, body.Status)));

    [HttpGet("admin/products")]
    public async Task<IActionResult> Products() =>
        EnvelopeList(await Mediator.Send(new GetAdminProductsQuery()));

    [HttpPost("admin/products")]
    public async Task<IActionResult> CreateProduct([FromBody] ProductInput input) =>
        EnvelopeCreated(await Mediator.Send(new CreateProductCommand(input)));

    [HttpPut("admin/products/{id:guid}")]
    public async Task<IActionResult> UpdateProduct(Guid id, [FromBody] ProductInput input) =>
        Envelope(await Mediator.Send(new UpdateProductCommand(id, input)));

    [HttpPatch("admin/products/{id:guid}/availability")]
    public async Task<IActionResult> ToggleProduct(Guid id, [FromBody] SetAvailabilityRequest body) =>
        Envelope(await Mediator.Send(new SetProductAvailabilityCommand(id, body.IsAvailable)));

    [HttpDelete("admin/products/{id:guid}")]
    public async Task<IActionResult> DeleteProduct(Guid id) =>
        Envelope(await Mediator.Send(new DeleteProductCommand(id)));

    [HttpPost("admin/categories")]
    public async Task<IActionResult> CreateCategory([FromBody] CategoryInput input) =>
        EnvelopeCreated(await Mediator.Send(new CreateCategoryCommand(input)));

    [HttpPut("admin/categories/{id:guid}")]
    public async Task<IActionResult> UpdateCategory(Guid id, [FromBody] CategoryInput input) =>
        Envelope(await Mediator.Send(new UpdateCategoryCommand(id, input)));

    [HttpDelete("admin/categories/{id:guid}")]
    public async Task<IActionResult> DeleteCategory(Guid id) =>
        Envelope(await Mediator.Send(new DeleteCategoryCommand(id)));

    [HttpPost("admin/producers")]
    public async Task<IActionResult> CreateProducer([FromBody] ProducerInput input) =>
        EnvelopeCreated(await Mediator.Send(new CreateProducerCommand(input)));

    [HttpPut("admin/producers/{id:guid}")]
    public async Task<IActionResult> UpdateProducer(Guid id, [FromBody] ProducerInput input) =>
        Envelope(await Mediator.Send(new UpdateProducerCommand(id, input)));

    [HttpDelete("admin/producers/{id:guid}")]
    public async Task<IActionResult> DeleteProducer(Guid id) =>
        Envelope(await Mediator.Send(new DeleteProducerCommand(id)));

    [HttpGet("admin/drivers")]
    public async Task<IActionResult> Drivers() =>
        EnvelopeList(await Mediator.Send(new GetDriversQuery()));

    [HttpPost("admin/drivers")]
    public async Task<IActionResult> CreateDriver([FromBody] DriverInput input) =>
        EnvelopeCreated(await Mediator.Send(new CreateDriverCommand(input)));

    [HttpPut("admin/drivers/{id:guid}")]
    public async Task<IActionResult> UpdateDriver(Guid id, [FromBody] DriverInput input) =>
        Envelope(await Mediator.Send(new UpdateDriverCommand(id, input)));

    [HttpDelete("admin/drivers/{id:guid}")]
    public async Task<IActionResult> DeleteDriver(Guid id) =>
        Envelope(await Mediator.Send(new DeleteDriverCommand(id)));

    [HttpGet("admin/routes")]
    public async Task<IActionResult> Routes([FromQuery] string date) =>
        EnvelopeList(await Mediator.Send(new GetRoutesQuery(date)));

    [HttpPost("admin/routes")]
    public async Task<IActionResult> AssignRoute([FromBody] AssignRouteCommand command) =>
        EnvelopeCreated(await Mediator.Send(command));

    [HttpPost("admin/routes/{id:guid}/optimize")]
    public async Task<IActionResult> OptimizeRoute(Guid id) =>
        Envelope(await Mediator.Send(new OptimizeRouteCommand(id)));

    /* ---------- Magacin: polazište i krajnja tačka svake ture ---------- */

    [HttpGet("admin/warehouse")]
    public async Task<IActionResult> Warehouse() =>
        Envelope(await Mediator.Send(new GetWarehouseQuery()));

    [HttpPut("admin/warehouse")]
    public async Task<IActionResult> UpdateWarehouse([FromBody] UpdateWarehouseCommand command) =>
        Envelope(await Mediator.Send(command));

    /// <summary>Dodjeljuje ili prebacuje narudžbu vozaču za dati dan (dispečerski ekran).</summary>
    [HttpPost("admin/orders/{id:guid}/driver")]
    public async Task<IActionResult> AssignOrderDriver(Guid id, [FromBody] AssignDriverRequest body) =>
        EnvelopeList(await Mediator.Send(new AssignOrderDriverCommand(id, body.DriverId, body.Date)));

    /// <summary>Dodjeljuje više narudžbi (cijelo naselje) jednom vozaču — sve ili ništa.</summary>
    [HttpPost("admin/orders/assign-bulk")]
    public async Task<IActionResult> AssignOrdersBulk([FromBody] AssignOrdersBulkRequest body) =>
        EnvelopeList(await Mediator.Send(
            new AssignOrdersBulkCommand(body.OrderIds, body.DriverId, body.Date)));

    /// <summary>Skida narudžbu sa rute; vraća je u „Nedodijeljeno".</summary>
    [HttpDelete("admin/orders/{id:guid}/driver")]
    public async Task<IActionResult> RemoveOrderDriver(Guid id, [FromQuery] string date)
    {
        await Mediator.Send(new RemoveOrderDriverCommand(id, date));
        return Envelope(new { message = "Narudžba je skinuta sa rute." });
    }

    [HttpPost("admin/orders/geocode-missing")]
    public async Task<IActionResult> GeocodeMissing() =>
        Envelope(await Mediator.Send(new GeocodeMissingCommand()));

    [HttpPut("admin/orders/{id:guid}/coordinates")]
    public async Task<IActionResult> SetCoordinates(Guid id, [FromBody] SetCoordinatesRequest body)
    {
        await Mediator.Send(new SetOrderCoordinatesCommand(id, body.Latitude, body.Longitude));
        return Envelope(new { message = "Koordinate su sačuvane." });
    }

    /* ---------- Korpice: pregled sedmice, izmjena nacrta, pakerska lista ---------- */

    [HttpGet("admin/korpice/sadrzaj")]
    public async Task<IActionResult> SadrzajPregled([FromQuery] int godina, [FromQuery] int sedmica) =>
        EnvelopeList(await Mediator.Send(new GetSadrzajPregledQuery(godina, sedmica)));

    [HttpPost("admin/korpice/sadrzaj/generisi")]
    public async Task<IActionResult> GenerisiSadrzaj([FromBody] GenerisiSadrzajRequest body) =>
        EnvelopeList(await Mediator.Send(
            new GenerisiSedmicniSadrzajCommand(body.Godina, body.Sedmica, body.KorpicaId)));

    [HttpGet("admin/korpice/sadrzaj/{id:guid}")]
    public async Task<IActionResult> SadrzajZaIzmjenu(Guid id) =>
        Envelope(await Mediator.Send(new GetSadrzajZaIzmjenuQuery(id)));

    [HttpPut("admin/korpice/sadrzaj/{id:guid}")]
    public async Task<IActionResult> IzmijeniNacrt(Guid id, [FromBody] IzmijeniNacrtRequest body) =>
        Envelope(await Mediator.Send(new IzmijeniNacrtCommand(id, body.Stavke)));

    [HttpPost("admin/korpice/sadrzaj/{id:guid}/objavi")]
    public async Task<IActionResult> ObjaviSadrzaj(Guid id) =>
        Envelope(await Mediator.Send(new ObjaviSadrzajCommand(id)));

    [HttpGet("admin/korpice/pakerska-lista")]
    public async Task<IActionResult> PakerskaLista([FromQuery] int godina, [FromQuery] int sedmica) =>
        Envelope(await Mediator.Send(new GetPakerskaListaQuery(godina, sedmica)));

    /// <summary>
    /// Koliko je korpica obećano za narednu sedmicu, prije nego što ijedna narudžba nastane —
    /// pitanje koje nabavka do P3 nije mogla postaviti.
    /// </summary>
    [HttpGet("admin/pretplate")]
    public async Task<IActionResult> Pretplate([FromQuery] string? status) =>
        Envelope(await Mediator.Send(new GetAdminPretplateQuery(status)));

    /// <summary>Otkaz pretplate od strane podrške — radi i dok traje obaveza plana (R2).</summary>
    [HttpDelete("admin/pretplate/{id:guid}")]
    public async Task<IActionResult> OtkaziPretplatu(Guid id)
    {
        await Mediator.Send(new AdminOtkaziPretplatuCommand(id));
        return NoContent();
    }

    [HttpGet("admin/utisci")]
    public async Task<IActionResult> Utisci() =>
        EnvelopeList(await Mediator.Send(new GetAdminUtisciQuery()));

    [HttpPost("admin/utisci")]
    public async Task<IActionResult> CreateUtisak([FromBody] UtisakInput input) =>
        EnvelopeCreated(await Mediator.Send(new CreateUtisakCommand(input)));

    [HttpPut("admin/utisci/{id:guid}")]
    public async Task<IActionResult> UpdateUtisak(Guid id, [FromBody] UtisakInput input) =>
        Envelope(await Mediator.Send(new UpdateUtisakCommand(id, input)));

    [HttpDelete("admin/utisci/{id:guid}")]
    public async Task<IActionResult> DeleteUtisak(Guid id) =>
        Envelope(await Mediator.Send(new DeleteUtisakCommand(id)));

    [HttpGet("admin/novosti")]
    public async Task<IActionResult> Novosti() =>
        EnvelopeList(await Mediator.Send(new GetAdminNovostiQuery()));

    [HttpGet("admin/promo-kodovi")]
    public async Task<IActionResult> PromoKodovi() =>
        EnvelopeList(await Mediator.Send(new GetAdminPromoKodoviQuery()));

    [HttpPost("admin/promo-kodovi")]
    public async Task<IActionResult> CreatePromoKod([FromBody] PromoKodInput input) =>
        EnvelopeCreated(await Mediator.Send(new CreatePromoKodCommand(input)));

    [HttpPut("admin/promo-kodovi/{id:guid}")]
    public async Task<IActionResult> UpdatePromoKod(Guid id, [FromBody] PromoKodInput input) =>
        Envelope(await Mediator.Send(new UpdatePromoKodCommand(id, input)));

    [HttpDelete("admin/promo-kodovi/{id:guid}")]
    public async Task<IActionResult> DeletePromoKod(Guid id) =>
        Envelope(await Mediator.Send(new DeletePromoKodCommand(id)));

    [HttpGet("admin/ocjene")]
    public async Task<IActionResult> Ocjene([FromQuery] int? ocjena) =>
        Envelope(await Mediator.Send(new GetAdminOcjeneQuery(ocjena)));

    [HttpPost("admin/ocjene/{id:guid}/utisak")]
    public async Task<IActionResult> NapraviUtisakIzOcjene(Guid id) =>
        EnvelopeCreated(await Mediator.Send(new NapraviUtisakIzOcjeneCommand(id)));

    /* ---------- Pakovanje, zone i termini, kupci, sadržaj sajta ---------- */

    [HttpGet("admin/pakovanje")]
    public async Task<IActionResult> Pakovanje([FromQuery] int godina, [FromQuery] int sedmica) =>
        Envelope(await Mediator.Send(new GetPakovanjeQuery(godina, sedmica)));

    [HttpGet("admin/zone")]
    public async Task<IActionResult> Zone() => EnvelopeList(await Mediator.Send(new GetAdminZoneQuery()));

    [HttpPost("admin/zone")]
    public async Task<IActionResult> CreateZona([FromBody] ZonaInput input) =>
        EnvelopeCreated(await Mediator.Send(new CreateZonaCommand(input)));

    [HttpPut("admin/zone/{id:guid}")]
    public async Task<IActionResult> UpdateZona(Guid id, [FromBody] ZonaInput input) =>
        Envelope(await Mediator.Send(new UpdateZonaCommand(id, input)));

    [HttpGet("admin/termini")]
    public async Task<IActionResult> Termini() => EnvelopeList(await Mediator.Send(new GetAdminTerminiQuery()));

    [HttpPost("admin/termini")]
    public async Task<IActionResult> CreateTermin([FromBody] TerminInput input) =>
        EnvelopeCreated(await Mediator.Send(new CreateTerminCommand(input)));

    [HttpPut("admin/termini/{id:guid}")]
    public async Task<IActionResult> UpdateTermin(Guid id, [FromBody] TerminInput input) =>
        Envelope(await Mediator.Send(new UpdateTerminCommand(id, input)));

    [HttpDelete("admin/termini/{id:guid}")]
    public async Task<IActionResult> DeleteTermin(Guid id) =>
        Envelope(await Mediator.Send(new DeleteTerminCommand(id)));

    [HttpGet("admin/kupci")]
    public async Task<IActionResult> Kupci([FromQuery] string? pretraga) =>
        EnvelopeList(await Mediator.Send(new GetAdminKupciQuery(pretraga)));

    [HttpGet("admin/kupci/{id:guid}")]
    public async Task<IActionResult> Kupac(Guid id) => Envelope(await Mediator.Send(new GetAdminKupacQuery(id)));

    [HttpPut("admin/sadrzaj-sajta")]
    public async Task<IActionResult> SacuvajSadrzajSajta([FromBody] SadrzajSajtaRequest body) =>
        Envelope(await Mediator.Send(new SacuvajSadrzajSajtaCommand(
            body.Telefon, body.Email, body.Adresa, body.Podrucje, body.Pitanja)));

    [HttpGet("admin/poeni")]
    public async Task<IActionResult> PoeniKupci([FromQuery] string? pretraga) =>
        EnvelopeList(await Mediator.Send(new GetAdminPoeniKupciQuery(pretraga)));

    [HttpGet("admin/poeni/{userId:guid}")]
    public async Task<IActionResult> PoeniKupca(Guid userId) =>
        Envelope(await Mediator.Send(new GetAdminPoeniKupcaQuery(userId)));

    [HttpPost("admin/poeni/{userId:guid}/korekcija")]
    public async Task<IActionResult> KorigujPoene(Guid userId, [FromBody] KorekcijaPoenaRequest body) =>
        Envelope(await Mediator.Send(new KorigujPoeneCommand(userId, body.Iznos, body.Razlog)));

    [HttpGet("admin/recepti")]
    public async Task<IActionResult> Recepti() =>
        EnvelopeList(await Mediator.Send(new GetAdminRecepteQuery()));

    [HttpPost("admin/recepti")]
    public async Task<IActionResult> CreateRecept([FromBody] ReceptInput input) =>
        EnvelopeCreated(await Mediator.Send(new CreateReceptCommand(input)));

    [HttpPut("admin/recepti/{id:guid}")]
    public async Task<IActionResult> UpdateRecept(Guid id, [FromBody] ReceptInput input) =>
        Envelope(await Mediator.Send(new UpdateReceptCommand(id, input)));

    [HttpDelete("admin/recepti/{id:guid}")]
    public async Task<IActionResult> DeleteRecept(Guid id) =>
        Envelope(await Mediator.Send(new DeleteReceptCommand(id)));

    [HttpGet("admin/analize")]
    public async Task<IActionResult> Analize() =>
        EnvelopeList(await Mediator.Send(new GetAdminAnalizeQuery()));

    [HttpPost("admin/analize")]
    public async Task<IActionResult> CreateAnaliza([FromBody] AnalizaInput input) =>
        EnvelopeCreated(await Mediator.Send(new CreateAnalizaCommand(input)));

    [HttpPut("admin/analize/{id:guid}")]
    public async Task<IActionResult> UpdateAnaliza(Guid id, [FromBody] AnalizaInput input) =>
        Envelope(await Mediator.Send(new UpdateAnalizaCommand(id, input)));

    [HttpGet("admin/partneri")]
    public async Task<IActionResult> Partneri([FromQuery] string? status) =>
        EnvelopeList(await Mediator.Send(new GetAdminPartneriQuery(status)));

    [HttpPatch("admin/partneri/{id:guid}")]
    public async Task<IActionResult> ObradiPartnera(Guid id, [FromBody] ObradaInput input) =>
        Envelope(await Mediator.Send(new ObradiPrijavuCommand(id, input)));

    [HttpDelete("admin/analize/{id:guid}")]
    public async Task<IActionResult> DeleteAnaliza(Guid id) =>
        Envelope(await Mediator.Send(new DeleteAnalizaCommand(id)));
}

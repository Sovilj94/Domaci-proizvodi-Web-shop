using EPiljara.Application.Features.Novosti;
using EPiljara.Application.Features.Admin;
using EPiljara.Application.Features.Admin.Poslovanje;
using EPiljara.Application.Features.Admin.Troskovi;
using EPiljara.Application.Features.Fiskalizacija;
using EPiljara.Application.Features.Voznje;
using EPiljara.Application.Features.Mejlovi;
using EPiljara.Application.Features.Analize;
using EPiljara.Application.Features.Ocjene;
using EPiljara.Application.Features.Recepti;
using EPiljara.Application.Features.PromoKodovi;
using EPiljara.Application.Features.Utisci;
using EPiljara.Application.Features.Korpice;
using EPiljara.Application.Features.Naselja;
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

    [HttpGet("admin/poslovanje")]
    public async Task<IActionResult> Poslovanje([FromQuery] int? godina, [FromQuery] int? mjesec) =>
        Envelope(await Mediator.Send(new GetPoslovanjeQuery(godina, mjesec)));

    /// <summary>Plan poslovanja: mjesec sa N narudžbi po današnjim cijenama, nabavnim i troškovima.</summary>
    [HttpGet("admin/poslovanje/plan")]
    public async Task<IActionResult> PlanPoslovanja([FromQuery] int narudzbi = 200, [FromQuery] int? godina = null, [FromQuery] int? mjesec = null, [FromQuery] decimal dodatno = 0m) =>
        Envelope(await Mediator.Send(new GetPlanPoslovanjaQuery(narudzbi, godina, mjesec, dodatno)));

    [HttpGet("admin/poslovanje/godina")]
    public async Task<IActionResult> PoslovanjeGodina([FromQuery] int? godina) =>
        Envelope(await Mediator.Send(new GetPoslovanjeGodinaQuery(godina)));

    [HttpGet("admin/fiskalni-racuni")]
    public async Task<IActionResult> FiskalniRacuni([FromQuery] DateOnly? od, [FromQuery(Name = "do")] DateOnly? doDana) =>
        Envelope(await Mediator.Send(new GetFiskalniRacuniQuery(od, doDana)));

    [HttpGet("admin/fiskalni-racuni/{id:guid}")]
    public async Task<IActionResult> FiskalniRacun(Guid id) =>
        Envelope(await Mediator.Send(new GetFiskalniRacunQuery(id)));

    [HttpPost("admin/fiskalni-racuni/{id:guid}/ponovi")]
    public async Task<IActionResult> PonoviFiskalniRacun(Guid id) =>
        Envelope(await Mediator.Send(new PonoviFiskalniRacunCommand(id)));

    [HttpPost("admin/orders/{orderId:guid}/refundacija")]
    public async Task<IActionResult> RefundirajNarudzbu(Guid orderId) =>
        EnvelopeCreated(await Mediator.Send(new RefundirajFiskalniRacunCommand(orderId)));

    [HttpGet("admin/fiskalna-podesavanja")]
    public async Task<IActionResult> FiskalnaPodesavanja() =>
        Envelope(await Mediator.Send(new GetFiskalnaPodesavanjaQuery()));

    [HttpPut("admin/fiskalna-podesavanja")]
    public async Task<IActionResult> SacuvajFiskalnaPodesavanja([FromBody] SacuvajFiskalnaPodesavanjaCommand c) =>
        Envelope(await Mediator.Send(c));

    /* ---------- Mejlovi: red, obavještenja, ručno slanje ---------- */

    [HttpGet("admin/mejlovi")]
    public async Task<IActionResult> Mejlovi([FromQuery] string? status, [FromQuery] string? vrsta, [FromQuery] string? trazi) =>
        Envelope(await Mediator.Send(new GetEmailoviQuery(status, vrsta, trazi)));

    [HttpGet("admin/mejlovi/{id:guid}")]
    public async Task<IActionResult> Mejl(Guid id) => Envelope(await Mediator.Send(new GetEmailQuery(id)));

    [HttpPost("admin/mejlovi/{id:guid}/ponovi")]
    public async Task<IActionResult> PonoviMejl(Guid id) => Envelope(await Mediator.Send(new PonoviEmailCommand(id)));

    [HttpPost("admin/mejlovi/probni")]
    public async Task<IActionResult> ProbniMejl([FromBody] PosaljiProbniEmailCommand c)
    {
        await Mediator.Send(c);
        return Envelope(new { message = "Probni mejl je u redu za slanje." });
    }

    [HttpGet("admin/obavjestenja")]
    public async Task<IActionResult> Obavjestenja() => Envelope(await Mediator.Send(new GetObavjestenjaPodesavanjaQuery()));

    [HttpPut("admin/obavjestenja")]
    public async Task<IActionResult> SacuvajObavjestenja([FromBody] SacuvajObavjestenjaPodesavanjaCommand c) =>
        Envelope(await Mediator.Send(c));

    [HttpPost("admin/mejlovi/kampanja/pregled")]
    public async Task<IActionResult> PregledKampanje([FromBody] PregledKampanjeQuery q) => Envelope(await Mediator.Send(q));

    [HttpPost("admin/mejlovi/kampanja")]
    public async Task<IActionResult> PosaljiKampanju([FromBody] PosaljiKampanjuCommand c) => EnvelopeCreated(await Mediator.Send(c));

    [HttpGet("admin/mejlovi/kampanje")]
    public async Task<IActionResult> Kampanje() => EnvelopeList(await Mediator.Send(new GetKampanjeQuery()));

    /// <summary>Procjena dnevne ture i goriva po vozaču za dan (zastarjele se preračunaju).</summary>
    [HttpGet("admin/voznje")]
    public async Task<IActionResult> Voznje([FromQuery] DateOnly datum) =>
        EnvelopeList(await Mediator.Send(new GetVoznjeDanaQuery(datum)));

    [HttpGet("admin/voznje/mjesec")]
    public async Task<IActionResult> VoznjeMjeseca([FromQuery] int godina, [FromQuery] int mjesec) =>
        EnvelopeList(await Mediator.Send(new GetVoznjeMjesecaQuery(godina, mjesec)));

    [HttpPut("admin/voznje/{id:guid}/kilometraza")]
    public async Task<IActionResult> KilometrazaVoznje(Guid id, [FromBody] KilometrazaUnos body) =>
        Envelope(await Mediator.Send(new SacuvajKilometrazuVoznjeCommand(id, body.Pocetak, body.Kraj)));

    [HttpPut("admin/voznje/{id:guid}/gorivo")]
    public async Task<IActionResult> GorivoVoznje(Guid id, [FromBody] GorivoUnos body) =>
        Envelope(await Mediator.Send(new SacuvajGorivoVoznjeCommand(id, body.Iznos, body.Litara, body.Napomena)));

    /// <summary>Koliko gotovine koji vozač treba da preda po danu i ko još nije razdužen.</summary>
    [HttpGet("admin/razduzenje")]
    public async Task<IActionResult> Razduzenje([FromQuery] DateOnly od, [FromQuery] DateOnly @do) =>
        Envelope(await Mediator.Send(new GetRazduzenjeQuery(od, @do)));

    /// <summary>Evidencija predane gotovine za vozača i dan; iznos null poništava.</summary>
    [HttpPut("admin/razduzenje")]
    public async Task<IActionResult> EvidentirajPredaju([FromBody] PredajaUnos body) =>
        Envelope(await Mediator.Send(new EvidentirajPredajuCommand(body.DriverId, body.Datum, body.Iznos)));

    /// <summary>„Predao sve": svi nerazduženi dani vozača do datuma postaju predati u punom iznosu.</summary>
    [HttpPost("admin/razduzenje/sve")]
    public async Task<IActionResult> RazduziSve([FromBody] RazduziSveUnos body) =>
        Envelope(await Mediator.Send(new RazduziSveCommand(body.DriverId, body.DoDatuma)));

    [HttpGet("admin/troskovi")]
    public async Task<IActionResult> Troskovi([FromQuery] int godina, [FromQuery] int mjesec) =>
        Envelope(await Mediator.Send(new GetTroskoviQuery(godina, mjesec)));

    [HttpPost("admin/troskovi")]
    public async Task<IActionResult> DodajTrosak([FromBody] TrosakInput i) =>
        EnvelopeCreated(await Mediator.Send(new SacuvajTrosakCommand(null, i.Datum, i.VrstaId, i.Iznos, i.Opis, i.RacunUrl)));

    [HttpPut("admin/troskovi/{id:guid}")]
    public async Task<IActionResult> IzmijeniTrosak(Guid id, [FromBody] TrosakInput i) =>
        Envelope(await Mediator.Send(new SacuvajTrosakCommand(id, i.Datum, i.VrstaId, i.Iznos, i.Opis, i.RacunUrl)));

    [HttpDelete("admin/troskovi/{id:guid}")]
    public async Task<IActionResult> ObrisiTrosak(Guid id) =>
        Envelope(await Mediator.Send(new ObrisiTrosakCommand(id)));

    [HttpPost("admin/troskovi/vrste")]
    public async Task<IActionResult> DodajVrstuTroska([FromBody] VrstaTroskaInput i) =>
        EnvelopeCreated(await Mediator.Send(new SacuvajVrstuTroskaCommand(null, i.Naziv, i.JeGorivo, i.Redoslijed, i.Aktivna)));

    [HttpPut("admin/troskovi/vrste/{id:guid}")]
    public async Task<IActionResult> IzmijeniVrstuTroska(Guid id, [FromBody] VrstaTroskaInput i) =>
        Envelope(await Mediator.Send(new SacuvajVrstuTroskaCommand(id, i.Naziv, i.JeGorivo, i.Redoslijed, i.Aktivna)));

    [HttpDelete("admin/troskovi/vrste/{id:guid}")]
    public async Task<IActionResult> ObrisiVrstuTroska(Guid id) =>
        Envelope(await Mediator.Send(new ObrisiVrstuTroskaCommand(id)));

    [HttpPost("admin/troskovi/ponavljajuci")]
    public async Task<IActionResult> DodajPonavljajuciTrosak([FromBody] DodajPonavljajuciTrosakCommand c) =>
        EnvelopeCreated(await Mediator.Send(c));

    [HttpPost("admin/troskovi/ponavljajuci/{id:guid}/zaustavi")]
    public async Task<IActionResult> ZaustaviPonavljajuciTrosak(Guid id, [FromBody] MjesecInput m) =>
        Envelope(await Mediator.Send(new ZaustaviPonavljajuciTrosakCommand(id, m.Godina, m.Mjesec)));

    [HttpDelete("admin/troskovi/ponavljajuci/{id:guid}")]
    public async Task<IActionResult> ObrisiPonavljajuciTrosak(Guid id) =>
        Envelope(await Mediator.Send(new ObrisiPonavljajuciTrosakCommand(id)));

    [HttpPost("admin/troskovi/gorivo")]
    public async Task<IActionResult> DodajCijenuGoriva([FromBody] DodajCijenuGorivaCommand c) =>
        EnvelopeCreated(await Mediator.Send(c));

    [HttpDelete("admin/troskovi/gorivo/{id:guid}")]
    public async Task<IActionResult> ObrisiCijenuGoriva(Guid id) =>
        Envelope(await Mediator.Send(new ObrisiCijenuGorivaCommand(id)));

    /// <param name="unassigned">Kad je true, <paramref name="date"/> je dan DOSTAVE i vraćaju
    /// se samo narudžbe koje tog dana nisu ni na čijoj ruti.</param>
    [HttpGet("admin/zadaci")]
    public async Task<IActionResult> Zadaci() =>
        Envelope(await Mediator.Send(new GetAdminZadaciQuery()));

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

    /// <summary>Kako bi grupna dodjela podijelila narudžbe na rute („Borik 1: 8 · Borik 2: 7") — bez upisa.</summary>
    [HttpPost("admin/orders/assign-bulk/pregled")]
    public async Task<IActionResult> PregledPodjele([FromBody] AssignOrdersBulkRequest body) =>
        EnvelopeList(await Mediator.Send(new PregledPodjeleQuery(body.OrderIds, body.DriverId, body.Date)));

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
            new GenerisiSedmicniSadrzajCommand(body.Godina, body.Sedmica, body.KorpicaId, body.Ponovo ?? false)));

    [HttpGet("admin/korpice/sadrzaj/{id:guid}")]
    public async Task<IActionResult> SadrzajZaIzmjenu(Guid id) =>
        Envelope(await Mediator.Send(new GetSadrzajZaIzmjenuQuery(id)));

    [HttpPut("admin/korpice/sadrzaj/{id:guid}")]
    public async Task<IActionResult> IzmijeniNacrt(Guid id, [FromBody] IzmijeniNacrtRequest body) =>
        Envelope(await Mediator.Send(new IzmijeniNacrtCommand(id, body.Stavke, body.Zamjene)));

    [HttpPost("admin/korpice/sadrzaj/{id:guid}/objavi")]
    public async Task<IActionResult> ObjaviSadrzaj(Guid id) =>
        Envelope(await Mediator.Send(new ObjaviSadrzajCommand(id)));

    /// <summary>Sezonski kalendar: kulture vrste korpice sa mjesecima (puni „Šta dobijaš" i generator).</summary>
    [HttpGet("admin/korpice/kalendar")]
    public async Task<IActionResult> KalendarKorpica([FromQuery] string? tip) =>
        EnvelopeList(await Mediator.Send(new GetKalendarKorpicaQuery(tip)));

    [HttpPut("admin/korpice/kalendar/{productId:guid}")]
    public async Task<IActionResult> PostaviKalendar(Guid productId, [FromBody] PostaviKalendarRequest body) =>
        Envelope(await Mediator.Send(new PostaviKalendarCommand(productId, body.UKorpici, body.Mjeseci ?? [], body.MjeseciGlavni, body.Opciono)));

    /// <summary>Obračun cijene korpica: tipičan sadržaj mjeseca, najniža cijena za maržu i najviša da budemo povoljniji od piljare.</summary>
    [HttpGet("admin/korpice/obracun")]
    public async Task<IActionResult> ObracunCijena([FromQuery] decimal marza, [FromQuery] decimal povoljnije, [FromQuery] int? mjesec) =>
        Envelope(await Mediator.Send(new GetObracunCijenaKorpicaQuery(marza, povoljnije, mjesec)));

    [HttpPut("admin/korpice/{id:guid}/cijena")]
    public async Task<IActionResult> PostaviCijenuKorpice(Guid id, [FromBody] PostaviCijenuKorpiceRequest body) =>
        Envelope(await Mediator.Send(new PostaviCijenuKorpiceCommand(id, body.Cijena)));

    /// <summary>Postavke korpica: po vrsti kategorije, cijene i ponuda veličina.</summary>
    [HttpGet("admin/korpice/postavke")]
    public async Task<IActionResult> PostavkeKorpica() =>
        Envelope(await Mediator.Send(new GetPostavkeKorpicaQuery()));

    [HttpPut("admin/korpice/postavke/{tip}")]
    public async Task<IActionResult> SacuvajTipKorpice(string tip, [FromBody] SacuvajTipKorpiceRequest body) =>
        Envelope(await Mediator.Send(new SacuvajTipKorpiceCommand(tip, body.KategorijeIds ?? [], body.Korpice ?? [])));

    /// <summary>Artikli u korpicama: smije li u korpicu, prioritet i zadate količine — više odjednom.</summary>
    [HttpPut("admin/korpice/postavke-artikli")]
    public async Task<IActionResult> SacuvajArtikleKorpica([FromBody] SacuvajArtikleKorpicaRequest body) =>
        Envelope(await Mediator.Send(new SacuvajArtikleKorpicaCommand(body.Artikli ?? [])));

    /// <summary>Korpica jednog kupca u narudžbi — admin je uređuje sa količinama (kupac je zvao).</summary>
    [HttpGet("admin/narudzbe/korpica/{orderItemId:guid}")]
    public async Task<IActionResult> KorpicaNarudzbe(Guid orderItemId) =>
        Envelope(await Mediator.Send(new GetAdminKorpicaNarudzbeQuery(orderItemId)));

    [HttpPut("admin/narudzbe/korpica/{orderItemId:guid}")]
    public async Task<IActionResult> IzmijeniKorpicuNarudzbe(Guid orderItemId, [FromBody] IzmijeniKorpicuNarudzbeRequest body) =>
        Envelope(await Mediator.Send(new IzmijeniKorpicuNarudzbeCommand(orderItemId, body.Stavke)));

    /// <summary>Bez godine i sedmice vraća zadnju zaključenu — onu za koju se sad nabavlja.</summary>
    [HttpGet("admin/korpice/pakerska-lista")]
    public async Task<IActionResult> PakerskaLista([FromQuery] int? godina, [FromQuery] int? sedmica) =>
        Envelope(await Mediator.Send(new GetPakerskaListaQuery(godina, sedmica)));

    /// <summary>Korpice na čekanju po sedmici i koliko ih stanje dozvoljava da se potvrdi.</summary>
    [HttpGet("admin/korpice/potvrdjivanje")]
    public async Task<IActionResult> Potvrdjivanje() =>
        Envelope(await Mediator.Send(new GetPotvrdjivanjeQuery()));

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

    /// <summary>Poslovna pravila (marža, popusti, poeni, prag „pri kraju“) — jedan red u bazi.</summary>
    [HttpGet("admin/podesavanja")]
    public async Task<IActionResult> Podesavanja() => Envelope(await Mediator.Send(new GetPodesavanjaQuery()));

    [HttpPut("admin/podesavanja")]
    public async Task<IActionResult> SacuvajPodesavanja([FromBody] PodesavanjaDto body) =>
        Envelope(await Mediator.Send(new SacuvajPodesavanjaCommand(body)));

    /// <summary>Blokovi jedne sekcije sajta (i neobjavljeni).</summary>
    [HttpGet("admin/blokovi/{sekcija}")]
    public async Task<IActionResult> Blokovi(string sekcija) =>
        EnvelopeList(await Mediator.Send(new GetAdminBlokoviQuery(sekcija)));

    /// <summary>Cijela sekcija odjednom — redoslijed je redoslijed liste.</summary>
    [HttpPut("admin/blokovi/{sekcija}")]
    public async Task<IActionResult> SacuvajBlokove(string sekcija, [FromBody] SacuvajBlokoveRequest body) =>
        EnvelopeList(await Mediator.Send(new SacuvajSekcijuCommand(sekcija, body.Blokovi ?? [])));

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

    /* Naselja (zamjenjuju zone): granica, centar, aktivnost. Vidi AdminNaselja. */

    [HttpGet("admin/naselja")]
    public async Task<IActionResult> Naselja() => EnvelopeList(await Mediator.Send(new GetAdminNaseljaQuery()));

    /// <summary>Novo naselje (Id = null) ili izmjena. Kad izmjena pomjera naselje postojećim
    /// zapisima a PotvrdiUticaj = false, ništa se ne snima i vraća se uticaj (sacuvano = false).</summary>
    [HttpPost("admin/naselja")]
    public async Task<IActionResult> SacuvajNaselje([FromBody] SacuvajNaseljeCommand cmd) =>
        Envelope(await Mediator.Send(cmd));

    [HttpDelete("admin/naselja/{id:guid}")]
    public async Task<IActionResult> ObrisiNaselje(Guid id)
    {
        await Mediator.Send(new ObrisiNaseljeCommand(id));
        return NoContent();
    }

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

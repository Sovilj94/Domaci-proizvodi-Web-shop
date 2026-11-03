using EPiljara.Application.Features.Korpice;

namespace EPiljara.API.Controllers;

public record GenerisiSadrzajRequest(int Godina, int Sedmica, Guid? KorpicaId, bool? Ponovo);

/// <summary>Red sezonskog kalendara: da li kultura ide u korpicu i u kojim mjesecima (prazno = cijele godine).</summary>
public record PostaviKalendarRequest(bool UKorpici, IReadOnlyList<int>? Mjeseci);

public record IzmijeniNacrtRequest(IReadOnlyList<StavkaUnos> Stavke, IReadOnlyList<Guid>? Zamjene);
public record IzmijeniKorpicuNarudzbeRequest(IReadOnlyList<StavkaUnos> Stavke);

public record SacuvajTipKorpiceRequest(IReadOnlyList<Guid>? KategorijeIds, IReadOnlyList<KorpicaUnos>? Korpice);

public record SacuvajArtikleKorpicaRequest(IReadOnlyList<ArtikalKorpiceUnos>? Artikli);

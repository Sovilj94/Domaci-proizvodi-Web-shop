using EPiljara.Application.Features.Korpice;

namespace EPiljara.API.Controllers;

public record GenerisiSadrzajRequest(int Godina, int Sedmica, Guid? KorpicaId, bool? Ponovo);
public record IzmijeniNacrtRequest(IReadOnlyList<StavkaUnos> Stavke, IReadOnlyList<Guid>? Zamjene);
public record IzmijeniKorpicuNarudzbeRequest(IReadOnlyList<StavkaUnos> Stavke);

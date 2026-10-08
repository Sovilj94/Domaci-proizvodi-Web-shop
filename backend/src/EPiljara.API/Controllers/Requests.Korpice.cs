using EPiljara.Application.Features.Korpice;

namespace EPiljara.API.Controllers;

public record GenerisiSadrzajRequest(int Godina, int Sedmica, Guid? KorpicaId);
public record IzmijeniNacrtRequest(IReadOnlyList<StavkaUnos> Stavke);

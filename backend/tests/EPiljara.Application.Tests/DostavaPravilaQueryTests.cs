using EPiljara.Application.Features.Delivery;

namespace EPiljara.Application.Tests;

public class DostavaPravilaQueryTests
{
    [Fact]
    public async Task Vraca_pravila_dostave_iz_podesavanja()
    {
        var dto = await new GetDostavaPravilaHandler(TestOpcije.DostavaO(minimalna: 15m))
            .Handle(new GetDostavaPravilaQuery(), default);

        Assert.Equal(new DostavaPravilaDto(0m, 5m, 15m), dto);
    }
}

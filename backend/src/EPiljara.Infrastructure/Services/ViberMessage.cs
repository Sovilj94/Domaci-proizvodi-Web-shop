using System.Globalization;
using System.Text;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;

namespace EPiljara.Infrastructure.Services;

/// <summary>Short plain-text order confirmation for Viber (no rich formatting).</summary>
public static class ViberMessage
{
    private static string Money(decimal v) =>
        "KM " + v.ToString("0.00", CultureInfo.InvariantCulture).Replace('.', ',');

    public static string Build(Order order)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"E piljara — narudžba {order.Code} je zaprimljena. Hvala!");
        sb.AppendLine();
        foreach (var it in order.Items)
            sb.AppendLine(
                $"• {it.Quantity.ToString("0.##", CultureInfo.InvariantCulture)} {it.Unit.ToApi()} {it.ProductName}");
        sb.AppendLine();
        if (order.Popust > 0)
            sb.AppendLine(
                $"Popust{(string.IsNullOrWhiteSpace(order.PromoKod) ? "" : $" ({order.PromoKod})")}: −{Money(order.Popust)}");
        sb.AppendLine($"Ukupno: {Money(order.TotalAmount)} (plaćanje pouzećem)");
        sb.AppendLine($"Dostava: {order.DeliveryAddress}");
        if (order.DeliverySlot?.Label is { } slot)
            sb.AppendLine($"Termin: {slot}");
        return sb.ToString().TrimEnd();
    }
}

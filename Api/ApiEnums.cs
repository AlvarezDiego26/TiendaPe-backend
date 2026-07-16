using TiendaPe.Domain.Entities;

namespace TiendaPe.Api;

public static class ApiEnums
{
    public static string ToApiValue(this PaymentMethod value) => value switch
    {
        PaymentMethod.Cash => "cash",
        PaymentMethod.YapePlin => "yape_plin",
        PaymentMethod.Yape => "yape",
        PaymentMethod.Plin => "plin",
        PaymentMethod.Transfer => "transfer",
        _ => value.ToString()
    };

    public static string ToApiValue(this ExpenseCategory value) => value switch
    {
        ExpenseCategory.Alquiler => "alquiler",
        ExpenseCategory.Servicios => "servicios",
        ExpenseCategory.Transporte => "transporte",
        ExpenseCategory.Sueldos => "sueldos",
        ExpenseCategory.Mantenimiento => "mantenimiento",
        ExpenseCategory.Otros => "otros",
        _ => value.ToString()
    };

    public static bool TryParsePaymentMethod(string value, out PaymentMethod result)
    {
        result = value.Trim().ToLowerInvariant() switch
        {
            "cash" or "efectivo" => PaymentMethod.Cash,
            "yape_plin" or "yape-plin" or "yape/plin" => PaymentMethod.YapePlin,
            "yape" => PaymentMethod.Yape,
            "plin" => PaymentMethod.Plin,
            "transfer" or "transferencia" => PaymentMethod.Transfer,
            _ => default
        };

        return value.Trim().ToLowerInvariant() is "cash" or "efectivo" or "yape_plin" or "yape-plin" or "yape/plin" or "yape" or "plin" or "transfer" or "transferencia";
    }

    public static string ToApiValue(this ProductTrackingType value) => value switch
    {
        ProductTrackingType.Unit => "unit",
        ProductTrackingType.Package => "package",
        ProductTrackingType.Weight => "weight",
        ProductTrackingType.Bulk => "bulk",
        _ => value.ToString()
    };

    public static bool TryParseProductTrackingType(string? value, out ProductTrackingType result)
    {
        result = string.IsNullOrWhiteSpace(value)
            ? ProductTrackingType.Unit
            : value.Trim().ToLowerInvariant() switch
            {
                "unit" or "unidad" => ProductTrackingType.Unit,
                "package" or "paquete" or "caja" => ProductTrackingType.Package,
                "weight" or "peso" => ProductTrackingType.Weight,
                "bulk" or "granel" => ProductTrackingType.Bulk,
                _ => ProductTrackingType.Unit
            };

        return string.IsNullOrWhiteSpace(value) ||
               value.Trim().ToLowerInvariant() is "unit" or "unidad" or "package" or "paquete" or "caja" or "weight" or "peso" or "bulk" or "granel";
    }

    public static bool TryParseExpenseCategory(string value, out ExpenseCategory result)
    {
        result = value.Trim().ToLowerInvariant() switch
        {
            "alquiler" => ExpenseCategory.Alquiler,
            "servicios" => ExpenseCategory.Servicios,
            "transporte" => ExpenseCategory.Transporte,
            "sueldos" => ExpenseCategory.Sueldos,
            "mantenimiento" => ExpenseCategory.Mantenimiento,
            "otros" => ExpenseCategory.Otros,
            _ => default
        };

        return value.Trim().ToLowerInvariant() is "alquiler" or "servicios" or "transporte" or "sueldos" or "mantenimiento" or "otros";
    }
}

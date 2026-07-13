using TiendaPe.Domain.Entities;

namespace TiendaPe.Api;

public static class ApiEnums
{
    public static string ToApiValue(this PaymentMethod value) => value switch
    {
        PaymentMethod.Cash => "cash",
        PaymentMethod.YapePlin => "yape_plin",
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
            "yape_plin" or "yape-plin" or "yape/plin" or "yape" or "plin" => PaymentMethod.YapePlin,
            _ => default
        };

        return value.Trim().ToLowerInvariant() is "cash" or "efectivo" or "yape_plin" or "yape-plin" or "yape/plin" or "yape" or "plin";
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

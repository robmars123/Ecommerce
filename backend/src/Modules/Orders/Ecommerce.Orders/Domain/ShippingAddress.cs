namespace Ecommerce.Orders.Domain;

/// <summary>The address snapshot taken at checkout (arch 9 checkout contract), stored as jsonb. Never edited.</summary>
internal sealed record ShippingAddress(
    string FullName,
    string Line1,
    string? Line2,
    string City,
    string? Region,
    string PostalCode,
    string Country);

namespace ShopSphere.Api.Entities;

// Copy of the address at the time the order was placed, stored in the Orders table.
public class OrderAddress
{
    public string FullName { get; set; } = null!;
    public string Line1 { get; set; } = null!;
    public string? Line2 { get; set; }
    public string City { get; set; } = null!;
    public string? State { get; set; }
    public string PostalCode { get; set; } = null!;
    public string Country { get; set; } = null!;
    public string? PhoneNumber { get; set; }
}

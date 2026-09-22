namespace ShopSphere.Api.Entities;

public class Address : AuditableEntity
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public string FullName { get; set; } = null!;
    public string Line1 { get; set; } = null!;
    public string? Line2 { get; set; }
    public string City { get; set; } = null!;
    public string? State { get; set; }
    public string PostalCode { get; set; } = null!;
    public string Country { get; set; } = null!;
    public string? PhoneNumber { get; set; }
    public bool IsDefault { get; set; }
}

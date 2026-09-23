namespace ShopSphere.Api.Features.Checkout;

public class ShippingSettings
{
    public const string SectionName = "Shipping";

    public decimal FlatRate { get; set; } = 5.99m;
    public decimal FreeShippingThreshold { get; set; } = 75m;

    public decimal CalculateFor(decimal amountAfterDiscount) =>
        amountAfterDiscount <= 0 || amountAfterDiscount >= FreeShippingThreshold ? 0m : FlatRate;
}

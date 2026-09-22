namespace ShopSphere.Api.Entities;

public enum UserRole
{
    Customer,
    Admin
}

public enum OrderStatus
{
    Pending,
    Confirmed,
    Processing,
    Shipped,
    Delivered,
    Cancelled
}

public enum PaymentStatus
{
    Pending,
    Succeeded,
    Failed,
    Refunded
}

public enum DiscountType
{
    Percentage,
    FixedAmount
}

public enum InventoryChangeReason
{
    Restock,
    Sale,
    Adjustment,
    OrderCancelled,
    OrderExpired
}

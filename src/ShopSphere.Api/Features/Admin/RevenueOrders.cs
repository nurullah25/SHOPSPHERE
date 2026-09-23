using ShopSphere.Api.Entities;

namespace ShopSphere.Api.Features.Admin;

// One definition of "this order counts as revenue", shared by the dashboard
// and every report. Pending orders aren't paid yet and cancelled ones were
// refunded, so neither counts.
public static class RevenueOrders
{
    public static readonly OrderStatus[] CountedStatuses =
    [
        OrderStatus.Confirmed,
        OrderStatus.Processing,
        OrderStatus.Shipped,
        OrderStatus.Delivered
    ];

    public static IQueryable<Order> CountedAsRevenue(this IQueryable<Order> orders) =>
        orders.Where(o => CountedStatuses.Contains(o.Status));

    public static IQueryable<OrderItem> FromRevenueOrders(this IQueryable<OrderItem> items) =>
        items.Where(i => CountedStatuses.Contains(i.Order.Status));
}

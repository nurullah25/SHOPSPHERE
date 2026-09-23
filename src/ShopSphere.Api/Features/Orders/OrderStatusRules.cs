using ShopSphere.Api.Common;
using ShopSphere.Api.Entities;

namespace ShopSphere.Api.Features.Orders;

// The order state machine lives in one place so it can't drift between
// the customer endpoints, the admin endpoints and the background job.
public static class OrderStatusRules
{
    private static readonly Dictionary<OrderStatus, OrderStatus[]> AllowedTransitions = new()
    {
        [OrderStatus.Pending] = [OrderStatus.Confirmed, OrderStatus.Cancelled],
        [OrderStatus.Confirmed] = [OrderStatus.Processing, OrderStatus.Cancelled],
        [OrderStatus.Processing] = [OrderStatus.Shipped, OrderStatus.Cancelled],
        [OrderStatus.Shipped] = [OrderStatus.Delivered],
        [OrderStatus.Delivered] = [],
        [OrderStatus.Cancelled] = []
    };

    public static IReadOnlyList<OrderStatus> AllowedNext(OrderStatus status) =>
        AllowedTransitions.TryGetValue(status, out var next) ? next : Array.Empty<OrderStatus>();

    public static bool CanTransition(OrderStatus from, OrderStatus to) => AllowedNext(from).Contains(to);

    // Customers can call off an order until the warehouse starts working on it
    public static bool CanCustomerCancel(OrderStatus status) =>
        status is OrderStatus.Pending or OrderStatus.Confirmed;

    public static void EnsureTransition(OrderStatus from, OrderStatus to)
    {
        if (CanTransition(from, to))
            return;

        var allowed = AllowedNext(from);
        var detail = allowed.Count == 0
            ? $"A {from.ToString().ToLowerInvariant()} order can't change status anymore."
            : $"An order in {from} can only move to {string.Join(" or ", allowed)}.";

        throw new BusinessRuleException("Invalid status change", detail);
    }
}

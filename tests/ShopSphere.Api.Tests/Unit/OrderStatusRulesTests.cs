using ShopSphere.Api.Common;
using ShopSphere.Api.Entities;
using ShopSphere.Api.Features.Orders;

namespace ShopSphere.Api.Tests.Unit;

public class OrderStatusRulesTests
{
    [Theory]
    [InlineData(OrderStatus.Pending, OrderStatus.Confirmed)]
    [InlineData(OrderStatus.Pending, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Processing)]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Processing, OrderStatus.Shipped)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Delivered)]
    public void Allowed_transitions_are_accepted(OrderStatus from, OrderStatus to)
    {
        Assert.True(OrderStatusRules.CanTransition(from, to));
    }

    [Theory]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Shipped)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Confirmed)]
    [InlineData(OrderStatus.Delivered, OrderStatus.Shipped)]
    [InlineData(OrderStatus.Pending, OrderStatus.Shipped)]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Delivered)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Processing)]
    public void Invalid_transitions_are_rejected(OrderStatus from, OrderStatus to)
    {
        Assert.False(OrderStatusRules.CanTransition(from, to));

        var exception = Assert.Throws<BusinessRuleException>(() => OrderStatusRules.EnsureTransition(from, to));
        Assert.Equal(409, exception.StatusCode);
    }

    [Theory]
    [InlineData(OrderStatus.Pending, true)]
    [InlineData(OrderStatus.Confirmed, true)]
    [InlineData(OrderStatus.Processing, false)]
    [InlineData(OrderStatus.Shipped, false)]
    [InlineData(OrderStatus.Delivered, false)]
    [InlineData(OrderStatus.Cancelled, false)]
    public void Customers_can_only_cancel_before_the_order_is_processed(OrderStatus status, bool expected)
    {
        Assert.Equal(expected, OrderStatusRules.CanCustomerCancel(status));
    }

    [Fact]
    public void Final_states_have_no_next_status()
    {
        Assert.Empty(OrderStatusRules.AllowedNext(OrderStatus.Delivered));
        Assert.Empty(OrderStatusRules.AllowedNext(OrderStatus.Cancelled));
    }
}

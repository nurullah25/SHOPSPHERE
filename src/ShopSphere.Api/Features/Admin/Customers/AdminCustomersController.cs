using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopSphere.Api.Common;
using ShopSphere.Api.Data;
using ShopSphere.Api.Entities;

namespace ShopSphere.Api.Features.Admin.Customers;

public class CustomerListItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int OrderCount { get; set; }
    public decimal TotalSpent { get; set; }
    public DateTime? LastOrderAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CustomerDetailDto : CustomerListItemDto
{
    public string? PhoneNumber { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public List<CustomerOrderDto> RecentOrders { get; set; } = new();
}

public class CustomerOrderDto
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public DateTime PlacedAt { get; set; }
}

public class UpdateCustomerStatusRequest
{
    public bool IsActive { get; set; }
}

[ApiController]
[Route("api/admin/customers")]
[Authorize(Policy = Policies.Admin)]
public class AdminCustomersController : ControllerBase
{
    private const int RecentOrderCount = 10;

    private readonly AppDbContext _db;
    private readonly AuditService _audit;

    public AdminCustomersController(AppDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<CustomerListItemDto>>> GetCustomers(
        [FromQuery, MaxLength(100)] string? search,
        [FromQuery] bool? isActive,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 20)
    {
        var customers = _db.Users.AsNoTracking().Where(u => u.Role == UserRole.Customer);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            customers = customers.Where(u =>
                u.Email.Contains(term) || u.FirstName.Contains(term) || u.LastName.Contains(term));
        }

        if (isActive.HasValue)
            customers = customers.Where(u => u.IsActive == isActive);

        return Ok(await customers
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new CustomerListItemDto
            {
                Id = u.Id,
                Name = u.FirstName + " " + u.LastName,
                Email = u.Email,
                IsActive = u.IsActive,
                // Spend only counts orders that were actually paid
                OrderCount = u.Orders.Count(o => RevenueOrders.CountedStatuses.Contains(o.Status)),
                TotalSpent = u.Orders.Where(o => RevenueOrders.CountedStatuses.Contains(o.Status)).Sum(o => (decimal?)o.Total) ?? 0m,
                LastOrderAt = u.Orders.Max(o => (DateTime?)o.PlacedAt),
                CreatedAt = u.CreatedAt
            })
            .ToPagedResultAsync(page, pageSize));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CustomerDetailDto>> GetCustomer(int id)
    {
        var customer = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == id && u.Role == UserRole.Customer)
            .Select(u => new CustomerDetailDto
            {
                Id = u.Id,
                Name = u.FirstName + " " + u.LastName,
                Email = u.Email,
                PhoneNumber = u.PhoneNumber,
                IsActive = u.IsActive,
                LastLoginAt = u.LastLoginAt,
                CreatedAt = u.CreatedAt,
                OrderCount = u.Orders.Count(o => RevenueOrders.CountedStatuses.Contains(o.Status)),
                TotalSpent = u.Orders.Where(o => RevenueOrders.CountedStatuses.Contains(o.Status)).Sum(o => (decimal?)o.Total) ?? 0m,
                LastOrderAt = u.Orders.Max(o => (DateTime?)o.PlacedAt),
                RecentOrders = u.Orders
                    .OrderByDescending(o => o.PlacedAt)
                    .Take(RecentOrderCount)
                    .Select(o => new CustomerOrderDto
                    {
                        Id = o.Id,
                        OrderNumber = o.OrderNumber,
                        Status = o.Status.ToString(),
                        Total = o.Total,
                        PlacedAt = o.PlacedAt
                    })
                    .ToList()
            })
            .SingleOrDefaultAsync()
            ?? throw new NotFoundException($"Customer {id} was not found.");

        return Ok(customer);
    }

    // Customers are never deleted, because their orders must stay intact
    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, UpdateCustomerStatusRequest request)
    {
        var customer = await _db.Users.SingleOrDefaultAsync(u => u.Id == id && u.Role == UserRole.Customer)
            ?? throw new NotFoundException($"Customer {id} was not found.");

        customer.IsActive = request.IsActive;

        if (!request.IsActive)
        {
            // A disabled account shouldn't be able to refresh its way back in
            await _db.RefreshTokens
                .Where(t => t.UserId == id && t.RevokedAt == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.RevokedAt, DateTime.UtcNow));
        }

        _audit.Record(User.GetUserId(), request.IsActive ? "CustomerActivated" : "CustomerDeactivated",
            nameof(User), id, new { customer.Email });

        await _db.SaveChangesAsync();
        return NoContent();
    }
}

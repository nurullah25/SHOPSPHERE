# ShopSphere

A single-vendor e-commerce platform built with **ASP.NET Core 8** and **Angular 20**. It has a customer storefront (catalog, cart, wishlist, checkout, order history) and an admin area (catalog, inventory, orders, coupons, customers, reports).

The goal is realistic business logic rather than a tutorial cart: checkout runs in a database transaction, stock is decremented atomically, orders follow a strict status state machine, order lines keep the price that was paid, and coupons enforce expiry and usage limits.

> 🚧 Work in progress. The project is built in phases (see [Roadmap](#roadmap)).

## Tech stack

| Layer | Technologies |
|---|---|
| Backend | C#, .NET 8, ASP.NET Core Web API, Entity Framework Core, SQL Server, LINQ |
| Auth | JWT access tokens, rotating refresh tokens (HttpOnly cookie), role-based authorization |
| Frontend | Angular 20, TypeScript, RxJS, Angular Material, Reactive Forms, Router, HTTP interceptors, route guards |
| Logging / docs | Serilog (console + rolling file), Swagger / OpenAPI |
| Testing | xUnit, integration tests with `WebApplicationFactory` against SQL Server LocalDB |

Why Angular 20: it's an LTS release still receiving fixes, with the same support window as .NET 8 (Nov 2026).

## Planned features

**Customer:** registration and login · product search with category, price and availability filters · sorting and pagination · product details with reviews · cart with live stock validation · wishlist · checkout with coupons, shipping rules and mock payment · order history and cancellation · profile and address book

**Admin:** dashboard (revenue, orders, customers, low stock, recent orders) · product and category management with images · inventory adjustments and stock history · order processing with enforced status transitions · coupons · customers · sales reports

See [docs/design.md](docs/design.md) for the database design, checkout flow and order state machine.

## Project structure

```
ShopSphere/
├── src/
│   ├── ShopSphere.Api/        ASP.NET Core Web API
│   └── shopsphere-web/        Angular application
├── tests/
│   └── ShopSphere.Api.Tests/  xUnit integration tests
└── docs/                      design notes, screenshots, Postman collection
```

## Getting started

### Prerequisites

- .NET 8 SDK (or newer SDK that can target `net8.0`)
- Node.js 20.19+ or 22.12+
- SQL Server or SQL Server Express LocalDB
- Trusted HTTPS dev certificate: `dotnet dev-certs https --trust`

### Run the API

The API needs a JWT signing key (at least 32 characters). It isn't committed, so set it once in User Secrets. The API refuses to start without it.

```bash
dotnet tool restore
cd src/ShopSphere.Api
dotnet user-secrets set "Jwt:Key" "<a long random string, 32+ characters>"
dotnet run --launch-profile https
```

In the Development environment the API applies EF Core migrations and seeds demo data on startup. The default connection string points to LocalDB (`(localdb)\MSSQLLocalDB`, database `ShopSphere`). Change `ConnectionStrings:DefaultConnection` if you use a full SQL Server instance.

To apply migrations manually instead:

```bash
dotnet ef database update --project src/ShopSphere.Api
```

- Swagger UI: https://localhost:7001/swagger
- Health check: https://localhost:7001/health

### Demo accounts (Development only)

| Role | Email | Password |
|---|---|---|
| Admin | admin@shopsphere.local | Admin#12345 |
| Customer | demo@shopsphere.local | Customer#12345 |

### Test payments

Checkout uses a mock payment gateway, so no real payment data is involved. The checkout page offers three test cards:

| Card | Result |
|---|---|
| Visa ending 4242 | Payment approved |
| Visa ending 0002 | Card declined |
| Mastercard ending 9995 | Insufficient funds |

A declined payment leaves the order in Pending with its stock reserved, and the confirmation page offers a retry.

The seed also creates a category tree, 32 products (some discounted, low on stock, out of stock or inactive) and four coupons: `WELCOME10`, `SAVE20`, `FLASH50`, and the expired `SUMMER25`.

### Run the Angular app

```bash
cd src/shopsphere-web
npm install
npm start
```

Open http://localhost:4200. Requests to `/api` are forwarded to the API by the Angular dev server (`proxy.conf.json`), so the browser sees a single origin and no CORS setup is needed in development.

### Configuration

| Setting | Where | Notes |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | appsettings.json | LocalDB by default |
| `Jwt:Key` | User Secrets / environment variable | Required, never committed |
| `Jwt:AccessTokenMinutes`, `Jwt:RefreshTokenDays` | appsettings.json | 15 minutes / 7 days |
| `Seed:*` | appsettings.Development.json | Demo account credentials, Development only |
| `Shipping:FlatRate`, `Shipping:FreeShippingThreshold` | appsettings.json | $5.99, free over $75 |

In production, settings would come from environment variables (e.g. `Jwt__Key`) or a secret store.

## Running the tests

```bash
dotnet test
```

The integration tests start the API in memory with `WebApplicationFactory` and run against a separate LocalDB database (`ShopSphere_Tests`), which is dropped and recreated at the start of each run. They use a real SQL Server because the business rules rely on transactions and constraints that the EF Core in-memory provider doesn't support.

## Roadmap

- [x] Phase 0: Solution setup, Serilog, Swagger, global error handling, Angular shell
- [x] Phase 1: Database schema, migrations, seed data
- [x] Phase 2: Authentication and authorization
- [x] Phase 3: Catalog API and admin catalog management
- [x] Phase 4: Storefront UI and admin catalog screens
- [x] Phase 5: Cart and wishlist
- [x] Phase 6: Coupons, checkout and mock payment
- [ ] Phase 7: Orders and status workflow
- [ ] Phase 8: Inventory management
- [ ] Phase 9: Reviews
- [ ] Phase 10: Admin dashboard, reports, customers
- [ ] Phase 11: Account pages and polish
- [ ] Phase 12: Documentation, screenshots, Postman collection

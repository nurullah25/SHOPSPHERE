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
├── tests/                     xUnit tests (added with the auth phase)
└── docs/                      design notes, screenshots, Postman collection
```

## Getting started

### Prerequisites

- .NET 8 SDK (or newer SDK that can target `net8.0`)
- Node.js 20.19+ or 22.12+
- SQL Server or SQL Server Express LocalDB
- Trusted HTTPS dev certificate: `dotnet dev-certs https --trust`

### Run the API

```bash
cd src/ShopSphere.Api
dotnet run --launch-profile https
```

- Swagger UI: https://localhost:7001/swagger
- Health check: https://localhost:7001/health

### Run the Angular app

```bash
cd src/shopsphere-web
npm install
npm start
```

Open http://localhost:4200. Requests to `/api` are forwarded to the API by the Angular dev server (`proxy.conf.json`), so the browser sees a single origin and no CORS setup is needed in development.

### Configuration

No secrets are committed. Local-only values (JWT signing key, etc.) go into .NET User Secrets:

```bash
cd src/ShopSphere.Api
dotnet user-secrets set "Jwt:Key" "<a long random string>"
```

## Roadmap

- [x] Phase 0: Solution setup, Serilog, Swagger, global error handling, Angular shell
- [ ] Phase 1: Database schema, migrations, seed data
- [ ] Phase 2: Authentication and authorization
- [ ] Phase 3: Catalog API and admin catalog management
- [ ] Phase 4: Storefront UI
- [ ] Phase 5: Cart and wishlist
- [ ] Phase 6: Coupons, checkout and mock payment
- [ ] Phase 7: Orders and status workflow
- [ ] Phase 8: Inventory management
- [ ] Phase 9: Reviews
- [ ] Phase 10: Admin dashboard, reports, customers
- [ ] Phase 11: Account pages and polish
- [ ] Phase 12: Documentation, screenshots, Postman collection

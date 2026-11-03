# Car Rental: Clean Architecture + CQRS-lite (.NET 8)

[![CI](https://github.com/Legion91919/CarRental-CleanArchitecture/actions/workflows/ci.yml/badge.svg)](https://github.com/Legion91919/CarRental-CleanArchitecture/actions/workflows/ci.yml)

A proof of concept for a car rental system: search airport and city branches, sign in, verify the
main driver (SA ID or passport), price the trip with transparent rules, accept the terms, book
without ever overbooking and with the card authorised (mock gateway), and, in the separate
**Roam Staff** counter app, check the car out, take it back and capture the payment with any late fee.

**Design B: a Clean Architecture monolith with CQRS-lite.**
C# / .NET 8 · ASP.NET Core · MediatR · EF Core (SQL Server) · MongoDB read model · xUnit ·
Testcontainers · React + TanStack Query · GitHub Actions · Azure Pipelines

> **Start here:** [AGENTS.md](AGENTS.md) (architecture, business, security and design rules) ·
> [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) (diagrams, decisions, trade-offs) ·
> [docs/SOLID.md](docs/SOLID.md) (each principle → the code → the test that proves it)

---

## Solution layout

```
CarRental.sln
├── services/
│   ├── CarRental.Domain/          ← zero dependencies: aggregates (incl. Payment), value objects, pricing rules, policies
│   ├── CarRental.Application/     ← use cases (MediatR commands/queries), ports, pipeline behaviours
│   ├── CarRental.Infrastructure/  ← EF Core + SQL Server, MongoDB projection, repositories, unit of work
│   └── CarRental.Api/             ← composition root: DI, thin controllers, ProblemDetails
├── tests/
│   ├── CarRental.Domain.Tests/        ← pure business rules (pricing, eligibility, late fees, inventory)
│   ├── CarRental.Application.Tests/   ← handlers with fakes and mocks, pipeline wiring
│   ├── CarRental.ArchitectureTests/   ← the Dependency Rule, enforced
│   └── CarRental.IntegrationTests/    ← real SQL Server via Testcontainers: overbooking under concurrency
├── client/                        ← React + TypeScript + TanStack Query + Tailwind (see client/README.md)
├── docs/                          ← architecture and SOLID map
├── docker-compose.yml             ← SQL Server + MongoDB for local development
├── .github/workflows/ci.yml       ← build + test on every push
└── azure-pipelines.yml            ← build, test, publish, deploy Dev → Prod (with approval gate)
```

Dependencies point **inwards only**: `Api → Infrastructure → Application → Domain`.

## Business rules

The full list, with error codes and where each rule lives, is in [AGENTS.md §3](AGENTS.md#3-business-rules-the-source-of-truth-is-the-domain-keep-this-table-in-sync). Highlights:

| Rule | Where it lives | Test |
|---|---|---|
| **No overbooking**, even under concurrent requests | `CategoryInventory` (aggregate per branch/category/day) + rowversion + check constraint + retry | `OverbookingTests` (Testcontainers) |
| Charge per started 24h, 59-minute grace; weekly 15%; Gold 10% / Silver 5% | `RentalPeriod`, `IPriceRule` rules | `RentalPricerTests` |
| Young driver R150/day, one-way R750, capped extras; fees never discounted | `YoungDriverFeeRule`, `OneWayFeeRule`, `ExtrasRule`, `PriceContext` | `RentalPricerTests` |
| Eligibility: 21+, licence 1+ year, Luxury 25+ | `DriverEligibilityPolicy` | `DriverEligibilityPolicyTests` |
| **Signed-in customers only**; age, licence and loyalty tier come from the verified profile, never the request | `CurrentCustomer`, `CreateReservationHandler` | `CreateReservationHandlerTests`, `ApiEndToEndTests` |
| **SA ID number** validated (date, citizenship digit, Luhn) and must match the date of birth; passports validated; shown masked only | `SouthAfricanIdNumber`, `IdentityDocument`, `Customer` | `SouthAfricanIdNumberTests`, `CustomerTests` |
| **Terms and conditions**: the current version must be accepted; stored on the booking | `TermsAcceptance` | `TermsAcceptanceTests` |
| Late fee 1.5× daily rate per started 24h after 59 min; odometer can't decrease | `Rental` + DB check constraint | `RentalTests` |
| Only the owner can see or cancel a booking (others get 404); counter operations need the Agent role | read-model SQL filter, `[Authorize(Roles = "Agent")]` | `ApiEndToEndTests` |
| **Card payments** (mock gateway): authorise at booking, capture total + late fee at return (at most 15% over the hold), void on cancel; card numbers never reach the Domain, database or logs | `Payment`, `IPaymentGateway`, `MockPaymentGateway`, `/api/payments/tokenize` | `PaymentTests`, `RentalLifecycleTests`, `CounterAndPaymentTests` |
| Agents may look up **any** booking by reference; customers only their own | `ICounterReadModel` (Agent role) vs `IReservationReadModel` (owner filter) | `CounterAndPaymentTests` |
| **Fleet**: real cars with plate/VIN, status and logbook; registering or retiring a car changes the branch's capacity in the same transaction | `Vehicle`, `FleetCapacityHandlers` | `VehicleTests`, `FleetInspectionJourneyTests` |
| **No keys without a checkout inspection** (checklist, readings, 4 photos, driver verified); return needs a return inspection, which lists new damage; completed reports are locked evidence | `InspectionReport`, `Rental.CheckOut` / `Return` | `InspectionReportTests`, `RentalLifecycleTests`, `FleetInspectionJourneyTests` |
| **Uploads**: JPEG/PNG/PDF by magic bytes, ≤ 10 MB, metadata stripped, random storage keys, served only through an authorised endpoint | `FileUploadPolicy`, `IFileStorage` | `InspectionHandlerTests`, `FleetInspectionJourneyTests` |
| **Pay at the branch**: book without a card; the agent holds the total on the card at checkout, before the keys | `Reservation.PaymentOption`, `Rental.CheckOut`, `TakeCounterPaymentHandler` | `PayAtBranchTests` (Domain, Application), `CounterAndPaymentTests` |
| Agents cancel with a reason; **no-show** from pickup + 2h charges one day's rate | `Reservation.Cancel` / `MarkNoShow`, `OneDayRateNoShowFee` | `ReservationTests`, `CancelReservationHandlerTests` |

## Live demo (GitHub Codespaces, nothing to install)

1. On this page click **Code → Codespaces → Create codespace on main** (takes a few minutes the first time).
2. The API and the React client **start automatically** in two terminals once the codespace opens.
   (If you stopped them: `dotnet run --project services/CarRental.Api --launch-profile http` and `cd client && npm run dev`.)
3. Open the **Ports** tab: port **5173** is the React app, port **5080** + `/swagger` is the API.
4. Create an account in the app, or use the **development demo accounts** (password `Demo_Pass_123!`):
   `gold@roam.test` (Gold loyalty customer), `young@roam.test` (under 25: young driver fee) and
   `agent@roam.test` (counter agent: open **#/agent**, or "Staff sign-in" in the footer).

### Try a late return without waiting

`DemoClock:Offset` (Development only) shifts the API's clock. Book a pickup an hour from now for
12 days, check it out in the staff app, then restart the API 12 days and 3 hours later:

```bash
DemoClock__Offset=12.03:00:00 dotnet run --project services/CarRental.Api --launch-profile http
```

The car shows as **Late** under Returns; returning it adds a R525 late fee and captures
booking total + fee on the card (inside the 15% the hold allows).

## Sign-up walkthrough

The same steps as the app, as HTTP calls (all in [`CarRental.Api.http`](services/CarRental.Api/CarRental.Api.http)):

1. `POST /api/auth/register` `{ email, password }` (8+ chars, upper, lower, digit, symbol), then
   `POST /api/auth/login` → `accessToken`. In the app, the **Create account** tab has an
   **Example** helper that fills a sample email and lists valid test SA ID numbers.
2. `PUT /api/customers/me` with the driver profile. Valid test SA ID numbers:
   `9005105009088` (born 1990-05-10), `0308125800083` (2003-08-12, under 25),
   `8512310001082` (1985-12-31, already used by `gold@roam.test`). The date of birth must match the ID.
3. `GET /api/terms/current` → send its `version` back as `acceptedTermsVersion`.
4. `POST /api/payments/tokenize` `{ number, expiry, cvc }` with a test card → `{ token, brand, last4 }`.
   `4242 4242 4242 4242` Visa (approved) · `5555 5555 5555 4444` Mastercard (approved) ·
   `4000 0000 0000 0002` (declined: `card_declined`) · `4000 0000 0000 9995` (declined:
   `insufficient_funds`) · `4000 0000 0000 0077` (approved after 2 seconds).
5. `POST /api/reservations` with the trip, `acceptedTermsVersion` and `paymentToken` → 201 with the
   price and `payment: { status: "Authorized", cardBrand, cardLast4 }`, or **402
   `payment.declined`** with a `reason`, in which case nothing is booked.

## Run it locally

```bash
docker compose up -d                                         # SQL Server + MongoDB
dotnet test                                                  # all tests (integration tests need Docker)
dotnet run --project services/CarRental.Api --launch-profile http # http://localhost:5080/swagger
cd client && npm install && npm run dev                      # http://localhost:5173
```

Use the Mongo read model for availability search: `--launch-profile http-mongo`
(or set `ReadModels__Provider=Mongo`).

## API

| Method | Route | Auth | Side | Purpose |
|---|---|---|---|---|
| POST | `/api/auth/register`, `/api/auth/login` | – | – | ASP.NET Core Identity: create account, get a bearer token |
| GET | `/api/branches` | – | query | Airports and city branches |
| GET | `/api/availability?branchId&pickupAt&returnAt` | – | query | Cars available per category for the whole period |
| GET | `/api/terms/current` | – | query | Current rental terms and their version |
| GET / PUT | `/api/customers/me` | customer | query / command | Driver profile (SA ID or passport); ID returned masked |
| POST | `/api/quotes` | customer | query | Price for the signed-in driver, line by line |
| POST | `/api/payments/tokenize` | signed in | – | Mock hosted fields: test card → single-use token (never stored or logged) |
| POST | `/api/reservations` | customer | command | Book: `paymentOption` `Card` (with `paymentToken`: the card is authorised; 402 `payment.declined`) or `PayAtBranch` (no token, nothing taken). 409 sold out; 422 rule broken |
| GET | `/api/reservations/mine` | customer | query | My bookings |
| GET | `/api/reservations/{id}` | owner | query | Booking details (404 if not yours) |
| POST | `/api/reservations/{id}/cancel` | owner | command | Cancel before pickup, release inventory, void the card hold |
| GET | `/api/reservations/{id}/inspections` | owner | query | Completed checkout/return reports of my booking (drafts and others' are 404) |
| POST | `/api/reservations/{id}/inspections/{inspectionId}/acknowledge` | owner | command | "I've checked this report" |
| GET | `/api/inspections/{id}/attachments/{attachmentId}` | owner (completed) or Agent | query | Download a photo/file (`nosniff`, CSP, `Content-Disposition`); otherwise 404 |
| GET | `/api/account/me` | signed in | query | Email and roles (tells customers from staff) |
| GET | `/api/agent/branches/{branchId}/pickups?date` | Agent role | query | Bookings collecting a car that day (SAST) |
| GET | `/api/agent/branches/{branchId}/returns?date` | Agent role | query | Cars due back by the end of that day, `isLate` flagged |
| GET | `/api/agent/reservations/{reference}` | Agent role | query | Any booking by `ROAM-XXXX-XXXX-XXXX-XXXX` or id |
| POST | `/api/agent/customers` | Agent role | command | Register a walk-in customer; temporary password returned once |
| POST | `/api/agent/reservations/{id}/cancel` | Agent role | command | Cancel any confirmed booking, body `{ reason }` (shown to the customer); voids the hold |
| POST | `/api/agent/reservations/{id}/no-show` | Agent role | command | From pickup + 2h: close as no-show, capture one day's rate, release the rest |
| POST | `/api/agent/reservations/{id}/payment` | Agent role | command | Pay at the branch: `{ paymentToken }` holds the booking total on the customer's card (402 on a decline; once only) |
| GET | `/api/agent/vehicles?branchId&category&status&search&page&pageSize` | Agent role | query | Fleet list, paged |
| GET | `/api/agent/vehicles/{id}`, `/api/agent/vehicles/{id}/history` | Agent role | query | One car; its logbook + rentals with their inspection reports |
| GET | `/api/agent/vehicles/available-for/{reservationId}?includeUpgrades` | Agent role | query | Cars that can be handed over for this booking |
| POST | `/api/agent/vehicles` | Agent role | command | Register a car (adds capacity at its branch) |
| POST | `/api/agent/vehicles/{id}/service`, `/service/return`, `/retire` | Agent role | command | Status changes (`{ reason }`, `{ odometerKm }`, `{ reason }`) |
| POST | `/api/agent/inspections` | Agent role | command | Start (or resume) the Checkout/Return report: `{ reservationId, kind, vehicleId }` |
| GET / PUT | `/api/agent/inspections/{id}` | Agent role | query / command | Read; save checklist, odometer, fuel, damage marks (drafts only) |
| POST / DELETE | `/api/agent/inspections/{id}/attachments[/{attachmentId}]` | Agent role | command | Upload (multipart `file` + `angle`; JPEG/PNG/PDF by magic bytes, ≤ 10 MB) / remove a file |
| POST | `/api/agent/inspections/{id}/complete` | Agent role | command | Lock the report (checklist, notes, readings, 4 photos at checkout; new damage computed at return) |
| POST | `/api/rentals` | Agent role | command | Check out `{ reservationId, vehicleId, freeUpgrade }`: needs a completed checkout inspection, and for pay-at-branch bookings the counter payment |
| POST | `/api/rentals/{id}/return` | Agent role | command | Return (no body: odometer from the return inspection); late fee, payment capture |

Errors are RFC 7807 ProblemDetails with a stable `code`, e.g. `inventory.sold_out`,
`eligibility.under_age`, `customer.birth_date_mismatch`, `terms.outdated`, `payment.declined` (402),
`rental.inspection_required`, `inspection.photos_required`, `file.type_not_allowed`.
In Swagger UI, log in with `/api/auth/login`, then click **Authorize** and paste the `accessToken`.

## Migrations

Development uses `EnsureCreated` + seed data so the PoC runs with zero setup. For real environments:

```bash
dotnet tool install -g dotnet-ef
dotnet ef migrations add InitialCreate -p services/CarRental.Infrastructure -s services/CarRental.Api
```

…then apply migrations from the pipeline (an EF migration bundle) instead of at startup.

## Notes

- Payments use a **mock gateway** (`Payments:Provider = Mock`). A real provider is one more adapter
  implementing `IPaymentGateway`, with the browser posting card details to that provider's hosted
  fields; `/api/payments/tokenize` then disappears from our API.
- MediatR is pinned to **12.x** (Apache 2.0); v13+ uses a commercial licence.
- The connection string and demo password in `appsettings.Development.json` are **local
  development values only**. Real environments use Key Vault / pipeline secrets.
- Bearer tokens are ASP.NET Core Identity tokens protected by Data Protection; in production,
  persist the Data Protection keys (or switch to Entra ID / JWT) so tokens survive restarts.

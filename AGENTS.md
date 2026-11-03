# AGENTS.md

Rules for anyone changing this repository: AI coding agents and people alike. Read this before you
write code. If a change would break a rule here, change the rule in this file first, in the same
pull request, and say why.

- Architecture background: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)
- SOLID map: [docs/SOLID.md](docs/SOLID.md)
- Client structure: [client/README.md](client/README.md)

---

## 1. Commands

```bash
docker compose up -d                                          # SQL Server + MongoDB
dotnet build CarRental.sln                                    # must have 0 errors
dotnet test CarRental.sln                                     # all tests (integration tests need Docker)
dotnet test --filter "FullyQualifiedName!~IntegrationTests"   # fast tests only, no Docker
dotnet run --project services/CarRental.Api --launch-profile http  # http://localhost:5080/swagger
cd client && npm ci && npm run dev                            # http://localhost:5173 (proxies /api)
cd client && npm run lint && npm run build                    # type-check + build
```

A change is done when `dotnet test` and `npm run build` pass and CI is green.

---

## 2. Architecture rules (Clean Architecture monolith, CQRS-lite)

### 2.1 The Dependency Rule (enforced by `tests/CarRental.ArchitectureTests`)

```
Api ──> Infrastructure ──> Application ──> Domain
 └──────────────────────────^
```

| Project | May reference | Must NEVER reference |
|---|---|---|
| `CarRental.Domain` | nothing (BCL only) | EF Core, MediatR, ASP.NET, Mongo, `DateTime.Now`, any package |
| `CarRental.Application` | Domain, MediatR 12.x, FluentValidation, logging abstractions | Infrastructure, Api, EF Core, Mongo, ASP.NET, `HttpContext` |
| `CarRental.Infrastructure` | Application, Domain, EF Core, Mongo, ASP.NET Core Identity | Api |
| `CarRental.Api` | everything (composition root) | business rules |

### 2.2 Where code goes

| You are writing… | Put it in | Example |
|---|---|---|
| A business rule or invariant | **Domain**: aggregate method, value object, policy or domain service | `Rental.Return` refuses a lower odometer |
| A new kind of value (money, ID, period, document) | **Domain** `ValueObjects/` or the aggregate's folder, as an immutable `record` that validates in its factory | `IdentityDocument.SouthAfricanId(...)` |
| A pricing change | **Domain** `Pricing/Rules`: a new `IPriceRule` class + one DI line in `Application/DependencyInjection.cs` | `WeeklyDiscountRule` |
| A use case (something a user does) | **Application**, one folder per use case: command/query + validator + handler | `Reservations/CreateReservation/` |
| Something the use case needs from outside (DB, clock, user, terms) | a **port** (interface) in `Application/Abstractions`, implemented in Infrastructure or Api | `ICurrentUser`, `ITermsProvider` |
| SQL, EF mappings, Mongo, Identity | **Infrastructure** | `ReservationConfiguration` |
| HTTP: routes, status codes, auth attributes | **Api** controllers (thin) and `ApiExceptionHandler` | `ReservationsController` |

### 2.3 CQRS-lite conventions

- **Commands** implement `ICommand<T>`. They load aggregates through repositories, call domain
  methods and save **once** through `IUnitOfWork.SaveChangesAsync`. They never return aggregates.
- **Queries** implement `IQuery<T>`. They **do not load aggregates**. They read flat DTOs through a
  read-model port (`IReservationReadModel`, `IAvailabilityReadModel`…) using `AsNoTracking`
  projections or `Database.SqlQuery<T>`.
- **One handler per request** (`Every_command_and_query_has_exactly_one_handler` test).
- Commands that are safe to repeat after a lost race implement `IRetryOnConcurrencyConflict`.
- Handlers orchestrate; they contain **no business `if`s**. If you're writing
  `if (age < 21) throw…` in a handler, move it into the Domain.

### 2.4 Domain modelling rules

- Aggregates: **no public setters** (enforced by `Aggregates_expose_no_public_setters`). Change
  state only through intention-revealing methods that validate first.
- Aggregates reference other aggregates **by ID only** (`ReservationId`, `CustomerId`), never by
  object reference.
- Use **strongly typed IDs** (`readonly record struct XxxId(Guid Value)`), never raw `Guid` in the Domain.
- Money is `Money` (decimal + currency), rounded once with `Round()`. Never `double`.
- "Now" is always passed in (`DateTimeOffset now`) or comes from the injected `TimeProvider`.
  Never `DateTime.Now` or `DateTimeOffset.UtcNow` in Domain or Application code.
- Broken rules throw `DomainException(code, message)`, or `CapacityExceededException` for sold out.
- Raise a **domain event** (past tense, immutable record) when something other parts of the system
  may react to happens. Events are published after commit by `UnitOfWork`.
- Persistence-only details (rowversion, shadow properties, column names) live in Infrastructure
  configurations, never as attributes on domain classes.

### 2.5 Validation vs business rules

| Kind | Where | HTTP |
|---|---|---|
| Shape: missing field, bad enum, return before pickup | FluentValidation validator next to the command | 400 `validation.failed` |
| Business rule: age, terms, ID checksum, odometer | Domain | 422 with the rule's `code` |
| Card declined by the gateway | Domain (`PaymentDeclinedException`) | 402 `payment.declined` |
| Sold out / lost a race | Domain / UnitOfWork | 409 |
| Not signed in | Application (`UnauthenticatedException`) / `[Authorize]` | 401 |
| Wrong role | `[Authorize(Roles = Roles.Agent)]` | 403 |
| Not found **or not yours** | Application / read model | 404 |

### 2.6 Error codes are a contract

Every `DomainException` has a stable, dotted, lower-case code (`area.reason`). The client maps codes
to friendly text in `client/src/api/client.ts` (`errorMessages`). **Adding a code means adding its
message there. Renaming a code is a breaking change.**

### 2.7 Persistence rules

- Each aggregate has an `IEntityTypeConfiguration` in `Infrastructure/Persistence/Configurations`.
- Value objects are **owned types** (`OwnsOne`) with required navigations.
- Aggregates that can be edited concurrently get a shadow `RowVersion` (`IsRowVersion()`).
- Invariants the database can also guard get a CHECK constraint or a unique index (defence in depth).
- Enums are stored as strings (`HasConversion<string>()`), except `[Flags]` enums.
- Collections that are always read and written with their aggregate (checklist, damage, attachment
  metadata, vehicle logbook) are **JSON columns** (`OwnsMany(...).ToJson()`). EF Core 8 rejects a key
  on a JSON element, so **their value objects must not have a property called `Id`** (use a
  business name such as `AttachmentId`).
- Development uses `EnsureCreated` + seed. **Schema changes need a migration before production.**

---

## 3. Business rules (the source of truth is the Domain; keep this table in sync)

| # | Rule | Code(s) | Where |
|---|---|---|---|
| B1 | Never overbook: one car per occupied day per (branch, category, day), even under concurrent requests | `inventory.sold_out`, `inventory.not_offered`, `concurrency.conflict` | `CategoryInventory`, `InventoryAllocation`, rowversion, CHECK constraint |
| B2 | Charge per started 24h with a 59-minute grace, minimum 1 day | | `RentalPeriod.ChargeableDays` |
| B3 | Daily rates: Economy R350, Compact R450, SUV R750, Luxury R1500 | | `RateCard` |
| B4 | 7+ days: 15% off the base | | `WeeklyDiscountRule` |
| B5 | Loyalty: Gold 10%, Silver 5%, applied after the weekly discount. **Tier is set by the business, never by the customer** | | `LoyaltyDiscountRule`, `Customer.SetLoyaltyTier` |
| B6 | Under 25 on the pickup date: R150/day young driver fee | | `YoungDriverFeeRule` |
| B7 | Different return branch: R750 one-way fee | | `OneWayFeeRule` |
| B8 | Extras: GPS R60/day max R300; child seat R50/day max R250 | | `ExtrasRule` |
| B9 | Discounts never apply to fees or extras | | `PriceContext` |
| B10 | Driver must be 21+, licence held 1+ year; Luxury needs 25+ | `eligibility.under_age`, `eligibility.licence_too_new`, `eligibility.luxury_under_age` | `DriverEligibilityPolicy` |
| B11 | Pickup cannot be in the past; rental 1 hour to 90 days | `reservation.pickup_in_past`, `period.*` | `Reservation`, `RentalPeriod` |
| B12 | Booking requires accepting the **current** terms version; the version is stored on the reservation | `terms.not_accepted`, `terms.outdated` | `TermsAcceptance` |
| B13 | Booking requires a signed-in user with a driver profile; age, licence and tier come from the profile, not the request | `auth.required`, `customer.profile_required` | `CurrentCustomer`, `CreateReservationHandler` |
| B14 | SA ID number: 13 digits, valid YYMMDD date, citizenship digit 0/1, Luhn checksum | `identity.invalid_sa_id` | `SouthAfricanIdNumber` |
| B15 | With an SA ID, date of birth must equal the ID's date of birth | `customer.birth_date_mismatch` | `Customer.UpdateDetails` |
| B16 | Passport: 6-9 letters/digits + ISO alpha-3 country | `identity.invalid_passport`, `identity.invalid_country` | `IdentityDocument.Passport` |
| B17 | One identity document per account | `customer.document_in_use` | `SaveMyProfileHandler` + unique index |
| B18 | A **customer** cancels only a confirmed reservation, only before pickup, only their own; cars go back to inventory | `reservation.not_cancellable`, `reservation.too_late_to_cancel`, 404 | `Reservation.Cancel`, `CancelReservationHandler` |
| B19 | Checkout only from a confirmed reservation, at most 2h before pickup, before the return time; staff (Agent role) only | `rental.too_early`, `rental.reservation_expired`, `reservation.not_checkoutable` | `Rental.CheckOut`, `RentalsController` |
| B20 | Odometer can never decrease; a rental is returned once | `rental.odometer_decreased`, `rental.already_returned` | `Rental.Return` + CHECK constraint |
| B21 | Late fee: 1.5× daily rate per started 24h late, after a 59-minute grace | | `Rental.CalculateLateFee` |
| B22 | Booking authorises (holds) the quoted total on the card **before** anything is saved; a decline saves nothing | `payment.declined` (HTTP 402, with `reason`) | `Payment.Authorize`, `CreateReservationHandler` |
| B23 | Capture only an Authorized payment, at most **15% above** the authorisation (late fees); same currency; amounts positive | `payment.capture_exceeds_authorization`, `payment.invalid_transition`, `money.currency_mismatch`, `payment.invalid_amount` | `Payment.EnsureCanCapture` / `Capture` + CHECK constraints |
| B24 | Void only an Authorized payment; Captured, Voided and Failed are final | `payment.invalid_transition` | `Payment.Void` |
| B25 | Cancelling (customer or agent) voids the hold; a no-show captures the no-show fee (B44); returning captures booking total + late fee; a declined capture or void marks the payment Failed but still records the return/cancellation | | `BookingRelease`, `ReturnVehicleHandler` |
| B26 | One payment per reservation; the reservation id is the gateway idempotency key, so retries never authorise, capture or void twice | | unique index, `PaymentIdempotencyKey`, `CreateReservationCommand.ReservationId` |
| B27 | Agents may look up **any** booking (by `ROAM-XXXX-XXXX-XXXX-XXXX` reference or id); customers only their own | 404 | `ICounterReadModel` (Agent role) vs `IReservationReadModel` (owner filter) |
| B28 | A rental still out 59 minutes after its due time is **late** (flagged at the counter; the fee starts then) | | `Rental.IsOverdue` |
| B29 | The agent's branch is irrelevant to checkout (for now): any agent can hand over any branch's car | | `RentalLifecycleTests` (trade-off in docs/ARCHITECTURE.md) |
| B30 | An agent can register a walk-in customer; every customer rule (B14-B17) is checked **before** the sign-in account is created; one account per email | `account.email_in_use` + B14-B17 codes | `AgentRegisterCustomerHandler`, `Customer.EnsureValidDetails` |
| **Fleet** | | | |
| B31 | A car has a SA plate (`CA 123-456`, `ND 45678`: 1-3 letters, space, digits, optional `-digits`) and a 17-character VIN (no I, O, Q); both unique; model year not in the future and at most 10 years old; odometer ≥ 0 | `vehicle.invalid_registration`, `vehicle.invalid_vin`, `vehicle.duplicate_registration`, `vehicle.duplicate_vin`, `vehicle.invalid_year`, `vehicle.too_old`, `vehicle.negative_odometer` | `Vehicle.Register`, unique indexes |
| B32 | Status: Available ⇄ OnRent (checkout / return), Available ⇄ InService (with a reason; back with an odometer that never goes down), Available/InService → Retired (with a reason, final). A car on rent can't change status | `vehicle.not_available`, `vehicle.on_rent`, `vehicle.not_in_service`, `vehicle.not_on_rent`, `vehicle.retired`, `vehicle.reason_required`, `vehicle.odometer_decreased` | `Vehicle` |
| B33 | **Capacity follows the fleet**: registering a car adds one to its category's capacity at its branch for every future day, retiring removes one, in the **same transaction** (pre-commit event). Retiring is refused if customers already hold that car | `inventory.capacity_below_reserved` | `FleetCapacityHandlers`, `FleetCapacity` |
| B34 | Every status change is in the car's logbook; the history merges logbook and rentals (with their inspection reports), newest first | | `Vehicle.Log`, `IFleetReadModel.GetHistoryAsync` |
| **Checkout and return** | | | |
| B35 | The agent picks the actual car: Available, at the pickup branch, in the booked category, or a higher one only as a **free upgrade** (the price never changes) | `rental.vehicle_wrong_branch`, `rental.category_mismatch` | `Rental.CheckOut` |
| B36 | **No keys without a completed checkout inspection** of that car for that booking; **no return without a completed return inspection**. The rental's odometer comes from the inspection | `rental.inspection_required`, `rental.wrong_vehicle` | `Rental.CheckOut`, `Rental.Return` |
| B37 | Return puts the car back to Available **at the return branch** (one-way moves it) with the inspected odometer | | `Rental.Return`, `Vehicle.ReturnFromRental` |
| **Inspections** | | | |
| B38 | One Checkout and one Return report per booking. Completing needs every checklist item answered, a note (≤ 300 chars) on Damaged/Missing items, the odometer and the fuel level (eighths 0-8) | `inspection.incomplete`, `inspection.note_required`, `inspection.note_too_long`, `inspection.invalid_fuel`, `inspection.invalid_odometer`, `inspection.invalid_checklist` | `InspectionReport.Complete` / `Update` |
| B39 | A checkout report also needs photos of the **front, back, left and right**, and the driver's ID and licence checked | `inspection.photos_required`, `inspection.driver_not_verified` | `InspectionReport.RequiredCheckoutAngles` |
| B40 | The return report is compared with the checkout report: anything newly Damaged/Missing and new damage marks become **New damage** | `inspection.checkout_report_required`, `inspection.rental_required`, `inspection.rental_mismatch` | `InspectionReport.Complete(now, checkoutReport)` |
| B41 | **A completed report is evidence: it is locked** (no edits, no new or removed files) | `inspection.locked` | `InspectionReport.EnsureDraft` |
| B42 | At most 20 files per report; JPEG, PNG or PDF by **magic bytes** (not the extension or Content-Type), ≤ 10 MB, not empty | `inspection.too_many_attachments`, `file.type_not_allowed`, `file.too_large`, `file.empty` | `InspectionReport.AddAttachment`, `FileUploadPolicy` |
| B43 | The customer sees only **completed** reports of **their own** booking and can acknowledge them; a draft or someone else's report or file is 404. Staff see drafts | `inspection.not_completed`, 404 | `InspectionQueries`, `IInspectionReadModel` |
| **Cancellation and no-show** | | | |
| B44 | An **agent** can cancel any Confirmed booking at any time, with a reason the customer sees; a **no-show** can be recorded from **pickup + 2h** and charges one day's rate (at most the booking total), releasing the rest of the hold. Both release the inventory | `reservation.cancel_reason_required`, `reservation.no_show_too_early`, `reservation.not_cancellable` | `Reservation.Cancel` / `MarkNoShow`, `OneDayRateNoShowFee`, `BookingRelease` |
| B45 | Who cancelled and why is recorded on the reservation (`CancellationDetails`); the customer sees "Cancelled by you" or "Cancelled by Roam" and the reason, never the agent's user id | | `CancellationDto` |
| **Pay at the branch** | | | |
| B46 | The customer chooses **Card** (the total is held on the card when booking) or **PayAtBranch** (nothing is taken online; the car is still reserved). A pay-at-branch booking sends no card token at all; a card booking must send one | `reservation.invalid_payment_option`, 400 `validation.failed` | `Reservation.PaymentOption`, `CreateReservationValidator` |
| B47 | **No keys for a pay-at-branch booking until the agent has taken the payment**: an Authorized hold for that booking | `rental.payment_required` | `Rental.CheckOut` |
| B48 | The counter payment holds the **booking total** on the card, **once**, only for a confirmed pay-at-branch booking. A declined card saves nothing, so the agent can try another card (one idempotency key per card tried). From then on it is the card path: capture total + late fee at return (B23, B25) | `payment.declined` (402), `payment.already_taken`, `payment.paid_online`, `reservation.not_payable` | `Reservation.EnsureCanTakeCounterPayment`, `TakeCounterPaymentHandler`, `PaymentIdempotencyKey.AuthorizeAtCounter` |
| B49 | Cancelling or a no-show of an **unpaid** pay-at-branch booking releases the car and charges nothing (there is no card to charge) | | `BookingRelease` |

**Every rule has a unit test in `tests/CarRental.Domain.Tests` named as a sentence.** A new rule
without a test is not done.

---

## 4. Security and privacy rules

- **Identity comes from the token, never the body.** Commands never accept `customerId`,
  `userId`, `email`, `loyaltyTier`, `dateOfBirth` for booking. The Application asks `ICurrentUser`.
- **Ownership is enforced in the query** (SQL filters by the signed-in user). Someone else's
  resource is **404, not 403**: never confirm it exists.
- Staff operations use `[Authorize(Roles = Roles.Agent)]`.
- **ID numbers are personal information (POPIA).** Return them only masked
  (`IdentityDocument.Masked`). Never log them. Never put them in URLs or query strings.
- No secrets in source control. `appsettings.Development.json` holds **local Docker-only**
  values; real environments use Key Vault or pipeline secrets.
- Demo accounts are created **only** when `Seed:DemoPassword` is configured (Development).
- **PCI DSS: card numbers, expiry dates and CVCs never enter the Domain, the database or the logs.**
  The browser sends them only to the gateway's hosted fields (`POST /api/payments/tokenize`, a mock
  in this PoC, deliberately outside MediatR) and books with the returned single-use token. We store
  the gateway reference and the card's brand + last four digits (`CardSummary`), nothing else.
  Never put card data in a command, a DTO, an exception message or a log line.
- Payment gateway calls take an **idempotency key** derived from the reservation id
  (`PaymentIdempotencyKey`). A new gateway operation needs its own key. The counter authorisation's key
  includes a **hash** of the single-use token, never the token itself.
- **Counter payments follow the same PCI rule as online ones:** the customer's card goes to the
  gateway's hosted fields (`/api/payments/tokenize`) from the staff screen, and the API only sees the
  token. In production this is a card terminal (card present) or the gateway's hosted fields on the
  agent's device; never a card number typed into one of our endpoints.
- Staff may see any customer's name and email at the counter, but still only **masked** ID numbers
  and card summaries.
- An agent registering a walk-in customer gets a **temporary password once** in the response.
  Never log it, never store it in clear, never send it to the client again.
- **Uploaded files are untrusted.** `FileUploadPolicy` checks the magic bytes (JPEG, PNG, PDF only,
  never SVG/HTML), the 10 MB limit and strips EXIF / PNG text chunks (GPS, device data). The stored
  name is a **random key** (`IFileStorage`), never the user's file name; the user's name is kept only
  as sanitised display metadata. Files are not in `wwwroot` and are served only through
  `GET /api/inspections/{id}/attachments/{attachmentId}`, which checks ownership/role and sends
  `Content-Disposition`, `X-Content-Type-Options: nosniff` and a restrictive CSP.
- Only Infrastructure touches the file system or cloud SDKs (`System.IO.File`, `Azure.*`), and only
  the Api reads `HttpContext` / `IFormFile` (architecture tests). Use cases get a `Stream` and the
  `IFileStorage` port.
- **Inspection reports are evidence**: never add an endpoint that edits or deletes a completed
  report or its files.

---

## 5. Design rules (client)

The visual design comes from the Stitch "Roam" design system.

- **Tokens:** use the Tailwind theme in `client/tailwind.config.js` (colours, type scale,
  radius, shadows). **No hard-coded hex colours or pixel font sizes in components.**
- **Components: three layers, and markup only at the bottom one.**
  1. `components/ui`: the component library (layout, typography, surfaces, forms, data, feedback;
     see `client/README.md`). The only place that writes Tailwind classes and raw HTML elements.
     Presentational only: no API calls, no routing, no business rules.
  2. Feature components (`components/booking`, `components/staff`, `components/inspection`,
     `components/fleet`) compose the library into domain pieces (`CategoryCard`, `BookingTable`,
     `InspectionStepper`…).
  3. Pages in `pages/` compose feature components and library primitives, and call the hooks.
  Pages and feature components **don't hand-write layout or typography**: no `<div className="flex …">`,
  no `text-…`/`bg-…` classes, no raw `<table>`/`<dl>`/`<h1>`. Use `Stack`/`Inline`/`Grid`/`SplitLayout`,
  `Heading`/`Text`/`Money`, `Card`/`CardHeader`/`CardSection`, `DataList`/`DataTable`, `Callout`…
  If the library can't express something, add a prop or a component to `components/ui` (with a
  story) instead of styling it in place. Kept hand-built on purpose: `CarIllustration`,
  `FuelGauge`, `AttachmentTile`, `BranchPicker` (a combobox) and the print-tuned checklist table in
  `InspectionReportView`.
- **Storybook:** every `components/ui` component and the main feature components have stories
  (`*.stories.tsx` next to them; sample data in `src/stories/fixtures.ts`). Add or update the story
  when you add or change a component. `npm run storybook` (port 6006); `npm run build-storybook`
  must pass.
- **Data:** components never call `fetch`. All API access goes through the TanStack Query hooks
  in `api/hooks/` (one file per API area, re-exported by `api/hooks/index.ts`; the DTO types are
  split the same way in `api/types/`): `useQuery` for queries, `useMutation` for commands, then invalidate what changed.
- **No business logic in the browser.** Prices come from `POST /api/quotes`; eligibility comes from
  the API's error codes. Client-side checks (SA ID checksum) are only for instant feedback; the
  server re-checks.
- **Money:** always `formatMoney()` → `R 1 350,00` with tabular figures (`.tabular`).
- **Every data view has four states:** loading (skeleton), empty, error (`Banner` with the API
  `code`, and Retry when retryable) and success.
- **Shapes:** cards and inputs `rounded-card` (10px); badges and chips `rounded-full`.
- **Accessibility:** real `<button>`/`<label>`, `role="switch"`/`role="radio"`/`role="combobox"`
  where used, visible focus, text alternatives for icons that carry meaning.
- **Icons:** Material Symbols via `<Icon name="…" />`. Vehicle images are the drawn
  `CarIllustration` silhouettes: no stock photos, no real people.
- **Copy:** short, plain and specific. Never promise a feature the backend doesn't have
  (refunds, invoices). Payments are a **mock gateway**: say "authorise / hold" at booking and
  "capture" at return, and label test cards as sandbox.
- **Card fields:** keep card details in component state only until the token comes back, then clear
  them. They go to `useTokenizeCard` and nowhere else (no query cache, no storage, no logs). The same
  `CardFields` component is used on the review page and at the counter.
- **Change dates after choosing a car:** the review page's "Your trip" card (`TripCard`) edits a local
  copy of the dates, checks them (pickup not in the past, return after pickup) and the chosen
  category's availability for the whole new period before "Update dates"; if it is sold out it offers
  "Choose another car for these dates" (back to results). Applying updates the draft, so the quote
  re-prices. The API still re-checks at booking.
- **Payment choice on the review page:** "Pay by card now" or "Pay at the branch". Same price either way;
  the pay-at-branch copy says what happens at pickup (hold on a card in the main driver's name).

### 5.1 Staff ("Roam Staff") design rules

- The staff app lives under `#/agent/*` with its own shell (`StaffLayout`): dark `inverse-surface`
  sidebar, "Roam Staff" wordmark, branch selector on top, no marketing hero.
- **The customer TopBar never links to staff pages, and the staff shell never links to customer
  booking pages.** The only bridge is "Staff sign-in" in the customer footer.
- Staff routes check `GET /api/account/me` for the Agent role and send everyone else to
  `#/agent/signin` (which signs a customer out with "This account is not a staff account").
  This is UX only: the API enforces `[Authorize(Roles = Roles.Agent)]` on every staff endpoint.
- **After sign-in or sign-up** (`useLandingAfterSignIn`): an Agent account goes to `#/agent` even from
  the customer sign-in page; a customer without a driver profile goes to the profile page first (then
  on to where they were going); everyone else continues to `next`. Signing in as a different user
  first clears every cached per-user query (profile, bookings, quote, account, staff data).
- Per-tab preferences (branch, day) live in `lib/staffPrefs.ts` (sessionStorage, wrapped in try/catch).
- Tables use `BookingTable` (four states, status and payment pills, monospace references); booking
  headers use `BookingSummary`.
- **Check-out and return go through `InspectionStepper`** (Checklist → Fuel & odometer → Photos →
  Review). It saves on every Next, so a report survives a refresh; the hand-over or close button
  only appears once the report is Completed. Photo inputs use `capture="environment"` so a phone or
  tablet at the counter opens the camera.
- Status pills: Confirmed (success), Checked out, **No-show** and **Cancelled / Cancelled by staff**
  (danger). Vehicle pills: Available, On rent, In service, Retired (`VehicleStatusPill`).
- **Pay at the branch at checkout:** after the inspection, a "Take the payment" step (`TakePayment`,
  reusing `CardFields`) holds the total on the card; "Hand over keys" appears only once it is paid.
  Staff tables and booking headers show "Pay at the branch · Not paid yet" (`PayAtBranchLine`) until then.
- Destructive staff actions (cancel, no-show, retire) always open a `Dialog`; cancel and retire
  require a reason before the confirm button enables.
- Mutations whose success navigates or swaps the view (hand over, close rental) use `mutateAsync`:
  the hook's invalidation can unmount the button before a per-call `onSuccess` runs.

### 5.2 Customer inspection report

- `InspectionReports` shows the completed checkout/return reports on the booking page
  (`InspectionReportView`: checklist table, fuel, odometer, damage, photo gallery with lightbox).
  New damage is a danger callout at the top. Photos load through the authorised API as object URLs,
  never as public links.
- "Print / Save as PDF" is `window.print()` with the `@media print` rules in `styles.css` (A4, no
  navigation); mark screen-only controls `print:hidden`.

---

## 6. Testing rules

| Layer | Project | Style |
|---|---|---|
| Domain rules | `CarRental.Domain.Tests` | Pure unit tests, fixed dates (`TestData.Now`), no mocks |
| Use cases | `CarRental.Application.Tests` | Real handlers + in-memory **fakes** (`Fakes/`); NSubstitute **mocks** only to verify an interaction |
| Architecture | `CarRental.ArchitectureTests` | Dependency rule, one handler per request, no public setters, type-usage rules (`HttpContext`/`IFormFile` only in Api; `System.IO.File`, `Azure.*` only in Infrastructure; no `DateTime.Now` in use cases) |
| Database + HTTP | `CarRental.IntegrationTests` | Testcontainers SQL Server + MongoDB, `WebApplicationFactory<Program>` |

- Test names are sentences: `Odometer_cannot_go_backwards`.
- Never depend on the real clock or today's date in unit tests.
- Concurrency claims need an integration test against real SQL Server (`OverbookingTests`).
- Write the failing test first for every new rule (TDD).

---

## 7. Checklist: adding a feature

1. Business rule? Add it to the **Domain** with a test, and to the table in §3.
2. New use case? Create `Application/<Area>/<UseCase>/` with the command/query, validator and handler.
3. Needs data or a service from outside? Add a **port** in `Application/Abstractions` and an
   adapter in Infrastructure, registered in `AddInfrastructure`.
4. New persisted state? Add or extend the EF configuration (owned types, rowversion, indexes,
   CHECK constraints) and plan a migration.
5. Expose it: a thin controller action with `[Authorize]` as needed. Map any new exception type in
   `ApiExceptionHandler`.
6. New error code? Add its friendly message in `client/src/api/client.ts`.
7. Client: types in `api/types/<area>.ts`, hook in `api/hooks/<area>.ts`, a page or component built from `components/ui`, all four states.
8. Tests at each layer touched; `dotnet test` and `npm run build` green.
9. Update `README.md` (API table), this file (rules), and `docs/` if the architecture changed.

## 8. Do not

- Add packages to the Domain.
- Upgrade MediatR past 12.x (v13+ is commercially licensed).
- Return EF entities or aggregates from controllers; return DTOs.
- Read `HttpContext` (or `IFormFile`) outside the Api project, or `System.IO.File` / cloud SDKs outside Infrastructure.
- Catch `DomainException` in handlers to hide it; let `ApiExceptionHandler` map it.
- Put personal notes or secrets in the repository.
- Accept a card number anywhere except the gateway's hosted fields (the mock tokenize endpoint).
- Trust a file's extension or Content-Type, store it under the user's file name, or serve it from `wwwroot`.
- Edit or delete a completed inspection report.

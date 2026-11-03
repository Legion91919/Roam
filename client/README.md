# Roam client (React + TypeScript + TanStack Query + Tailwind)

The booking front end for the Car Rental API. The visual design was made in Google Stitch and
rebuilt here as typed, reusable React components. Design tokens (colours, type scale, spacing)
live in one place: [`tailwind.config.js`](tailwind.config.js).

```bash
npm install
npm run dev      # http://localhost:5173, proxies /api to the .NET API on :5080
npm run build    # type-check + production build
npm run storybook        # component library catalogue on http://localhost:6006
npm run build-storybook  # static Storybook in storybook-static/ (CI-friendly)
```

## The component library

The app is built in three layers, and only the bottom one writes HTML and Tailwind classes:

1. **`components/ui`: the Roam component library.** Presentational only (no API calls, no routing).
2. **Feature components** (`components/booking`, `staff`, `inspection`, `fleet`) compose the library
   into domain pieces: `CategoryCard`, `BookingTable`, `InspectionStepper`, `VehicleFacts`…
3. **Pages** (`pages/`) compose feature components and library primitives and call the hooks.
   Pages contain no `className` at all.

| Group | Components |
|---|---|
| Layout | `Stack` (column, row or responsive; gap, align, justify), `Inline` (wrapping row), `Grid` / `GridItem` (responsive columns), `SplitLayout` (main + aside), `Spacer` |
| Typography | `Heading`, `Text` (size, tone, mono, tabular, icon), `Eyebrow`, `Money` (R 1 350,00), `TextLink`, `BackLink`, `Icon` |
| Surfaces | `Card` (padding, tone, gap, width), `CardHeader`, `CardSection`, `CardFooter`, `IconBadge`, `LinkCard`, `Dialog` |
| Actions | `Button` (primary, secondary, tonal, ghost, danger; sm/md/lg; loading), `CopyButton` |
| Forms | `Field`, `Input`, `Select`, `Textarea`, `RadioCards`, `CheckboxCard`, `ToggleField`, `Toggle`, `Segmented`, `ChipGroup`, `TileInput`, `UploadTile`, `FileButton`, `FormActions` |
| Data | `DataList` (label/value grids or rows), `DataTable` (loading, empty, error and success states), `EmptyState`, `Pagination`, `Timeline`, `CheckList`, `BulletList`, `Pill`, `Steps` |
| Feedback | `Banner` (API error with its code and Retry), `Callout`, `QueryState` / `QueryError`, `Skeleton`, `SkeletonStack`, `Spinner` |

Rules of thumb:

- Need a layout or a text style a page doesn't have? Add a prop or a component to `components/ui`
  (with a story), don't style it in place.
- Spacing comes from the `gap` scale (4px steps), colours and type from `tailwind.config.js` tokens.
- Primitives pass native attributes through (`aria-*`, `data-testid`, `onSubmit`), so a `Stack as="form"`
  or a `Card data-testid="…"` works as expected.
- A few things stay hand-drawn on purpose: `CarIllustration`, `FuelGauge`, `AttachmentTile`,
  `BranchPicker` (a keyboard combobox) and the printable checklist table in `InspectionReportView`.

### Storybook

Every library component has stories next to it (`components/ui/*.stories.tsx`, grouped as
*Library/Layout, Typography, Surfaces, Actions, Forms, Data, Feedback*), and the main feature
components have their own (*Booking*, *Staff*, *Inspection*). Sample data lives in
`src/stories/fixtures.ts`; stories never call the API. Use the toolbar to switch to the
Phone (390) / Tablet / Desktop viewports, and the Accessibility tab (axe) to check a component.

## Structure

```
src/
├── api/
│   ├── client.ts         ← fetch wrapper; ProblemDetails -> ApiError(code); error copy per code
│   ├── types/            ← TypeScript mirrors of the API DTOs, one file per API area:
│   │                       booking, customer, payments, staff, fleet, inspections, common
│   └── hooks/            ← TanStack Query hooks: the ONLY place the UI talks to the API.
│                           Same areas, plus queryKeys.ts (shared cache keys)
│                           Both folders have an index.ts: import from '../api/types' / '../api/hooks'
├── components/
│   ├── ui/               ← the component library (see above) + *.stories.tsx
│   └── booking/          ← feature components built from the primitives:
│   │                       SearchCard, CategoryCard, PriceBreakdown, ExtrasList, BookingReference,
│   │                       PaymentCard / PaymentLine, InspectionReports (customer report cards)…
│   ├── inspection/       ← InspectionStepper (checklist → fuel & odometer → photos → review),
│   │                       InspectionReportView (read-only, printable), FuelGauge, AttachmentTile
│   │                       ChecklistStep, ReadingsStep, PhotosStep, ReviewStep (one file per step)
│   ├── fleet/            ← VehicleParts (status pill, facts, actions), VehicleHistory, FleetForms
│   └── staff/            ← StaffLayout (dark sidebar + branch selector), BookingTable, BookingSummary,
│                           BookingActions (cancel with reason, no-show)
├── stories/fixtures.ts   ← sample data for Storybook only
├── pages/                ← one per screen; compose components and call hooks (no className)
│   ├── SearchPage        (screen 1)
│   ├── ResultsPage       (screen 2: availability, filters, loading/empty states)
│   ├── SignInPage        (sign in / create account)
│   ├── ProfilePage       (screen 3: driver, SA ID checked as you type, DOB filled from the ID)
│   ├── ReviewPage        (screen 4: verified driver, extras, live quote, terms, book)
│   ├── ReservationPage   (confirmed / checked out / cancelled / no-show, payment line, inspection reports)
│   ├── MyBookingsPage    (the signed-in customer's bookings)
│   └── staff/            ← "Roam Staff" (#/agent/*): StaffApp (role guard), StaffSignInPage,
│                           TodayPage, CheckoutPage, ReturnsPage, FindBookingPage, AddCustomerPage,
                          FleetPages (FleetPage, AddVehiclePage, VehiclePage with history)
└── lib/                  ← auth session, formatting (R 1 350,00), SA ID check, card checks, booking draft,
                            hash router, staffPrefs (branch/day in sessionStorage)
```

## How it maps to the backend (CQRS on both sides)

| Screen action | Hook | API | Side |
|---|---|---|---|
| Sign in / register | `useAuth` (lib/auth.tsx) | `POST /api/auth/login`, `/register` | Identity |
| Branch list | `useBranches` | `GET /api/branches` | query |
| Search results | `useAvailability` | `GET /api/availability` | query |
| Driver profile | `useProfile` / `useSaveProfile` | `GET` / `PUT /api/customers/me` | query / command |
| Terms | `useTerms` | `GET /api/terms/current` | query |
| Live price | `useQuote` | `POST /api/quotes` | query (nothing saved) |
| Change dates on the review page | `useAvailability` (for the new dates) | `GET /api/availability` | query; applying the dates re-runs `useQuote` |
| Card → token | `useTokenizeCard` | `POST /api/payments/tokenize` | mock hosted fields (the only call that sees a card number) |
| Authorise and book | `useCreateReservation` | `POST /api/reservations` (`paymentOption: 'Card'` + `paymentToken`, or `'PayAtBranch'` and no token) | command, then invalidates availability |
| My bookings | `useMyReservations` | `GET /api/reservations/mine` | query |
| Confirmation / cancelled | `useReservation` | `GET /api/reservations/{id}` | query |
| Cancel (confirm dialog) | `useCancelReservation` | `POST /api/reservations/{id}/cancel` | command (voids the hold), then invalidates the reservation |
| Inspection reports | `useMyInspections` | `GET /api/reservations/{id}/inspections` | query (completed reports only) |
| "I've checked this report" | `useAcknowledgeInspection` | `POST /api/reservations/{id}/inspections/{inspectionId}/acknowledge` | command |
| Report photos | `useAttachmentUrl` | `GET /api/inspections/{id}/attachments/{attachmentId}` | authorised blob → object URL |

### Staff area (`#/agent/*`, Agent role)

| Screen | Hook | API | Side |
|---|---|---|---|
| Role check after sign-in | `useAccount` | `GET /api/account/me` | query |
| Today / Check out list | `useBranchPickups` | `GET /api/agent/branches/{id}/pickups?date` | query |
| Today / Returns list | `useBranchReturns` | `GET /api/agent/branches/{id}/returns?date` | query (`isLate` from the server) |
| Find booking, forms | `useCounterBooking` | `GET /api/agent/reservations/{reference}` | query |
| Choose the car | `useAvailableVehicles` | `GET /api/agent/vehicles/available-for/{reservationId}?includeUpgrades` | query |
| Inspection stepper | `useStartInspection`, `useInspection`, `useUpdateInspection`, `useUploadAttachment`, `useRemoveAttachment`, `useCompleteInspection` | `/api/agent/inspections…` | commands (saved on every Next) / query |
| Take the payment (pay at the branch) | `useTokenizeCard` → `useTakeCounterPayment` | `POST /api/payments/tokenize`, then `POST /api/agent/reservations/{id}/payment` | command (holds the total), invalidates `['agent']` |
| Hand over keys | `useCheckOut` | `POST /api/rentals` `{ reservationId, vehicleId, freeUpgrade }` | command, invalidates `['agent']` |
| Take return | `useReturnVehicle` | `POST /api/rentals/{id}/return` (no body) | command (late fee + capture), invalidates `['agent']` |
| Cancel booking / no-show | `useAgentCancel`, `useMarkNoShow` | `POST /api/agent/reservations/{id}/cancel` `{ reason }`, `/no-show` | commands |
| Fleet list | `useVehicles` | `GET /api/agent/vehicles?…` | query (paged, keeps previous page while loading) |
| Vehicle + history | `useVehicleHistory` | `GET /api/agent/vehicles/{id}/history` | query |
| Add vehicle / status | `useRegisterVehicle`, `useVehicleAction` | `POST /api/agent/vehicles`, `/{id}/service`, `/service/return`, `/retire` | commands |
| Add customer | `useRegisterCustomer` | `POST /api/agent/customers` | command (temporary password shown once) |

The staff shell never links to customer booking pages, and the customer TopBar never links to
the staff area (only the footer's "Staff sign-in").

Pricing is never re-implemented in the browser: the breakdown shows the lines returned by the
server's `IPriceRule` pipeline. Error banners show the API's error `code`
(`inventory.sold_out`, `concurrency.conflict`, `eligibility.under_age`…), so you can see which
backend rule fired.

Printing: the customer's report card has "Print / Save as PDF" (`window.print()`); the
`@media print` rules in `styles.css` lay it out on A4 and hide everything marked `print:hidden`.

Fonts (Inter, JetBrains Mono, Material Symbols) are self-hosted from npm: no runtime calls to
Google Fonts.

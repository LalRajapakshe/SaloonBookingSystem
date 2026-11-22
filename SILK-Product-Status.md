# SILK product status

Read-only audit of the customer app, staff app, and ASP.NET API on 20 Nov 2026. Database figures are a live SELECT against `saloonDB` on `NAGSERVER`. No records were changed.

**Not ready to finalize.** The salon operating core is built: login, catalogue, customer booking, and staff scheduling. The product still depends on sample content for promotions, history, dashboard, and revenue, and it has no registration, customer status sync, notifications, or payments. A separate stylist app is not required if the existing staff app hides modules the signed-in role cannot use.

| Check | Result |
| --- | --- |
| Backend tests | 84/84 passed |
| Customer tests | 59/59 passed |
| Staff tests | 112/115 passed |
| Appointments in saloonDB | 13 |

Status labels:

- **COMPLETED** means implemented and verified in this audit.
- **IMPLEMENTED** means the code and automated tests exist, without a fresh live walkthrough.
- **PARTIAL** means only some of the behavior exists.
- **NOT IMPLEMENTED** means it is absent.
- **BLOCKED** means usable code is waiting on data.

No feature in this audit is marked COMPLETED, because this pass did not click through the running apps.

## Appointments currently stored

Source: `saloonDB.Appointments`, read 20 Nov 2026. All 13 rows are on branch Negombo. Counts are rows, not revenue.

| Status | Rows |
| --- | ---: |
| Pending | 5 |
| Confirmed | 7 |
| Checked in | 1 |

## Branch coverage

Active, non-deleted rows. Kurunagala and Galle can appear in the catalogue but have no stylists, so a customer booking there cannot pass stylist eligibility.

| Branch | Visible services | Active employees | Active customers |
| --- | ---: | ---: | ---: |
| Negombo | 13 | 2 | 7 |
| Kurunagala | 4 | 0 | 0 |
| Galle | 4 | 0 | 0 |

## Customer application

Package `salon_customer_app` at `D:\Projects\salon_customer_app`. The on-screen name is Lumière. API base is `http://localhost:5041`. Login is required at startup.

| Feature | Status | API | Evidence |
| --- | --- | --- | --- |
| Registration and authentication | PARTIAL | `POST /api/Auth/login` | `login_screen.dart`, `customer_auth_api.dart`. No registration. Token stays in memory. |
| Customer validation | PARTIAL | Login response `userCategory` | Rejects non-customer profiles. No separate validate endpoint. |
| Profile retrieval and update | PARTIAL | None after login | Name comes from login. Contact rows are `MockCatalog.customer`. Edit is blocked. |
| Branch discovery | PARTIAL | `GET /api/CustomerCatalogue/branches` | Live after sign-in. Booking uses the service salon, not a separate picker. |
| Categories and catalogue | IMPLEMENTED | `GET .../branches/{id}/services` | Live when signed in. Guest path is `MockCatalog`. |
| Service details, price, duration | IMPLEMENTED | Included in catalogue payload | `service_detail_screen.dart` |
| Stylist listing and selection | IMPLEMENTED | `GET .../services/{id}/stylists` | Live with the catalogue. Any-stylist option is local. |
| Stylist qualifications and profiles | PARTIAL | qualifications on stylist payload | Shown in the booking step. No profile screen. Qualifications table is empty. |
| Appointment availability | PARTIAL | `GET /api/CustomerCatalogue/availability` | Live times when catalogue is live. Dates still come from `MockAvailability`. |
| Appointment request | IMPLEMENTED | `POST /api/CustomerAppointments` | Saves Pending when signed in with a live catalogue. Otherwise a local id only. |
| Persistence in the database | IMPLEMENTED | `POST /api/CustomerAppointments` | Code and SQLite test exist. `saloonDB` currently holds 5 Pending rows. This audit did not insert a new row. |
| Booking history | PARTIAL | None | `my_bookings_screen.dart` reads memory plus `SampleAppointments.seed`. |
| Cancellation | NOT IMPLEMENTED | None in the customer app | Staff API has `POST /api/Appointments/{id}/cancel`. |
| Status and notifications | PARTIAL | None | Status pill only. Reminder switch is not saved. No email, SMS, or push sender. |
| Profile and settings | PARTIAL | Sign-out only | Payments, addresses, and edit profile are preview copy. |
| Promotions | PARTIAL | None | `MockCatalog.heroPromotion` and offers on the home screen. |
| Wishlist | PARTIAL | None | In-memory look ids over `InspirationCatalog`. |
| Beauty Inspiration Gallery | IMPLEMENTED | None | Local assets and `inspiration_catalog.dart`. Not a salon catalogue. |
| AI Hair Studio | PARTIAL | External `HAIR_STUDIO_ENDPOINT` | Styles are bundled. Transform runs only if that dart-define is set. |
| Smart Booking Calendar | PARTIAL | None | `DemoAvailability` and `MockCatalog`. Explicitly not the live booking tab. |

## Staff application

Package `salon_booking_mobile` at `D:\Projects\salon_booking_mobile`. There is no router. Login replaces the screen with the dashboard. Signed-out screens keep sample catalogues.

| Feature | Status | API | Evidence |
| --- | --- | --- | --- |
| Authentication and permissions | IMPLEMENTED | `POST /api/Auth/login` | Permissions come from the login profile. No sign-out control. |
| System administrator | PARTIAL | `platform.manage` gates Add Branch | No platform console. Scheduling asks for a branch when the token has none. |
| Salon administrator | PARTIAL | Branch-scoped JWT | No dedicated salon-admin module. Quick actions are not permission-filtered. |
| Tenant management | NOT IMPLEMENTED | API exists, UI does not | Dashboard Tenant tile is coming soon. Login only sends `tenantCode`. |
| Branch management | IMPLEMENTED | `GET/POST/PUT/DELETE /api/Branch` | Branch form includes customer visibility and photo. |
| Service categories | IMPLEMENTED | `/api/ServiceCategories` | Live when signed in. |
| Services | IMPLEMENTED | `/api/Services` | CRUD, price, duration, visibility, popular flag, photo. |
| Employees | IMPLEMENTED | `/api/Employees` | CRUD and photo. Unsigned list is a sign-in message. |
| Specialisations | PARTIAL | `GET/POST /api/EmployeeCapabilities` | Assign exists. The API also has PUT and DELETE, which the app does not call. |
| Qualifications | IMPLEMENTED | `/api/Qualifications` | UI exists. `saloonDB` has zero qualification rows. |
| Employee, branch, service photos | IMPLEMENTED | `POST/GET/DELETE .../photo` | Catalogue photo editor. |
| Publishing controls | IMPLEMENTED | `isCustomerVisible` on branch, service, employee | Stored and edited in the staff forms. |
| Working schedules | PARTIAL | `GET/POST /api/StaffSchedules` | Working hours only. Breaks and leave have an API and no staff screen. |
| Appointment management | IMPLEMENTED | `GET/POST/PUT /api/Appointments` | Salon-wide users can list without a branch. Verified by SQLite test, not by a fresh UI pass. |
| Approval and assignment | IMPLEMENTED | `POST .../schedule` and `.../status` | Scheduling board and reception confirm/check-in. |
| Cancellation and rescheduling | IMPLEMENTED | DELETE service line; POST schedule | Line cancel is on the edit screen. Whole-appointment cancel API is not wired in the app. |
| Dashboard and reporting | PARTIAL | None | Dashboard KPIs and the revenue screen use `mock_revenue_data`. |
| Users and permissions | IMPLEMENTED | `/api/Users`, `/api/Roles`, `/api/Permissions` | Shown only when the token has the matching read permission. |
| Customers and reception | IMPLEMENTED | `/api/Customers`; reception uses appointments | Live when signed in. Unsigned reception is a sample day. |

## Backend

`D:\Projects\SaloonBooking`. .NET 9 projects: Domain, Application, Infrastructure, Persistence, API. SQL Server in production. Tests use SQLite in memory, so they do not touch `saloonDB`.

| Area | Status | Evidence |
| --- | --- | --- |
| JWT login and current profile | IMPLEMENTED | `POST /api/Auth/login`, `GET /api/Auth/me` |
| Permission policies | IMPLEMENTED | `RequiresPermission` plus `PermissionAuthorizationHandler` |
| Tenant and branch isolation | IMPLEMENTED | `SalonBookingDbContext` global filters |
| Platform administrator | IMPLEMENTED | `GET /api/Platform/tenants`, `POST .../salon-administrator` |
| Tenant CRUD | IMPLEMENTED | `/api/Tenant`. No staff UI. |
| Catalogue and visibility | IMPLEMENTED | `/api/CustomerCatalogue` requires a customer JWT |
| Staff availability | IMPLEMENTED | `GET /api/Availability` |
| Customer booking | IMPLEMENTED | `POST /api/CustomerAppointments` always stores Pending |
| Appointment lifecycle | IMPLEMENTED | Pending, Confirmed, CheckedIn, InProgress, Completed, Cancelled, NoShow |
| Schedules and leave | IMPLEMENTED | `/api/StaffSchedules` and `/api/StaffLeaves` |
| Notifications | NOT IMPLEMENTED | No email, SMS, push, or SignalR sender |
| Payments and invoices | NOT IMPLEMENTED | Empty `Payment` and `Invoice` classes. Not mapped in the database. |
| Error handling | PARTIAL | Known scheduling and auth exceptions become JSON. Other exceptions are unmapped. |

## Database readiness

`saloonDB` on `NAGSERVER`. Implementation is separate from this configuration. Empty tables mean the feature can exist in code and still be unusable.

| Record | What is stored | Readiness |
| --- | --- | --- |
| Tenants | ABC saloon (`TEN000001`) and Prasad (`TEN000002`), both active | Prasad has no branches |
| Branches | Negombo, Kurunagala, and Galle, all customer-visible. Negombo and Kurunagala are both marked head office | Data issue |
| Services | 21 active services, all customer-visible, spread across the three branches | Usable |
| Employees | 2 active visible stylists at Negombo. 3 older rows are deleted | Other branches are blocked |
| Customers | 7 active customers, all on Negombo. 3 deleted | Usable for one branch |
| Appointments | 5 Pending, 7 Confirmed, 1 CheckedIn. No Completed, Cancelled, or NoShow | History is thin |
| Skills and hours | 4 active employee-service links. 7 working-schedule rows. 0 leave rows. 0 qualifications | Qualifications and leave are empty |
| Accounts | 1 general user with no branch, 1 platform administrator with a branch, 1 customer. 6 roles | Salon-wide list depends on the no-branch path |

## Booking path across the three apps

| Stage | Status | What happens |
| --- | --- | --- |
| Customer signs in | IMPLEMENTED | `POST /api/Auth/login`. Customer category is required. |
| Catalogue loads | IMPLEMENTED | CustomerCatalogue branches, services, and stylists. Guest mode stays on `MockCatalog`. |
| Time is checked | PARTIAL | Live availability when ids are numeric. The date strip is still sample data. |
| Booking is saved | IMPLEMENTED | `POST /api/CustomerAppointments` writes Pending and a preferred time. It does not reserve the slot. |
| Staff sees it | IMPLEMENTED | `GET /api/Appointments`. A user with no branch now lists the whole tenant. Live screen was not reopened in this audit. |
| Staff assigns a slot | IMPLEMENTED | Scheduling board posts the service-line schedule. Find availability on that board is still mock data. |
| Reception advances status | IMPLEMENTED | Confirm and check-in call `POST /api/Appointments/{id}/status`. |
| Customer sees the update | NOT IMPLEMENTED | No customer history endpoint and no notification sender. |

## Sample data still on screen

| Source | Where it still appears |
| --- | --- |
| `mock_catalog.dart` | Guest catalogue, home promotions, profile contact, smart calendar |
| `mock_availability.dart` | Booking dates, and times whenever the catalogue is not live |
| `sample_appointments.dart` | Seeded past visit in My Bookings |
| `inspiration_catalog.dart` and `hair_catalog.dart` | Gallery, wishlist, and AI studio styles |
| `demo_availability.dart` | Smart calendar only |
| `mock_appointment_data.dart` | Staff appointments, reception, and create form while signed out |
| `mock_scheduling_data.dart` | Staff schedule demo and Find availability dialog even when signed in |
| `mock_revenue_data.dart` | Revenue dashboard and the summary cards that use it |

## Backend without a staff screen

- Tenant CRUD at `/api/Tenant` and platform tenant assignment at `/api/Platform`.
- Staff leave at `/api/StaffLeaves`. The dashboard tile says coming soon.
- Whole-appointment cancel at `POST /api/Appointments/{id}/cancel`. The app cancels a service line instead.
- Employee capability update and delete. The app only lists and creates assignments.
- `GET /api/Auth/me`. Neither app calls it after login.
- `GET /api/test`. It only proves a token works and should not ship.

## Build and tests

| Check | Result |
| --- | --- |
| Backend `dotnet test` | Passed 84, failed 0, skipped 0. SQLite only. Nullable warnings in service classes. Build succeeded. |
| Customer `flutter test` | All 59 tests passed. |
| Customer `flutter analyze` | No issues found. |
| Staff `flutter test` | 112 passed and 3 failed, all in `dashboard_modules_test.dart`. The test expects seven system cards, and a session without permissions only renders Tenant and Setting. The phone-width case also timed out after 10 minutes. |
| Staff `flutter analyze` | 27 issues, no errors. Warnings include an unused import and a dead null check. The rest are info-level deprecations. |

## Security and production gaps

- `appsettings.json` leaves the SQL connection and JWT secret empty. The API starts only when those values are supplied outside the file.
- Both apps call `http://localhost:5041`. There is no production host, certificate trust step, or stored customer session.
- The customer catalogue is not public. It requires a customer token, which is correct, and it also means a guest cannot browse live salons.
- Quick actions on the staff dashboard are visible to every signed-in user. Permission checks cover the system-module cards, not those shortcuts.
- `ActiveAccountMiddleware` rewrites tenant and branch from the database, which limits forged claims. Unhandled exceptions are not mapped to a safe public body.

## Remaining work, in order

| Order | Task | Why |
| --- | --- | --- |
| 1 | Customer history and status refresh | The loop stops after the salon changes a booking. |
| 2 | Remove sample dates from the live booking flow | A saved time can sit on a sample date strip. |
| 3 | Staff the other branches or hide them | Kurunagala and Galle have services and no stylists. |
| 4 | Permission-filter staff navigation | This is what lets a stylist use the same app safely. |
| 5 | Replace dashboard and revenue mocks | Those screens do not read `saloonDB`. |
| 6 | Registration, profile edit, and persisted login | Customers cannot create or keep an account in the app. |
| 7 | Notifications, then payments | Neither has a sender or a mapped table. |
| 8 | Fix the three staff dashboard tests | They still expect every system card, including ones hidden without permission. |

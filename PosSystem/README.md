# POS System — Project Setup

## 1. Create the project (on your Windows dev machine, in Visual Studio)

- New Project → **WPF App (.NET)** → name it e.g. `PosSystem`
- Target framework: **.NET 8** (long-term support, fast, fine on old hardware — the runtime is what's heavy, not your code)

## 2. NuGet packages to install

| Package | Why |
|---|---|
| `Microsoft.Data.Sqlite` | Local embedded database — no install needed on the till machine |
| `Dapper` | Lightweight SQL mapping — faster and simpler than EF Core for a small schema like this |
| `LiveChartsCore.SkiaSharpView.WPF` | Real-time charts for the dashboard, WPF-native |
| `MailKit` | Sends the monthly report email over SMTP |

Skip Entity Framework for v1 — it adds startup overhead that matters on old CPUs, and Dapper + raw SQL is plenty for a schema this size.

## 3. Folder structure

```
PosSystem/
  Models/          <- drop Models.cs here
  Data/            <- schema.sql + a DbInitializer.cs that runs it on first launch
  Views/           <- WPF windows/pages (CheckoutView, DashboardView, MenuEditorView)
  ViewModels/       <- MVVM logic per view
  Services/
    OrderService.cs      <- create orders, mark synced
    SyncService.cs        <- push unsynced orders to central server on connectivity
    ReportService.cs      <- monthly PDF/HTML summary + email send
  App.xaml
```

## 4. Files included here

- `schema.sql` — run this against a new SQLite file on first launch to create the local DB
- `Models.cs` — POCOs matching the schema exactly, ready to drop into `Models/`

## 5. Build order (recommended)

1. **DB + models wired up** — get `schema.sql` running and `Models.cs` reading/writing via Dapper. Confirm you can insert and read back an Order with Items.
2. **Checkout screen** — product grid → tap to add → cart → total → cash/card toggle → save Order. This is the single most-used screen; get it fast and simple before anything else.
3. **Sold-out toggle** — a long-press or small toggle on each product tile flips `IsAvailable`. Trivial once checkout works, but core to daily use.
4. **Dashboard** — wire an event on order-save that pushes the new total into LiveCharts data directly (don't poll). Start with: today's revenue, top 5 items, cash vs card split.
5. **Sync service** — background task that, when internet is detected, pushes all `Orders WHERE SyncedAt IS NULL` to a central server (a simple hosted API + Postgres works fine) and stamps `SyncedAt`. This is also what powers a multi-truck owner dashboard later.
6. **Monthly report** — scheduled task queries last 30 days, builds a simple HTML/PDF summary, emails it via MailKit/SMTP.

Everything past step 4 can wait until step 1–4 are solid and one real truck is using it — better to have a rock-solid checkout + dashboard than a half-working sync layer.

# PosSystem — Skeleton

The old UI is gone entirely. This is a clean two-project split: a pure backend
library with zero UI dependency, and an empty WPF shell to build the modern
UI into from scratch.

## Structure

```
PosSystem.sln
PosSystem.Core/            <- class library, NO WPF/UI references at all
  Models/                  <- data classes (Customers, Bills, Goods, Sells, etc.)
  Data/                    <- SQLite data access (renamed from Manager/Database)
    Server.cs              <- connection string + CreateDatabase/CreateTable helpers
  packages.config           <- only System.Data.SQLite, nothing else

PosSystem.App/              <- WPF shell, currently just a blank window
  App.xaml / App.xaml.cs
  MainWindow.xaml            <- proves the theme + Core reference both work, nothing else
  Theming/
    ThemeManager.cs           <- swaps Colors.Light/Dark.xaml at runtime (ThemeManager.Toggle())
  Themes/
    Colors.Light.xaml         <- real palette, generated from seed #6C4CE0 (Material Design 3 Fidelity scheme)
    Colors.Dark.xaml          <- same tokens, dark variant, same seed
    Typography.xaml           <- type scale (placeholder font, real sizes)
  ViewModels/
    ViewModelBase.cs          <- INotifyPropertyChanged base for every screen ViewModel
    RelayCommand.cs            <- ICommand implementation for button bindings
  Views/                      <- empty, this is where real screens go
  rovaShop.db                 <- existing database, copied to output on build
```

## What happened to the old code

- **Every `.xaml` / `.xaml.cs` file from the old UI is gone** — no MainWindow, POS, Customers, Inventory, Bank, etc. screens survived. That's intentional; we're rebuilding all of it.
- **`Manager/Database/*.cs` → `PosSystem.Core/Data/*.cs`** — kept as-is, just renamed and moved into a real class library. This is the SQLite CRUD layer (`InsertCustomers`, `ReadCustomers_Range`, etc.) and it still works exactly the same way.
- **`Models/*.cs` → `PosSystem.Core/Models/*.cs`** — kept as-is. `Customers.Paid`/`Remain`, `Bills.Paid`/`Remain`/`Tax`/`Discount`, all still there for the debt-tracking feature.
- **Firebase (`FireSharp`) is gone** — it only existed inside the UI files we deleted (`Inventory.xaml.cs`, the two `POS_*_UserControl.xaml.cs` files). The backend layer never touched it, so deleting the UI resolved that leaked-credential problem automatically. No cloud sync exists right now — that's a deliberate gap to fill later with our own service, not the old one.
- **MindFusion, Office.Interop.Excel** — left out of the new `packages.config` entirely; add back only if/when actually needed.

## Opening this in Visual Studio

1. Open `PosSystem.sln`
2. Right-click the solution → **Restore NuGet Packages** (pulls down `System.Data.SQLite` for Core)
3. Set **PosSystem.App** as the startup project
4. Build & run — you should get a blank window with "PosSystem — skeleton running" in the middle. That confirms the Core reference, the theme resource dictionaries, and the SQLite package are all wired correctly before any real screen exists.

## Next steps (in order)

1. ~~**Verify Core against real data**~~ — done. `MainWindow.xaml.cs` now has a `RunCoreDataSmokeTest()` call that reads `goods` and `customers` through the Core data layer and prints row counts + total outstanding debt to the window on launch.
2. ~~**Design the real color palette**~~ — done. See "Color palette" below for the seed, the scheme used, and why.
3. **Build the app shell/navigation** — a left sidebar (Checkout, Customers, Inventory, Dashboard) is the natural Material-style layout for a POS. This replaces the old app's custom-drawn menu rectangle entirely.
4. **Build Checkout first** — highest-traffic screen, and the one that most needs to look and feel modern since it's what the client's staff sees all day.
5. **Build Customers/Debt screen second** — this is the feature the skincare client specifically asked for, and the data model already supports it. Seeded test data (see below) is now in place to build and eyeball this screen against before real client data arrives.

## Color palette

Seed color: `#6C4CE0` (bold violet). Generated with `materialyoucolor`, the real
HCT-based engine behind Android's dynamic color — not hand-picked hex values —
using the **Fidelity** scheme variant, which keeps Primary true to the seed
color instead of muting it like Material's default scheme does.

Design decision: **calm/neutral surfaces, violet reserved for things staff
need to notice and act on** (buttons, totals, active nav state) rather than
saturated purple everywhere. On cheap, uncalibrated store monitors, a fully
saturated UI causes eye fatigue over a full shift; restrained neutrals with a
confident accent color hold up better across 8–10 hour days and scale cleanly
across different client verticals (cafes, food trucks, retail) without a
redesign per client.

The algorithm also auto-derived a warm amber/orange tertiary color
(`#7C3F00` light / `#FFB781` dark) as the seed's complement — earmarked for
discount badges, low-stock flags, or "card payment" state, so there's a second
accent ready without introducing an arbitrary second color.

`Colors.Light.xaml` and `Colors.Dark.xaml` define the identical set of brush
keys (`PrimaryBrush`, `SurfaceBrush`, `OnSurfaceVariantBrush`, etc.) — every
screen should bind only to these keys, never a raw hex, so
`ThemeManager.Toggle()` (wired to a throwaway button in `MainWindow` right
now) repaints the whole app with no per-screen logic. Startup default is
Light (POS counters are usually in bright rooms); Dark is available via the
toggle.

## Seeded test data

The original `rovaShop.db` had 0 rows in `customers`, `bills`, and `sells` — no way to build or eyeball the debt screen. Seeded 8 test customers with mixed states, plus 5 matching bill rows for the first four so bill-level Paid/Remain actually sums to the customer-level totals:

| Customer | Paid | Remain | State |
|---|---|---|---|
| منى عبد الرحمن | 450 | 0 | Fully paid |
| سارة حسن | 300 | 150 | Partial debt (2 bills: one settled, one open) |
| ياسمين محمود | 0 | 620 | All debt, nothing paid yet |
| هدى إبراهيم | 200 | 0 | Fully paid, small discount on bill |
| نور الدين علي | 150 | 90 | Partial debt |
| ريم فتحي | 0 | 275 | All debt |
| أميرة سعيد | 500 | 500 | Half paid on a large order |
| دينا كمال | 1000 | 0 | High-value customer, fully paid |

This covers the states a debt screen needs to visually distinguish: zero balance, partial balance, full balance owed, and a big-spender case. Delete these rows (or swap in real client data) once the skincare seller's actual customer list is available.

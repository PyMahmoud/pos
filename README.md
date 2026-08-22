# PosSystem — Skeleton

The old UI is gone entirely. This is a clean two-project split: a pure backend
library with zero UI dependency, and a WPF shell being built up screen by screen.

## Structure

```
PosSystem.sln
PosSystem.Core/            <- class library, NO WPF/UI references at all
  Models/                  <- data classes (Customers, Bills, Goods, Sells, etc.)
  Data/                    <- SQLite data access (renamed from Manager/Database)
    Server.cs              <- connection string + CreateDatabase/CreateTable helpers
  packages.config           <- only System.Data.SQLite, nothing else

PosSystem.App/              <- WPF shell with real sidebar nav + growing set of screens
  App.xaml / App.xaml.cs
  MainWindow.xaml            <- shell: sidebar (240px) + active screen, MVVM-driven
  Assets/
    IconGeometries.cs         <- hand-authored flat vector nav icons (no font/library dependency)
  Converters/
    GreaterThanZeroConverter.cs <- numeric -> bool, used to disable/dim out-of-stock items
    CountToVisibilityConverter.cs <- collection count -> Visibility, used for Checkout's empty-cart message
  Localization/
    LocalizationManager.cs    <- swaps Strings.English/Arabic.xaml at runtime, flips FlowDirection
    Strings.English.xaml / Strings.Arabic.xaml <- identical key sets, every screen binds via DynamicResource
  Theming/
    ThemeManager.cs           <- swaps Colors.Light/Dark.xaml at runtime (ThemeManager.Toggle())
  Themes/
    Colors.Light.xaml         <- real palette, generated from seed #6C4CE0 (Material Design 3 Fidelity scheme)
    Colors.Dark.xaml          <- same tokens, dark variant, same seed
    Typography.xaml           <- type scale (placeholder font, real sizes)
    NavStyles.xaml             <- sidebar ListBoxItem + ThemeToggleButtonStyle
    CheckoutStyles.xaml         <- category chip style, Cash/Card selectable-button styles
  ViewModels/
    ViewModelBase.cs          <- INotifyPropertyChanged base for every screen ViewModel
    RelayCommand.cs            <- ICommand implementation for button bindings
    NavItem.cs                  <- sidebar entry model (label key, icon, lazy view factory)
    MainViewModel.cs             <- owns nav items + which screen is currently shown
    DashboardViewModel.cs         <- runs the Core/SQLite smoke test, will host real KPIs later
    CategoryChip.cs                <- Checkout's category filter chip model
    CartLine.cs                     <- one line in Checkout's current order
    CheckoutViewModel.cs             <- Checkout screen logic (see "Checkout screen" below)
  Views/
    DashboardView.xaml            <- shows smoke-test output; charts land here later
    CheckoutView.xaml               <- real screen: item grid + cart + Cash/Card + Complete Sale
    CustomersView.xaml               <- placeholder, real screen is next (debt tracking)
    InventoryView.xaml                <- placeholder, no step assigned yet
    SettingsView.xaml                  <- language toggle lives here
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
4. Build & run — you get a sidebar (Dashboard, Checkout, Customers, Inventory, Settings) with Dashboard shown by default. Click into **Checkout** to ring up a real test sale against `rovaShop.db`'s 281 seeded products; use **Settings** to confirm the English/Arabic toggle and the sidebar's "Toggle Light / Dark" button both work.

## Checkout screen

Built against the existing `Core.Data.Goods` / `Bills` / `Sells` layer — no new SQL was added to Core:

- **Item grid**: all 281 goods loaded once on screen load, filtered client-side by category chip + search text (no per-keystroke SQLite round-trip). Out-of-stock items (`Quantity <= 0`) are dimmed and their Add button disabled.
- **Cart**: tap an item to add it, +/- to adjust quantity (capped at stock on hand), running Total in the sidebar panel.
- **Cash / Card**: a plain toggle, no payment gateway — matches the business plan's manual-entry model (staff key in whatever the card reader showed). Both are treated as paid-in-full; "buy now, pay later" belongs to the Customers/Debt screen, not here, so every sale here is a walk-in sale (blank `Ownername`/`Ownerid`/`Ownernumber`).
- **Complete Sale** writes one `Bills` row for the order and one `Sells` row per line, then decrements each sold good's `Quantity` via `UpdateGoodCount`. `Bills.InsertBills` requires an explicit `ID` (not just `Billnumber`) with no dedicated "next ID" helper in Core, so `CheckoutViewModel` computes both from the existing table via `ReadAdapter("bills")` rather than adding new SQL to a file someone else might also be touching.
- **Tax/Discount are intentionally left at 0** — not invented here. Per the Settings screen's own description, tax rate is meant to be added there when the client actually needs it, not guessed at in Checkout.

Open question carried over from the dev plan: a per-product "sold out" flag was mentioned as a nice-to-have, but the `Goods` model has no `IsAvailable` column — `Quantity <= 0` is being used as that signal instead, consistent with how the rest of the schema already treats stock.

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
screen should bind only to these keys via `DynamicResource` (not
`StaticResource` — that resolves once at parse time and won't repaint when
the theme swaps), never a raw hex, so `ThemeManager.Toggle()` (in the sidebar
footer) repaints the whole app with no per-screen logic. Startup default is
Light (POS counters are usually in bright rooms); Dark is available via the
toggle.

## Language

App language is switchable at runtime between English and Arabic — toggle it from the **Settings** screen (5th sidebar item). Same swap pattern as the theme: `Localization/Strings.English.xaml` and `Localization/Strings.Arabic.xaml` define the identical set of string keys, `Localization/LocalizationManager.cs` hot-swaps which one is merged into `Application.Resources`, and every screen binds to those keys via `DynamicResource` instead of a hardcoded literal — so nothing screen-specific has to know which language is active.

Arabic also flips the app to right-to-left: `LocalizationManager.SwitchLanguage()` sets an `AppFlowDirection` resource that `MainWindow` and every `View` (Checkout included) bind their `FlowDirection` to, and WPF mirrors the layout automatically from there.

Sidebar labels (`NavItem.Label`) are the one spot that couldn't use a plain XAML `DynamicResource` binding, since `NavItem` is a C# object, not a dependency property — `NavItem` instead resolves its label through `LocalizationManager.GetString()` and re-raises `PropertyChanged` when the language changes. `CheckoutViewModel`'s category chips follow the same pattern (see `RebuildCategoryChips()`), since they're also built from string-resource keys, not literals.

## Sidebar icons

`Assets/IconGeometries.cs` has 5 small hand-authored flat vector icons (Dashboard, Checkout, Customers, Inventory, Settings) as raw `Geometry` path data — not from an icon font or external package. Deliberate: Segoe MDL2 Assets isn't guaranteed on very old Windows installs, and an external icon library is one more dependency on machines this app needs to stay light on. They're legible and distinct but not polished — swap for a proper icon set later if the client wants something more refined; nothing else needs to change since `NavItem.IconData` is just a `Geometry` reference.

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

Note: ringing up test sales through the new Checkout screen will change `goods.Quantity` for whatever you sell — expected and fine for testing, just don't be surprised if a product's stock number has moved from what's listed above/in earlier commits.

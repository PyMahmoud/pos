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
  Themes/
    Colors.xaml              <- Material-You-style token palette (placeholder values)
    Typography.xaml          <- type scale (placeholder font, real sizes)
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

1. **Verify Core against real data** — write a throwaway test call (e.g. in `MainWindow`'s constructor) to `new PosSystem.Core.Data.Customers().ReadCustomers_Range(...)` against `rovaShop.db` to confirm the data layer actually reads existing records end to end.
2. **Design the real color palette** — replace the placeholder hex values in `Themes/Colors.xaml` with your actual brand seed color. If going for genuine Material You, generate a full tonal palette from one seed color rather than picking values by eye.
3. **Build the app shell/navigation** — a left sidebar (Checkout, Customers, Inventory, Dashboard) is the natural Material-style layout for a POS. This replaces the old app's custom-drawn menu rectangle entirely.
4. **Build Checkout first** — highest-traffic screen, and the one that most needs to look and feel modern since it's what the client's staff sees all day.
5. **Build Customers/Debt screen second** — this is the feature the skincare client specifically asked for, and the data model already supports it.

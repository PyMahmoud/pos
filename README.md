# RovaPOS → [Your Product Name] — Modernization Plan

Forked from [mohamedelareeg/WPF-POS](https://github.com/mohamedelareeg/WPF-POS) (MIT license) as the base for our POS product. This document is the plan for what we keep, what we rip out, and what we rebuild.

## 1. What's already here (audit)

The original repo (internally called "RovaPOS") is more complete than its README suggests:

**Good news — reusable as-is or with light edits:**
- `.NET Framework 4.8`, WPF, `System.Data.SQLite` (EF6) — matches our planned local-first architecture exactly
- Real per-entity data access layer in `Manager/Database/` (Bills, Customers, Goods, Purchase, Sells, Returned, Expense, Categories) — not a toy CRUD demo
- **`Customers` model already has `Paid` and `Remain` fields** — this is the debt/credit ledger our skincare client needs, already half-built
- **`Bills` model already has `Paid`, `Remain`, `Earned`, `Tax`, `Discount`** — a real invoicing/billing structure, not just a flat sales log
- `LiveCharts` (0.9.7) already wired in for dashboard charts — same library family we already planned to use
- Built-in **RTL (right-to-left) layout support** (`FlowDirection="RightToLeft"` on the main window) — genuinely useful for Arabic UI, saves us localization work
- Inventory, Purchases, Returned, Expenses, Bank modules — closer to a retail/shop system than a bare-bones demo, good fit for a skincare products seller

**Needs attention before we ship anything to a client:**
- ✅ **RESOLVED — `MindFusion.Keyboard.Wpf`:** confirmed zero references anywhere in code or XAML. It was a dangling `packages.config` entry, never actually wired in. Removed from `packages.config` — no licensing question left to resolve.
- ⚠️ **`FirebaseSharp` / `FireSharp` — NOT safe to blindly strip, and higher priority than first thought.** These are actively used in `Inventory.xaml.cs`, `POS_Shop_UserControl.xaml.cs`, and `POS_Restaurant_UserControl.xaml.cs`, and worse: the Firebase connection config has the **original developer's live credentials hardcoded in plain text** (`AuthSecret` + his `rovapos.firebaseio.com` project URL). This must be removed before any client build — it's not our backend, it can't function correctly for us anyway, and shipping someone else's leaked credential in our product is a real liability. Plan: either (a) rip out the Firebase calls entirely for v1 since we're building local-first/offline-first anyway and don't need this cloud sync path, or (b) replace with our own sync service pointed at our own backend later. Leaning (a) for the client MVP — matches our offline-first architecture decision from earlier anyway.
- `Microsoft.Office.Interop.Excel` — heavy dependency just for export; likely replaceable with a lightweight CSV/OpenXML export instead.
- UI is entirely hardcoded XAML (inline gradients, fixed pixel positions, no real style/theme resource dictionary) — functional but visually dated and hard to re-skin without touching every window individually. This is the main "make it look modern" work.
- `LiveCharts` 0.9.7 is the old, unmaintained version — worth upgrading to `LiveChartsCore.SkiaSharpView.WPF` (actively maintained, better performance on weak hardware).

## 2. Modernization goals

"Modern" here means two separate things — visual and technical. Both matter, but visual is what the client sees first.

### Visual modernization
- Replace the hardcoded gradient/rectangle chrome with a proper flat, consistent theme: a real color palette, consistent spacing, modern typography (not default Segoe UI sizes everywhere)
- Move repeated styles (buttons, panels, headers) into `ResourceDictionary/` properly instead of inline `Button.Style` blocks copy-pasted per window — this alone will make every future screen faster to build and easier to keep consistent
- Consider a lightweight modern WPF UI toolkit (e.g. `WPF-UI` / Fluent Design-styled controls) to get rounded corners, proper shadows, and modern controls without hand-building a design system from scratch — free/MIT-licensed options exist
- Bigger touch-friendly tap targets on the checkout screen specifically, since this will run on modest/older touch or mouse-driven hardware

### Technical modernization
- Strip unused dependencies (Firebase/FireSharp) and resolve the MindFusion licensing question first — before any visual work, since this affects whether we can legally ship
- Decide: stay on **.NET Framework 4.8** (safest — all current packages already target it, zero migration risk) vs. migrate to **.NET 8** (better long-term performance and tooling, but real migration effort since `System.Data.SQLite` + EF6 + WPF on .NET 8 needs verification package-by-package). Recommendation: **stay on .NET Framework 4.8 for the skincare client MVP** — don't take on a framework migration and a UI overhaul at the same time. Revisit .NET 8 migration once we have one paying client live and stable.
- Upgrade `LiveCharts` → `LiveChartsCore.SkiaSharpView.WPF` for the dashboard
- Rename the project/namespace from `RovaPOS` to our actual product name early, before too much code references the old name

## 3. Adapting for the skincare client specifically

- `Customers.Paid` / `Customers.Remain` → this **is** the debt tracker. We mostly need a clean UI screen showing each customer's balance and a "record payment" action that reduces `Remain` and logs the payment — the data model already supports it.
- `Bills` already models tax/discount/earned per transaction — good fit for retail margin tracking on skincare products.
- `Goods`/`Inventory` module → maps directly to skincare product stock (no need for the modifier/variant complexity a clothing store would need yet).
- Skip `Purchases`/`Returned`/`Expenses` polish for v1 unless the client explicitly asks — focus the modernization effort on **Checkout → Customers/Debt → Dashboard**, the three screens that matter most for a "simple POS."

## 4. Build order

1. **License/dependency cleanup** — ✅ MindFusion removed (was unused). Still to do: remove the three Firebase call sites and their hardcoded credentials, confirm the build still compiles clean afterward
2. **Rebrand** — rename project/namespace, replace placeholder icon/assets
3. **Style system** — build the real `ResourceDictionary` (colors, button styles, typography) once, apply globally
4. **Checkout + Customers/Debt screens** — the two screens the skincare client will actually live in daily; modernize visually and verify the paid/remain logic end to end
5. **Dashboard** — upgrade LiveCharts, wire to real data, apply the new style system
6. **Everything else** (Inventory, Purchases, Expenses, Bank) — visual pass only, logic stays as-is unless the client asks for more

## 5. Open questions before we start coding

- Do we have (or need to buy) a MindFusion license, or can we identify and remove its usage entirely?
- Confirm with the skincare client: does "debt" mean customers who buy now and pay later (accounts receivable), or does the client also owe suppliers (accounts payable)? The existing model fits accounts receivable cleanly — accounts payable would need a parallel `Suppliers` table.

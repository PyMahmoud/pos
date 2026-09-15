# Reference-Repo Features Plan — from mohamedelareeg/POS

**Status: planning only, nothing implemented yet.** This file records the
comparison findings against `mohamedelareeg/POS` (the author's larger,
.NET 8/EF Core/Clean-Architecture sibling to the `WPF-POS` base this project
forked) and lays out the five High Value features as concrete next steps,
in priority order. Each section below was checked against this repo's
*actual current code* (not assumed) before being scoped — two of the
original comparison findings turned out to already exist or be partially
redundant here; both are called out explicitly so nobody re-builds
something that's already done.

**Source repo:** `https://github.com/mohamedelareeg/POS` — different tech
stack entirely (.NET 8, EF Core, ASP.NET Identity), so nothing below is a
code port. Every item is a re-implementation of the *concept* against this
app's real .NET Framework 4.8 / SQLite / ADO.NET stack, following this
project's own established conventions (idempotent
`DatabaseBootstrapper.EnsureSchema()` + `EnsureColumn` for schema changes,
model → data class → ViewModel → View → localization → `.csproj`
registration layering, admin-gating for sensitive fields, both
`Strings.English.xaml`/`Strings.Arabic.xaml` for every new string).

**Before starting any of this:** pull latest — Mahmoud pushes to the same
repo concurrently, and several of these touch files he's likely to have
also touched recently (`CheckoutViewModel.cs`, `DatabaseBootstrapper.cs`,
`SettingsViewModel.cs`).

---

## Corrections from the original comparison — read first

1. **DB backup/restore was on the original "high value" list as a gap. It
   isn't one.** `SettingsViewModel`'s Data & Backup section already does
   "Back Up Now" (timestamped copy of `rovaShop.db` into a `Backups`
   folder) and "Open Backups Folder". Restore was **deliberately not
   built** — the existing doc comment explains why: overwriting the live
   `.db` file while the app (and its open SQLite connections) is running
   is a real corruption risk, and this app has no maintenance-mode concept
   to do it safely. That reasoning is sound and shouldn't be casually
   overridden. What's actually missing, if anything, is a *safe* restore
   path (e.g. "restore on next launch" — copy the chosen backup over
   `rovaShop.db` from a small pre-`App.xaml.cs`-startup step, before any
   SQLite connection is opened) — see item 5 below, downgraded from the
   original "quick win" framing to a real but smaller piece of work.

2. **`Ownerid` on `customers` may already partially cover the
   tax-compliance-fields idea.** The reference repo's `CommercialRegister`/
   `TaxCard` fields were flagged as a likely gap, but this app's
   `Customers.Ownerid` is an existing free-text field whose actual current
   meaning (national ID? commercial register number? nothing formal, just
   whatever the cashier typed?) isn't pinned down anywhere in code or
   comments. **Confirm with Baraa/Mahmoud what `Ownerid` is actually used
   for today before adding a separate `TaxCard`/`CommercialRegister`
   field** — it may just need a clearer label, not a new column.

---

## 1. Multi-method payments (Cash / Card / Bank Transfer / Cheque / Pay Later)

**Highest priority — biggest real gap for a distributor collecting from
customers.**

**Current state (confirmed in code):** `CheckoutViewModel.PaymentMethod`
is `{ Cash, Card, PayLater }`. On `CompleteSale()`, the selected method
collapses to a plain string tag (`"Cash"` / `"Card"` / `"Credit"`) written
into `bills.Details` — no structured detail beyond that tag. `PayLater`
already does the "money owed" job the reference repo's `OnAccount` type
does (`Remain` grows on the linked customer, paid down later via
`CustomersViewModel.RecordPayment` — see `Models.Payment`'s existing
append-only history/revert design, which this feature should reuse, not
duplicate).

**Plan:**
- Extend the `PaymentMethod` enum (`CheckoutViewModel.cs`) with
  `BankTransfer` and `Cheque`.
- Add a `PaymentReference` nullable TEXT column to `bills` via
  `EnsureColumn(conn, "bills", "PaymentReference", "TEXT")` in
  `DatabaseBootstrapper.EnsureSchema()` — free-text (bank name + last 4 of
  account, or cheque number + drawee bank), not a new normalized table.
  Reference repo's `Cheque`/`Bank` entities are fully structured
  (BankName/PersonName/AccountNumber as separate columns); a single
  free-text reference field is the right-sized version of that for a
  2-person team's SQLite app — structured fields can be split out later
  if the client specifically asks to search/report on them.
- Keep writing the existing tag into `bills.Details` unchanged (`"Cash"` /
  `"Card"` / `"Credit"` / add `"BankTransfer"` / `"Cheque"`) — no rename,
  no migration of existing rows, every existing reader of that column
  (Dashboard's payment-split pie, Excel export) keeps working with two new
  possible values instead of three.
- Checkout UI: extend the existing Cash/Card/Pay Later segmented control
  with two more options; when Bank Transfer or Cheque is selected, show a
  single optional reference text box (bound to `PaymentReference`).
- Dashboard's existing payment-split pie chart
  (`LiveChartsCore`/`SkiaSharp`, event-driven via `OrderEvents.
  OrderCompleted`) needs no structural change — it already groups by
  whatever string is in `bills.Details`, so two new tag values show up as
  two new slices automatically.
- New localization keys (both `Strings.English.xaml` and
  `Strings.Arabic.xaml`): `CheckoutBankTransfer`, `CheckoutCheque`,
  `CheckoutPaymentReference` (label/placeholder).
- `.csproj`: no new files, so no `<Compile>` registration needed — this is
  entirely inside existing files.

**Open question for Baraa:** should Bank Transfer/Cheque be allowed to
combine with `PayLater` (e.g. "partial cheque now, rest on account")? The
reference repo's `InvoicePayment` model supports splitting one invoice
across multiple payment rows; this app's `CompleteSale()` currently
assumes exactly one payment method per bill. Recommend keeping it
one-method-per-bill for this pass (matches the existing UI's
single-selection segmented control) and treating split payments as a
separate, later feature if the client asks for it.

---

## 2. Per-product low-stock threshold (`MinStock`)

**Current state (confirmed in code):** `AppSettings`/`SettingsViewModel`
already has a **global** `LowStockThresholdInput` — one number for the
whole shop, driving Inventory's stock badges. This item adds an optional
**per-product override**, not a replacement — most products keep using
the global default, but a handful of fast-moving or slow-moving lines can
set their own reorder point.

**Plan:**
- `EnsureColumn(conn, "goods", "MinStock", "REAL")` in
  `DatabaseBootstrapper.EnsureSchema()`, same pattern as
  `goods.DiscountPercent` — nullable, no backfill needed (`NULL` = "use
  the global Settings threshold", read via `DbNullSafe.ToDouble` with a
  null-safe fallback to `AppSettings`'s existing global value, not to 0).
- `Models.Goods`: add `MinStock` (nullable `double?`), same
  `INotifyPropertyChanged` shape as the existing `DiscountPercent`
  property.
- `Data.Goods`: extend `InsertGoodsReturningId`/the update method used by
  Inventory's staged Save Changes flow to carry the new field through —
  same shape as how `DiscountPercent` was threaded through when it was
  added (see that feature's own comments in `Data/Goods.cs` for the
  staged-edit interaction to match).
- Inventory's Add/Edit Product form: one new optional numeric field,
  "Low stock override (blank = use shop default)". Reuse whatever
  low-stock red/badge logic already reads the global threshold — it just
  needs to check the product's own `MinStock` first, falling back to
  `AppSettings.LowStockThresholdPercent`/whatever the existing global
  property is actually named (confirm exact name in
  `AppSettings.cs`/`SettingsViewModel.cs` at implementation time).
- New localization keys: `InventoryMinStockLabel`,
  `InventoryMinStockHint`.

---

## 3. Pricing guardrail fields (`MinSalePrice`, `ProfitMargin`)

**Current state:** `Goods`/`GoodsR` has `Cost` and `Price` but nothing
stopping a cashier (or an Inventory edit) from setting a sale price below
cost. Relevant given this app already has per-customer discount
percentages (`Customers.DiscountPercent`) and per-product discounts
(`Goods.DiscountPercent`) — two live ways a price can be dropped below
intent with no floor.

**Plan:**
- `EnsureColumn(conn, "goods", "MinSalePrice", "REAL")` — nullable,
  optional per-product floor price. `NULL` = no floor enforced (default,
  matches today's behavior exactly for every existing product).
- Inventory's Add/Edit Product form: optional "Minimum sale price" field
  alongside the existing Price field.
- Enforcement point: `Goods.DiscountPercent` application and Checkout's
  per-bill discount (`CheckoutViewModel.DiscountPercentInput`) both need a
  check — if applying the discount would put a line's effective price
  below that product's `MinSalePrice`, either clamp the discount or show a
  warning (needs a decision from Baraa: hard block vs. admin-override
  warning — recommend routing through the same admin-gate pattern
  `IsDiscountUnlocked` already uses, so a locked-out cashier can't
  discount below floor but an unlocked admin still can, deliberately).
- `ProfitMargin` from the reference repo is dropped from this pass — it's
  a derived display value (`(Price - Cost) / Price`), trivially computable
  from existing `Cost`/`Price` with no new column needed. If Baraa wants
  it surfaced in the Inventory grid, that's a pure UI addition, not a
  schema change — flagged separately, not bundled into this schema-change
  item.
- New localization keys: `InventoryMinSalePriceLabel`,
  `InventoryMinSalePriceBelowFloorWarning`.

---

## 4. `DecimalTextBoxBehavior` (numeric-only input, clean pattern)

Smallest item, no schema change, no data-layer change — pure UI polish.
Directly portable from the reference repo's WPF code with no adaptation
needed (same WPF/.NET, just a different TFM):

- New file `PosSystem.App/Behaviors/DecimalTextBoxBehavior.cs` — attached
  `DependencyProperty` (`AllowDecimalPoint`) on `TextBox`, blocking
  non-digit/non-single-decimal-point keystrokes at `PreviewTextInput`,
  same as `Behaviors/ScrollBehavior.cs`/`MinLengthTrack.cs` already
  sitting in that folder.
- Apply to the new `PaymentReference`... no — apply to genuinely numeric
  entry fields that currently rely on post-hoc `double.TryParse` validation:
  `DiscountPercentInput` (Checkout), `TaxRatePercentInput`/
  `LowStockThresholdInput` (Settings), the new `MinStock`/`MinSalePrice`
  fields from items 2–3 above, and Inventory's Cost/Price/Quantity fields.
  Doesn't replace the existing parse-on-change validation logic in any of
  those ViewModels (still needed as a safety net for paste/IME input this
  behavior doesn't catch) — purely stops the bad keystroke from landing in
  the box in the first place, a UX improvement layered on top.
- `.csproj`: register the new file under `<Compile Include="Behaviors\
  DecimalTextBoxBehavior.cs" />` — old-style `.csproj`, easy to forget,
  flagged explicitly per this project's own known gotcha.
- No localization needed (no new strings).

---

## 5. Safe restore-on-next-launch (downgraded from "DB backup UI")

See Correction 1 above — backup already exists; this is the missing
restore half, scoped to be safe rather than just re-adding what's already
there.

**Plan:**
- Settings' Data & Backup section gets a "Restore from backup…" button
  that: (a) lets the user pick a `.db` file (presumably from the existing
  Backups folder, via `OpenFileDialog`), (b) does **not** copy it over the
  live database immediately, (c) instead writes the chosen path to a small
  marker file (e.g. `restore-pending.txt` next to `rovaShop.db`) and shows
  a "Restart the app to finish restoring" message.
- `App.xaml.cs`, at the very top of startup — **before**
  `DatabaseBootstrapper.EnsureSchema()` or any other code opens a
  `SQLiteConnection`— checks for that marker file; if present, copies the
  referenced backup over `rovaShop.db`, deletes the marker, then continues
  startup normally against the now-restored database.
- This keeps the original safety reasoning intact (no live connection is
  ever open during the actual file overwrite) while still giving
  Baraa/Mahmoud/the client a restore path that doesn't require manually
  closing the app, finding the file on disk, and copying it by hand.
- New localization keys: `SettingsRestoreButton`,
  `SettingsRestoreConfirmPrompt`, `SettingsRestorePendingMessage`.

---

## Suggested build order

**1 → 2 → 4 → 3 → 5**

Multi-method payments (1) is the one the client will actually notice and
use daily. Low-stock override (2) and the numeric-input behavior (4) are
both small, low-risk, and independent of everything else — good filler
between bigger pieces. Pricing guardrails (3) needs a real decision from
Baraa on enforcement behavior (hard block vs. admin-override) before
being built, so it's fine to sequence after the two easy wins. Restore (5)
touches app startup ordering — the single riskiest file to get wrong
(`App.xaml.cs`) — so it goes last, once everything else has landed and
there's a stable point to test against.

Every item above needs its own confirmation pass with Mahmoud before
touching shared files (`CheckoutViewModel.cs`, `DatabaseBootstrapper.cs`,
`SettingsViewModel.cs`, `App.xaml.cs`) — pull first, same as always.

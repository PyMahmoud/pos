# POS System — Development Plan

Current state: skeleton solution exists (`PosSystem.Core` + `PosSystem.App`), old UI fully removed, backend data layer intact and Firebase-free. This plan picks up from there.

---

## Phase 0 — Done ✅
- Researched POS feature baseline, pricing, and food-truck-specific requirements
- Chose stack: Windows, C#/.NET, WPF, SQLite (local-first)
- Chose `mohamedelareeg/WPF-POS` (MIT) as base instead of building from scratch
- Audited it: removed unused `MindFusion` dependency, identified and isolated the leaked-Firebase-credential issue
- Rebuilt as a clean two-project skeleton (`PosSystem.Core` / `PosSystem.App`), MVVM scaffolding in place, placeholder Material-You-style theme tokens created

---

## Phase 1 — Verify the foundation
**Goal:** prove the skeleton actually works before building on top of it.

- Restore NuGet packages, confirm the solution builds clean in Visual Studio
- Write a throwaway test call from `MainWindow` to `PosSystem.Core.Data.Customers.ReadCustomers_Range(...)` (or similar) against the existing `rovaShop.db` — confirm real data comes back
- Confirm the app runs on a genuinely old machine/VM if you have access to one — catch performance surprises now, not after the UI is built

**Exit criteria:** app builds, runs, and reads real rows from the existing database.

---

## Phase 2 — Design system
**Goal:** stop using placeholder colors; lock in the actual visual identity everything else will be judged on.

- Pick a real brand seed color (not picked by eye — use Material Theme Builder or similar to generate a full tonal palette from one seed color)
- Fill in `Themes/Colors.xaml` with the real palette (primary/secondary/surface/error roles, light mode first — dark mode later if wanted)
- Decide on a real font (system default Segoe UI is fine to keep, or pick one Google Font and bundle it)
- Build a small "component sandbox" window showing your button styles, text styles, and card/surface look in isolation — cheaper to iterate on one screen than to redesign five real screens later

**Exit criteria:** you can look at one window and say "yes, this looks professional" before any real screen exists.

---

## Phase 3 — App shell & navigation
**Goal:** the frame every screen lives inside.

- Build a sidebar (or top nav) shell: Checkout, Customers, Inventory, Dashboard, Settings
- Wire simple navigation (swap the main content area based on selection) — a `ContentControl` bound to the current ViewModel is enough, no need for a heavy navigation framework
- Apply the Phase 2 design system here first, since it's the one piece of UI visible on every screen

**Exit criteria:** clicking sidebar items switches views (even if views are still empty placeholders).

---

## Phase 4 — Checkout screen
**Goal:** the screen staff will live in all day — highest priority for both function and polish.

- [x] Product grid (tap to add), cart panel, running total
- [x] Cash / Card payment toggle (no gateway integration needed — confirmed manual-entry model)
- [x] Save order → writes to `Sells`/`Bills` tables via the existing `Data` layer
- [~] Sold-out toggle per product — there's no `IsAvailable` column on `Goods`, so this uses `Quantity <= 0` as the out-of-stock signal instead (dims the card, disables Add). A real toggle would need a schema change; flagging rather than adding one unasked.
- [x] **Tested end-to-end in Visual Studio — confirmed working (Mahmoud, real build/run pass).**

**Exit criteria met.** You can complete a full sale from product tap to saved order, styled with the real design system. Closed.

---

## Phase 5 — Customers & debt tracking
**Goal:** the specific feature the skincare client is waiting for.

- [x] Customer list screen: name, phone, current balance (`Remain`) — plus customer code (`Ownerid`), search by name/phone/code, and an "add a customer" form (not explicitly listed above, but there was no way to onboard a new customer without it)
- [x] "Record payment" action: reduces `Remain`, increases `Paid`. **No `CustomerPayments` log table was added** — the plan flagged this as optional ("if you want a full payment history"), and adding one is a schema change nobody asked for yet; Customers.Paid/Remain stay running totals only, same tradeoff Phase 4 made for the Goods `IsAvailable` question. Revisit if the client wants an actual payment history/audit trail, not just a current balance.
- [x] Link orders to a customer optionally at checkout, with a "pay later / add to tab" option that increases their `Remain` instead of requiring full payment — Checkout now has a customer picker (Walk-in by default, unchanged behavior) and a third Pay Later button next to Cash/Card, enabled only once a real customer is selected. Linking a customer under Cash/Card (fully paid) also now records who the sale was for and grows their `Paid` running total — a small extension beyond what was asked, since the `Paid` field on `customers` was otherwise going to sit at 0 forever for anyone who always pays in full.
- [ ] **Confirm with the client: accounts receivable only, or also payable?** Still open, flagged since Phase 0. Everything built in Phase 5 assumes receivable only (customers owing the shop) — the data model has no supplier/payable concept, and none was added.
- [ ] **Test end-to-end in Visual Studio — not yet done**, same caveat Phase 4 shipped with. This screen (and the Checkout additions) were written directly against the repo via file access, not compiled or run — no Windows/WPF runtime available here. Before treating Phase 5 as closed: build, add a test customer, ring up a Pay Later sale against them from Checkout, confirm their balance updates on the Customers screen (including when Checkout wasn't the last screen you were on — that cross-screen refresh is real logic, not just a coincidence of load order), then record a partial payment and confirm `Remain` drops correctly. Also worth toggling language and light/dark mid-flow once, since Phase 5 touches both.

**Exit criteria:** a customer can buy on credit, and later have a payment recorded that correctly reduces their balance — visible in the UI. *(Code is in place for this; needs the same real build/run pass as Phase 4 before calling it done.)*

---

## Phase 6 — Dashboard
**Goal:** real-time visibility, the feature you called out as core from day one.

- Upgrade `LiveCharts` (old 0.9.7) → `LiveChartsCore.SkiaSharpView.WPF`
- Cards/charts: today's revenue, top-selling items, cash-vs-card split, outstanding customer debt total
- Wire updates to fire on order-save (event-driven), not on a polling timer — keeps CPU usage near zero between sales on old hardware
- If multiple locations/trucks are in scope for this client, decide now whether the dashboard needs a location filter

**Exit criteria:** completing a sale on the Checkout screen visibly updates a chart on the Dashboard without any manual refresh.

---

## Phase 7 — Inventory & reporting polish
**Goal:** round out the rest of the app — lower priority than 4–6, but needed for a complete product.

- Inventory screen: stock levels, low-stock indication, tied to `Goods` model
- Monthly report: scheduled job (Windows Task Scheduler-triggered console app or background service) that queries the last 30 days, builds an HTML/PDF summary, sends via SMTP (MailKit)
- Apply the design system to Purchases/Expenses/Bank screens — visual pass only, logic mostly stays as the original repo had it unless the client asks for more

**Exit criteria:** monthly report email actually arrives with real numbers in it; inventory screen reflects real stock changes from sales.

---

## Phase 8 — Client pilot (skincare seller)
**Goal:** get real-world validation before calling this "done."

- Install on the client's actual machine
- Walk them through Checkout + Customer/Debt screens directly — these are the two they'll use daily
- Watch them use it live if possible — UX problems surface fast this way, faster than any amount of your own testing
- Collect a punch list of friction points, fix the highest-impact ones first

**Exit criteria:** the client is running real sales through it without you standing next to them.

---

## Phase 9 — Food truck adaptation
**Goal:** apply the same core app to the original target market.

- Confirm final hardware (repurposed old laptop, per earlier decision) actually runs it acceptably
- Adjust checkout flow for a quick-order, no-tables use case if the skincare-client version ended up more retail-shaped
- Build the multi-truck owner dashboard view (aggregate across locations) — this was flagged as the actual differentiator for the caravan/fleet market

**Exit criteria:** the same core product serves both a retail shop and a food truck without maintaining two separate codebases.

---

## Phase 10 — Productization & pricing
**Goal:** turn "a system we built for one client" into "a product we sell repeatedly."

- Decide final pricing structure (one-time license vs. optional monthly add-on for cloud sync/multi-branch dashboard/reporting — per earlier market research)
- Package an installer (simple Windows installer, not just a folder of files) for handing off to new clients without you present
- Write basic client-facing setup docs (separate from this internal dev plan)
- Consider what "cloud sync" actually means for you now that Firebase is gone — build your own lightweight sync backend, or defer this until a client actually needs multi-location aggregation

---

## Suggested order of attack from today
**1 → 2 → 3 → 4 → 5**, in that order, without skipping ahead — Checkout and Customers/Debt are the two screens the skincare client actually needs, so everything before them is foundation and everything after them (Dashboard, Inventory, reporting) can wait until those two are solid and in the client's hands.

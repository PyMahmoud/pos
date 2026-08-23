# Dashboard Parity Plan — matching the Power BI reference

**Status: deferred.** Not started. Pick this up only after every other phase in
`POS-development-plan.md` is done and the client pilot (Phase 8) is underway
or complete — this is the last thing the system needs, not a prerequisite for
anything else.

**Goal:** get `DashboardView` as close as realistically possible to the
reference dashboard (Power BI screenshots, shared 2026-08-10) — KPI cards,
category breakdown, customer-type donut, a real time-series trend, working
filters, and ideally cross-filtering and drill-down.

**Hard constraints, non-negotiable:**
- **$0 budget.** No paid component libraries, no paid chart controls, no
  cloud services with a bill attached. Everything here uses WPF's built-in
  controls plus `LiveChartsCore.SkiaSharpView.WPF` (MIT-licensed, already
  the chosen charting library since the base Phase 6 build) and hand-written
  code. Nothing in this plan requires buying anything.
- **Must run acceptably on the client's genuinely old machine.** This was a
  Phase 0 decision (repurposed old laptop) and it doesn't get relaxed just
  because the dashboard gets fancier. Every stage below has a performance
  note — read those before implementing, not after.
- **Must match the app's existing design system exactly — in both light and
  dark theme.** Not just "the charts use theme colors" (already true today)
  — every new element (filter controls, tooltips, drill-down affordances,
  panels) needs to look like it belongs next to Checkout/Customers/Settings,
  not like a different app was pasted in. See Design-system rules below.
- **Must be easy to use — this takes priority over matching the reference
  when the two conflict.** The screenshots are a capability target, not a
  pixel-for-pixel mandate. A shop-floor screen a staff member glances at
  under pressure needs to stay simple and legible; if a Power BI feature
  would add clutter or a learning curve, simplify it rather than replicate
  it exactly. See Usability rules below.

---

## Where this picks up from

The base Dashboard (built during Phase 6) already exists and works:
4 KPI cards (today's revenue/profit/transactions/outstanding debt), a
top-5-items bar chart, and a Cash/Card/Pay-Later pie chart. It refreshes
event-driven (`OrderEvents.OrderCompleted`, `ThemeManager.ThemeChanged`,
`LocalizationManager.LanguageChanged`) rather than polling — that pattern is
correct and everything below should keep using it, not introduce a timer.

`DashboardViewModel.GetThemeColor(...)` already bridges WPF theme brushes to
SkiaSharp colors for chart series — reuse it for every new chart instead of
hardcoding hex values, same reason the rest of the app never hardcodes a
color.

---

## Feasibility per reference feature

| Reference feature | Feasible at $0? | Effort | Old-hardware risk |
|---|---|---|---|
| Revenue-over-time line chart | Yes — `LineSeries<T>`, same package already in use | Small | Low — one series, ~30-90 points |
| Donut chart (punched center) | Yes — `PieSeries.InnerRadius` | Small | Low |
| Hover tooltips (category %, revenue %) | Yes — LiveCharts supports custom tooltip templates | Small–Medium | Low |
| Location checkbox filter | Yes — standard MVVM re-query against in-memory data | Small | Low, **if** filtering re-uses already-loaded data instead of re-querying SQLite per click |
| Date-range filter | Yes | Medium | Low, same caveat as above |
| Cross-filtering (click a bar → everything else updates) | Yes, but it's real engineering — needs one shared "active filter" state every chart/KPI both reads and can write to | Large | **Medium** — every click potentially recomputes every KPI + every chart; must stay in-memory (see Performance Rules) |
| Drill-down (month → day, "Drill up") | Yes, but LiveCharts doesn't do this automatically like Power BI — needs custom logic to swap the chart's data/axis on click | Medium–Large | Medium, same reason as cross-filtering |

Nothing here requires a paid control. The two "Large" items are large because
of engineering complexity, not cost.

---

## Usability rules (read these first — this is the most important section)

1. **Every control needs a clear, short label** (reuse `Strings.*.xaml`
   keys, both languages) — no icon-only affordance a staff member has to
   guess at.
2. **The default state must be useful immediately.** Dashboard should load
   to a sensible range (e.g. "Today" or "This week"), not an empty or
   unfiltered blob staff have to configure before seeing anything useful.
3. **Don't cram in every reference feature at reference density.** If the
   finished screen needs more explanation than "tap a card, it shows more,"
   it's too complex for this audience — simplify rather than push through.
4. **Touch/click targets match the rest of the app's buttons/inputs** (see
   `CommonStyles.xaml`, `CheckoutStyles.xaml` sizing conventions) — nothing
   sized for a mouse-precision analyst.
5. **Every filter needs an obvious clear/reset affordance.** A filtered
   dashboard that's hard to un-filter is a trap, not a feature.
6. **If cross-filtering (Stage 3) or drill-down (Stage 4) make the screen
   harder to read at a glance rather than easier, that's a signal to
   simplify or drop them** — usability wins over reference-parity, per the
   hard constraints above.

---

## Design-system & theme-parity rules (apply to every stage)

1. **No new colors, anywhere.** Every brush comes from
   `Colors.Light.xaml`/`Colors.Dark.xaml` — via `DynamicResource` in XAML,
   or `DashboardViewModel.GetThemeColor` for SkiaSharp/LiveCharts series.
   Same rule the rest of the app has followed since the Phase 2/3
   `StaticResource`-vs-`DynamicResource` fix.
2. **Reuse existing styles — don't invent parallel ones.** `TitleMedium` /
   `BodyLarge` / `LabelSmall` for text, `ThemedTextBoxStyle` /
   `ThemedComboBoxStyle` for any new filter inputs, `SelectableButtonTemplate`
   for any new buttons, the existing card treatment (`SurfaceContainerBrush`,
   ~14–16px corner radius, standard padding) for any new panel. A filter or
   drill-down control that looks like it came from a different app is a
   regression even if it technically works.
3. **Toggle light AND dark for every new element before calling a stage
   done** — not just charts (already covered by `GetThemeColor`), but every
   filter control, tooltip, and drill-down affordance too. A control that's
   legible in light mode and broken/invisible in dark mode is exactly the
   bug class already found and fixed twice in this project (the theme-toggle
   button's hover state, and the Customers payment textbox blending into its
   own card) — assume it'll happen again unless explicitly checked, don't
   assume "it uses DynamicResource so it's fine."
4. **Toggle Arabic/RTL for every new element too.** This screen inherits
   `FlowDirection` like every other screen — a slider, breadcrumb, or
   drill-down affordance built assuming left-to-right needs to be checked
   under RTL, not assumed fine because the brushes are theme-aware.

---

## Performance rules for old hardware (apply to every stage)

These aren't optional polish — skipping them is how a dashboard that looks
fine in Visual Studio on a dev machine ends up laggy on the client's actual
laptop:

1. **Load from SQLite once per screen visit / event, filter in memory
   after that.** This is already the pattern Checkout (281 goods) and
   Customers use — Dashboard should too. Cross-filtering and the date-range
   filter must re-filter an in-memory list, never re-hit the database per
   click/drag.
2. **Cap chart data points.** The reference's daily trend line covers
   roughly a month at a time — don't render a full year of daily points by
   default; aggregate to weekly/monthly if the selected range is long.
3. **Turn off or reduce LiveCharts animations** (`EasingFunction`/animation
   duration on series) if they cause visible stutter during testing — old
   integrated GPUs can struggle with animated SkiaSharp redraws more than a
   modern machine will show in dev.
4. **Debounce the date-range control.** If it's a slider (see open decision
   below), don't re-query on every pixel of drag — re-filter on drag-end
   (`Thumb.DragCompleted`) or with a short timer-based debounce, or every
   redraw during a drag will fight the UI thread.
5. **Test on the oldest hardware available** before considering a stage
   done, the same way Phase 1 asked for an old-machine test before any UI
   existed at all.

---

## Staged build order

Ship and test each stage before starting the next — this is a bigger single
lift than any phase completed so far, and there's still no Windows/WPF
runtime available to build/test blind from here. One giant untested change
across all four stages is how three things end up quietly broken at once.

**Definition of done, for every stage below — all five, every time:**
- [ ] No new colors/fonts/styles outside `Colors.*.xaml` and the existing
      `Themes/*.xaml` styles
- [ ] Verified correct in both light and dark theme
- [ ] Verified correct in both English and Arabic, including RTL layout
- [ ] Usability check: a new staff member could use it with no explanation;
      every filter has an obvious clear/reset; nothing is icon-only
- [ ] Performance check: smooth on the oldest hardware available, no
      per-click SQLite re-query

### Stage 1 — Trend line + donut styling
- Revenue-over-time line chart, last 30 days, `Bills.Datex` grouped by day
- Switch the existing payment-split pie to a donut (`InnerRadius`)
- Lowest risk, immediately makes the screen feel more "alive"

### Stage 2 — Working filters
- Location checkboxes (**note:** confirm `Bills`/`Goods` actually has a
  location field before building this — if the schema has no per-location
  data yet, this either needs a schema addition or should be dropped;
  don't fake a filter that doesn't filter anything real)
- Date-range filter that actually re-queries the KPIs and every chart
- **Open decision, not yet made:** real two-thumb draggable slider (matches
  the reference exactly, but WPF has no built-in range slider — this means
  a hand-built custom control) vs. two simple date pickers (much less
  work, functionally identical result). Decide this when picking the stage
  up; either is $0, the tradeoff is purely effort vs. visual fidelity —
  weigh it against the Usability rules above, not just visual match.

### Stage 3 — Cross-filtering
- Clicking a bar/slice narrows every other KPI and chart on the screen
- The hard one — see Performance Rules before starting, and revisit
  Usability rule 6 once it's working: does it actually make the screen
  easier to read, or just busier?

### Stage 4 — Drill-down
- Month → day drill on the trend line, with a "drill up" affordance
- Only worth doing if the client/you still want it after living with
  stages 1–3 for a while — it's the single most Power-BI-specific piece,
  the easiest to get wrong on Usability rule 6, and the least essential to
  the actual debt/revenue-tracking job the app needs to do

---

## Explicitly out of scope, still at $0

- Any actual Power BI / cloud BI integration — the reference is inspiration
  for what the *custom* dashboard should feel like, not a suggestion to
  embed or license Power BI itself
- Real-time multi-user dashboards — this is a single-till local app per
  Phase 0's architecture; nothing here changes that
- Matching reference density/complexity anywhere it conflicts with the
  Usability rules above — capability is the target, not a pixel-for-pixel
  copy, whenever the two goals pull in different directions

# Dashboard Parity Plan — matching the Power BI reference

**Status: Stages 1–4 code written, started far ahead of the original
schedule** (explicit decisions by Mahmoud: 2026-08-23 to start Stage 1 early,
then 2026-08-23 again to do "all of the dashboard plan" in one go — the
staged rollout below was overridden a second time, on request, not
forgotten). **None of it is build/run tested yet** — see the per-stage notes
and the Design Deviations section below before treating any of this as done.

**Goal:** get `DashboardView` as close as realistically possible to the
reference dashboard (Power BI screenshots, shared 2026-08-10) — KPI cards,
category breakdown, customer-type donut, a real time-series trend, working
filters, and cross-filtering/drill-down in spirit if not literal form (see
Design Deviations).

**Hard constraints, non-negotiable:**
- **$0 budget.** No paid component libraries, no paid chart controls, no
  cloud services with a bill attached. Everything here uses WPF's built-in
  controls plus `LiveChartsCore.SkiaSharpView.WPF` (MIT-licensed) and
  hand-written code.
- **Must run acceptably on the client's genuinely old machine.** Every stage
  below has a performance note — read those before implementing, not after.
- **Must match the app's existing design system exactly — in both light and
  dark theme.** Every new element needs to look like it belongs next to
  Checkout/Customers/Settings, not like a different app was pasted in.
- **Must be easy to use — this takes priority over matching the reference
  when the two conflict.** See Design Deviations: this constraint is why
  Stages 2–4 below don't literally match the reference's interactions.

---

## Design deviations from the original plan — read this before testing

Building all four stages back-to-back surfaced real tradeoffs the original
staged plan hadn't hit yet. Three deliberate departures from a literal
reference port, each justified by the Usability hard-constraint above:

1. **No two-thumb draggable slider.** Two `DatePicker` controls instead.
   WPF has no built-in range slider; a hand-built one is real custom-control
   work I can't test without a compiler, for a control that's objectively
   less standard/familiar to non-technical staff than two date pickers.
   Zero cost either way — this was purely an effort/risk-vs-fidelity call,
   decided in favor of the lower-risk, equally-functional option.
2. **No location filter.** Checked the schema directly: `bills`, `sells`,
   and `goods` have no location field anywhere — the only `Location` column
   in the whole database is on the single-row `info` table (the shop's own
   config, not per-transaction data). Building a location filter would mean
   a filter with nothing real to filter, which the plan's own Usability
   rules explicitly rule out. Dropped, not forgotten — this is Phase 9
   (multi-location) scope if/when the client actually needs it.
3. **No click-a-chart-element cross-filtering or click-a-trend-point
   drill-down.** Replaced with explicit filter chips (payment method,
   category — same chip pattern as Checkout's category filter) and
   quick-range buttons (Today / This Week / This Month / Last 30 Days) plus
   two date pickers for a fully custom range. Two reasons, both real:
   - **Usability:** a small pie slice or a single point on a 30-point line
     is a genuinely worse touch target than a labeled button, and touch
     targets sized like the rest of the app is an explicit Usability rule,
     not a nice-to-have.
   - **Risk:** LiveCharts' click-event API (exact event name, exact
     `ChartPoint` member names for identifying which slice/bar/point was
     hit) varies across library versions, and there's no way to verify it
     compiles without a Windows/WPF build environment. Ordinary WPF
     buttons/`ListBox` chips carry the same confidence level as the rest of
     this app; the chart-click approach would not have.
   - **The "Today" quick-range button IS Stage 4's drill-down**, functionally:
     tapping it collapses every KPI/chart from a 30-day overview to a single
     day's full detail, same practical value as the reference's month→day
     drill, without a fragile dependency on an unverifiable API. See
     Usability rule 6 in the (now-historical, but still valid) rules below —
     this is that rule being applied, not skipped.

If any of these three calls turn out wrong in practice once this is actually
run — e.g. staff really do want to tap directly on a chart — that's a fast
follow-up, not a rebuild; the filter-chip UI can coexist with chart clicks
added later.

---

## What actually got built

**Stage 1** — revenue trend line chart, payment split pie chart (originally
donut-styled; changed to a full circle with a legend and a corrected
"Card" slice color on 2026-08-24 — see "Post-Stage-1 revision" below).

**Stage 2** — quick-range buttons (Today/This Week/This Month/Last 30 Days)
+ two custom date pickers, all driving one shared date range used by every
KPI and chart on the screen.

**Stage 3** — payment-method chips and category chips (built from real data,
same pattern as Checkout's category filter), both combining with the date
range into one unified in-memory filter. KPIs, the payment-split donut, and
a new "Revenue by Category" bar chart (matching the reference's own panel of
that name) are all derived from the exact same filtered dataset, so they
never disagree with each other regardless of which filters are combined —
that's what makes this genuinely cross-filtering rather than three charts
that each filter slightly differently.

**Stage 4** — the "Today" quick-range button, per Design Deviations above.

### Post-Stage-1 revision, round 2 (2026-08-24)

Mahmoud reviewed a screenshot of the built filter bar and reported one real
bug and two requests:

1. **Bug: chips showed `PosSystem.App.ViewModels.CategoryChip` instead of
   their label.** Root cause: the `PaymentChips`/`CategoryChips` `ListBox`es
   in `DashboardView.xaml` had no `ItemTemplate`, unlike Checkout's category
   chip list, which does — WPF falls back to `ToString()` on the bound
   object when there's no template, which for a plain C# class is its full
   type name. Fixed by adding the same `<TextBlock Text="{Binding
   DisplayName}" />` `ItemTemplate` Checkout already uses, to both lists.
2. **All Time quick range.** Added `DashboardQuickRange.AllTime`, a fifth
   button (`RangeAllTimeButtonStyle`, same DataTrigger pattern as the other
   four), and `DashboardViewModel.GetEarliestDataDate()` — scans the
   already-cached `_cachedSells` for the earliest parsed sale date (falls
   back to 5 years ago if there's no data yet), so this stays consistent
   with the Performance rule (no fresh SQLite query just to answer "how far
   back does data go").
3. **DatePicker visual polish.** The stock WPF `DatePicker` (plain white box,
   default system chrome) didn't match the rest of the app. Added
   `ThemedDatePickerStyle` (`CommonStyles.xaml`) using the same setters-only
   approach already established for `ThemedComboBoxStyle` in this same file
   — **deliberately not a full `ControlTemplate` override**: correctly
   reproducing `DatePicker`'s `PART_Root`/`PART_Button`/`PART_TextBox`/
   `PART_Popup` contract isn't verifiable without a WPF runtime to click
   through, and getting one part wrong risks silently breaking the calendar
   popup. Also added an *implicit* (key-less) style for `DatePickerTextBox`
   — it's a genuine `TextBox` subtype, not a separate composite control, so
   this automatically reaches the inner text area everywhere a `DatePicker`
   renders one, without touching `DatePicker`'s own template.
   **Known limitation, accepted rather than hidden:** corners stay
   square-ish — `Border.CornerRadius` isn't a `DatePicker` property, so this
   is the one input in the app that doesn't get the usual 10px rounding. If
   that turns out to matter once seen for real, the fix is the fuller
   `ControlTemplate` override this revision deliberately avoided — not a
   rebuild, just picking up the risk that was set aside here.

All three changes are **not yet build-tested**, same standing caveat as
everything else in this file.

### Post-Stage-1 revision (2026-08-24)

Mahmoud reviewed a screenshot of the built Payment Split donut and asked for
three changes, all in `DashboardViewModel.BuildPaymentSplitChart` and
`DashboardView.xaml`:

1. **Donut → full circle.** Removed `InnerRadius` from all three
   `PieSeries`.
2. **"Card" slice color.** It was reading `SecondaryColor`, which Material's
   Fidelity scheme deliberately derives as a muted variant of the *same hue*
   as `PrimaryColor` — correct per the Material spec, wrong for a chart that
   needs two slices to look obviously different. In dark theme it's not even
   muted, it's identical: `Colors.Dark.xaml` has `PrimaryColor` ==
   `SecondaryColor` == `#CBBEFF`. Switched to `InversePrimaryColor` instead
   — same engine, but built specifically to contrast against Primary
   (inverted lightness in both themes), so it reads as a genuinely different
   color rather than a shade of the same one. Reuses an existing token, no
   new color introduced.
3. **Legend.** Added `LegendPosition="Right"` on the `PieChart`. Its text
   color is a plain SkiaSharp paint (`ChartLegendTextPaint`, new property),
   not a `DynamicResource` brush, so — same pattern as every other chart
   color on this screen — it won't repaint itself on a theme swap;
   `RefreshDashboard` now sets it from `OnSurfaceColor` alongside the
   existing per-series colors, so it's recomputed on the same triggers
   (real data reload, theme toggle, language toggle).

**Not yet build-tested**, same caveat as the rest of this file — written via
direct file access, no Windows/WPF runtime available to compile or run it.
Specifically unverified: `LegendPosition`/`LegendTextPaint` are standard
`PieChart` properties in the documented v2 API, but this is exactly the kind
of LiveCharts surface flagged elsewhere in this plan as the least-certain
part of the Dashboard work — if either name doesn't exist on whatever
package version is actually installed, that's a missing-member build error,
not a logic bug, and should be fixed against the real installed API rather
than guessed at again.

**Explicitly NOT filtered:** the Top-Selling Items (all-time) chart and
Outstanding Customer Debt KPI stay unfiltered on purpose — the former is
titled "All-Time" and answers a different question than the filtered
period; the latter is a live balance snapshot, not a period metric, and
filtering a snapshot by date range wouldn't mean anything real.

**Performance:** every filter interaction (date pickers, chips) re-filters
already-loaded data in memory — `RefreshDashboard` (real SQLite reads, on
sale/theme/language change) is now separate from `RecomputeAndRedraw`
(pure in-memory re-filter, on every date/chip change), specifically so
clicking a filter never re-queries the database. Long custom ranges
(>60 days) bucket the trend line by week instead of by day to cap chart
points; trend-line labels thin to roughly 8 regardless of range length.

---

## Feasibility per reference feature (historical — kept for context)

| Reference feature | Feasible at $0? | Effort | Old-hardware risk |
|---|---|---|---|
| Revenue-over-time line chart | Yes — `LineSeries<T>` | Small | Low |
| Donut chart (punched center) | Yes — `PieSeries.InnerRadius` | Small | Low |
| Location checkbox filter | **Dropped** — no location data exists anywhere in the schema | — | — |
| Date-range filter | Yes, via two `DatePicker`s instead of a slider | Medium | Low, in-memory only |
| Cross-filtering | Yes, via filter chips instead of chart clicks | Large | Low, in-memory only |
| Drill-down | Yes, via the "Today" quick-range button instead of chart-point clicks | Medium | Low |

---

## Usability rules (still the most important section)

1. **Every control needs a clear, short label** — no icon-only affordance.
2. **The default state must be useful immediately** — Last 30 Days on load,
   not an empty/unfiltered blob.
3. **Don't cram in every reference feature at reference density.**
4. **Touch/click targets match the rest of the app's buttons/inputs.**
5. **Every filter needs an obvious clear/reset affordance** — the "Clear
   filters" button resets date range, payment chip, and category chip
   together in one tap.
6. **If a feature makes the screen harder to read rather than easier,
   simplify or drop it** — this rule is *why* Design Deviations #1–3 above
   happened, not a rule that got skipped.

## Design-system & theme-parity rules (apply to everything above)

1. **No new colors, anywhere** — every brush via `DynamicResource` or
   `GetThemeColor`.
2. **Reuse existing styles.** The four new quick-range button styles
   (`RangeTodayButtonStyle` etc., in `CheckoutStyles.xaml`) are the one
   partial exception — WPF `Tag`-to-bool trigger matching is unreliable
   enough that four small dedicated styles, each mirroring
   `CashToggleButtonStyle`'s already-proven `DataTrigger` pattern exactly,
   were the lower-risk choice over one clever generic style. Everything
   else (chips, text, cards, the Clear-filters button) reuses
   `CategoryChipListBoxItemStyle`, `ThemeToggleButtonStyle`, and the
   standard card treatment as-is.
3. **Toggle light AND dark for every new element before calling this done**
   — filter chips, date pickers, quick-range buttons, the new category
   chart, all of it. Not yet done — see Testing checklist below.
4. **Toggle Arabic/RTL for every new element too.** `DashboardView`'s root
   inherits `FlowDirection` like every other screen, but WPF's `DatePicker`
   and `ListBox`-based chips haven't specifically been checked under RTL in
   this app before now — this is genuinely new ground, not a repeat of an
   already-proven pattern.

## Performance rules for old hardware (apply to everything above)

1. **Load from SQLite once per real data change, filter in memory after
   that** — implemented as the `RefreshDashboard` vs. `RecomputeAndRedraw`
   split described above.
2. **Cap chart data points** — implemented (weekly bucketing beyond 60 days,
   ~8-label thinning regardless of range).
3. **Reduce animations if they stutter** — not yet addressed; watch for this
   specifically during testing, nothing was preemptively disabled.
4. **No per-drag re-query** — not applicable now that there's no slider;
   `DatePicker` only fires its binding on a committed date selection, not
   per drag frame, so this concern doesn't apply to the chosen control.
5. **Test on the oldest hardware available before calling this done.**

---

## Testing checklist — this is the real "Definition of Done" now

Everything below is unchecked. This is the largest single untested change
in the project so far — bigger than Phase 5, with a real (if now much
smaller, per Design Deviations) LiveCharts-API uncertainty still present in
the chart-series construction code, plus entirely new interaction surface
(date pickers, filter chips, quick-range buttons) that's never been run once.

- [ ] Builds without error (biggest unknown: LiveCharts package API surface,
      same caveat as Stage 1/Phase 6 — flag any missing-member error and
      we'll fix it against whatever version is actually installed)
- [ ] Quick-range buttons (Today/Week/Month/Last 30) each show the right
      highlighted state and the right data
- [ ] Custom date range via the two date pickers works and correctly shows
      "Custom" (i.e. no quick-range button stays highlighted)
- [ ] Payment chips and category chips each filter correctly, and combine
      correctly with each other and with the date range
- [ ] "Clear filters" actually resets everything to Last 30 Days / All / All
- [ ] KPIs, the payment-split donut, and the Revenue by Category chart agree
      with each other under every filter combination tried
- [ ] A completed Checkout sale updates the dashboard immediately, still
      respecting whatever filter is currently active
- [ ] Light mode and dark mode, specifically on: date pickers, filter chips,
      quick-range buttons, the new category chart
- [ ] English and Arabic, specifically: chip labels, quick-range labels, and
      RTL layout of the whole filter bar (date pickers + chips have not been
      checked under RTL anywhere in this app before)
- [ ] Feels smooth — not just functional — on the oldest hardware available

---

## Explicitly out of scope, still at $0

- Any actual Power BI / cloud BI integration
- Real-time multi-user dashboards — single-till local app, unchanged
- Chart-click interactions (see Design Deviations #3) — not ruled out
  forever, just not the initial approach; could be added later as a bonus
  on top of the chip-based filtering, once there's a way to verify the
  LiveCharts event API actually compiles
- Matching reference density/complexity anywhere it conflicts with the
  Usability rules

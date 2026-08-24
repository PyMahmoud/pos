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
   **Known limitation at the time, since resolved (2026-08-24, second
   revision):** corners stayed square-ish because `Border.CornerRadius`
   isn't a `DatePicker` property, and the setters-only style couldn't reach
   it. Mahmoud saw the actual rendered result and flagged it as looking bad
   next to the rest of the app — the fuller `ControlTemplate` override
   deliberately avoided here was then picked up on his explicit request.
   See "Post-Stage-1 revision, round 3" below for what changed and what's
   still deliberately out of scope (the popup calendar's own appearance).

All three changes are **not yet build-tested**, same standing caveat as
everything else in this file.

### Post-Stage-1 revision, round 3 (2026-08-24)

Mahmoud saw the actual rendered date pickers and flagged them as looking
bad — plain gray box, square corners, mismatched calendar-icon button —
and asked for them to match the app's design system. This is exactly the
risk the round-2 revision above deliberately deferred (a full `DatePicker`
`ControlTemplate` override, correctly reproducing its
`PART_Root`/`PART_Button`/`PART_TextBox`/`PART_Popup` contract without a
WPF runtime to verify it against) — flagged as such before proceeding, per
his own stated preference for conflicts with a prior deliberate call to be
surfaced once, then actioned on explicit request.

**What changed**, in `CommonStyles.xaml`'s `ThemedDatePickerStyle`:
- Full `ControlTemplate` override: rounded 10px `Border` chrome matching
  every other input in the app (`ThemedTextBoxStyle`'s corner radius),
  themed background/border/focus-state colors, and a redrawn
  calendar-toggle button (a themed chevron `Path` instead of the plain
  system calendar glyph), with a hover state on the toggle button
  (`SurfaceContainerHighBrush`) and a focus state on the whole control
  (`PrimaryBrush` border, thickness 2) — mirroring `ThemedTextBoxStyle`'s
  existing focus-state pattern exactly, not inventing a new one.
- `PART_TextBox`, `PART_Button`, `PART_Popup` named exactly per the
  standard `DatePicker` part contract. `PART_Popup` is left with no
  explicit content — `DatePicker` assigns its own internal `Calendar`
  instance as that Popup's `Child` at runtime; the template must not add
  one itself.

**Deliberately still out of scope, flagged not hidden:** the popup
calendar's own appearance (the month/day grid that drops down when the
toggle button is clicked) still uses the default Windows `Calendar` control
look — restyling that is a separate, larger risk surface (`CalendarItem`,
`CalendarDayButton`, month/year navigation buttons all have their own PART
contracts) that the reported screenshot didn't actually show, since it only
captured the closed-picker state. If the open popup turns out to look
equally mismatched once this is actually seen in the running app, that's a
real fast-follow — same tradeoff pattern as the rest of this plan, not a
surprise gap.

**Not yet build-tested**, same standing caveat as everything else in this
file — and this one specifically carries the exact risk category the round-2
note above described: if the calendar popup silently stops opening once
built, that's the named risk materializing, not a mystery regression. Check
this first, before anything else, on the next build/run pass: click the
toggle button on both the From and To pickers and confirm the calendar
actually drops down and a date can be picked, in both light and dark theme,
and under Arabic/RTL.

### Post-Stage-1 revision, round 4 (2026-08-24) — chart colors in dark mode

Mahmoud sent a dark-mode screenshot: every chart's plot area was a plain
white rectangle sitting inside an otherwise-dark card, axis labels/gridlines
were unreadable-by-mismatch (not literally unreadable, just visibly the
wrong theme), and the hover tooltip was a light box floating on the dark
chart. Root cause, in two parts:

1. **Chart backgrounds were never set.** `CartesianChart`/`PieChart`
   default to an opaque (effectively white) background unless told
   otherwise — nothing in `DashboardView.xaml` overrode this, so every
   chart ignored the app's theme entirely regardless of light/dark. Fixed
   with `Background="Transparent"` on all four chart controls (three
   `CartesianChart`, one `PieChart`), so the surrounding card's own
   `SurfaceContainerBrush` shows through instead.
2. **Axis labels and gridlines were never given theme colors.** All six
   `Axis` instances (`TopItems`, `CategoryRevenue`, `RevenueTrend` × X/Y)
   were plain `new Axis()`, leaving `LabelsPaint`/`SeparatorsPaint` on
   LiveChartsCore's own default — fine-looking in light theme by
   coincidence, wrong in dark. Fixed with a new `ThemedAxis(...)` helper in
   `DashboardViewModel` that sets both from `OnSurfaceVariantColor` (labels)
   and `OutlineVariantColor` (gridlines) via the same `GetThemeColor` +
   `SolidColorPaint` pattern already proven for series colors and
   `ChartLegendTextPaint` — not a new API risk category, reuse of an
   already-working pattern. All six axis call sites now go through this one
   helper instead of repeating the setup, so a future palette tweak can't
   miss one.

**Also fixed while in the same area (same bug category, not scope creep):**
the hover tooltip (visible in the screenshot as a light box on "اتجاه
  الإيرادات — 0") had the same never-themed problem. Added
`ChartTooltipBackgroundPaint`/`ChartTooltipTextPaint` (from
`SurfaceContainerHighestColor`/`OnSurfaceColor`), set alongside
`ChartLegendTextPaint` in `RefreshDashboard` (same trigger set — real data
reload, theme toggle, language toggle), bound on all four chart controls.

**Not yet build-tested.** `Background` is a plain `FrameworkElement`
property, essentially zero API risk. `LabelsPaint`/`SeparatorsPaint` and
`TooltipBackgroundPaint`/`TooltipTextPaint` are the same `SolidColorPaint`
type and same general "paint property on a chart control" shape as
`ChartLegendTextPaint`, which is already in the codebase from the earlier
Stage-1 revision — so this carries that same, already-accepted level of
confidence, not a new uncertain area. Still: confirm on the next build,
specifically in dark theme — chart backgrounds blend into their cards, axis
text/gridlines are legible, and the hover tooltip is readable — then repeat
in light theme to confirm nothing regressed there.

### Post-Stage-1 revision, round 5 (2026-08-24) — Revenue in the Top Items tooltip

Mahmoud asked for the Top-Selling Items hover tooltip to also show total
revenue for that item (Quantity × Price, summed), not just Units sold —
named "Revenue" for consistency with every other revenue-labeled number on
this screen (Revenue KPI, Revenue Trend, Revenue by Category).

**This is a genuinely higher-risk change than anything else in this file so
far, and that's worth being direct about.** Every other chart here plots a
plain `double[]` — `Values`, a `Fill`/`Stroke` color, done. Showing a
second number in the tooltip needed the point to carry more than one value,
which meant:
- A new small model class, `TopItemPoint` (`ViewModels/TopItemPoint.cs`):
  `Name`, `Quantity`, `Revenue`.
- `ColumnSeries<TopItemPoint>` instead of `ColumnSeries<double>` — first
  custom-model series in the app.
- A `Mapping` delegate telling LiveCharts how to turn each `TopItemPoint`
  into a plotted point (`point.PrimaryValue = item.Quantity`,
  `point.SecondaryValue = point.Context.Index`) — bar heights are
  unchanged from before, only the underlying type changed.
- A `YToolTipLabelFormatter` reading `point.Model.Quantity` /
  `point.Model.Revenue` back off the hovered point to build the two-line
  tooltip text.

All four of `Mapping`, `point.Context.Index`, `YToolTipLabelFormatter`, and
`point.Model` are documented LiveChartsCore v2 API for exactly this
"plot a custom type, customize its tooltip" use case — not guessed at
random — but unlike `Background`, `LabelsPaint`, or `TooltipTextPaint`
(round 4, just above), none of these four have been used anywhere in this
codebase before now, so there's no prior working call in this project to
point at as precedent. If a build error shows up here specifically, it's
the most likely place in the whole Dashboard for a real missing-member/
signature mismatch against whatever LiveChartsCore version actually
installs — flag it plainly and fix against the real API, this is exactly
the kind of thing the standing LiveCharts caveat throughout this plan is
for, not a guess to be made twice.

**Not yet build-tested.** Once it builds, worth checking specifically:
hovering each of the 5 bars shows both numbers correctly matched to that
item (not off-by-one against the wrong bar — a real risk with any custom
index mapping), the tooltip is still legible in dark mode with the round-4
tooltip paint, and Arabic/RTL number formatting reads sensibly.

### Post-Stage-1 revision, round 6 (2026-08-24) — Profit added to every hover tooltip except Payment Split

Mahmoud asked for Profit to show up on hover for the Revenue Trend line,
the Revenue by Category bars, and the Top Items bars (Top Items already had
Revenue from round 5 above; this adds Profit alongside it) — explicitly
**not** the Payment Split pie chart, which stays as-is.

Same pattern as round 5 for all three, since that's the established,
lowest-risk way to plot a custom type in this codebase now:
- `TopItemPoint` gained a `Profit` field (`g.Sum(s => s.Earned)` per item);
  its `YToolTipLabelFormatter` now shows Units sold / Revenue / Profit.
- Two new model classes, `RevenueTrendPoint` (`Revenue`, `Profit`) and
  `CategoryRevenuePoint` (`Category`, `Revenue`, `Profit`) —
  `RevenueTrendSeries` and `CategoryRevenueSeries` moved from plain
  `LineSeries<double>`/`ColumnSeries<double>` to `LineSeries<RevenueTrendPoint>`/
  `ColumnSeries<CategoryRevenuePoint>`, each with its own `Mapping`
  (`(item, index) => new Coordinate(index, item.Revenue)` — bar/line
  heights unchanged, only the underlying type changed) and
  `YToolTipLabelFormatter` reading `point.Model.Revenue`/`point.Model.Profit`.
- Profit label reuses the existing `DashboardTodayProfit` resource key ("Profit"
  in English) already used by the KPI card, rather than adding a new
  localization key for the same word.
- All three new/changed model-class `.cs` files added to
  `PosSystem.App.csproj`'s `<Compile Include>` list — this project is the
  old-style, non-SDK-style `.csproj`, so a file sitting on disk without an
  explicit `<Compile Include>` entry is invisible to the compiler regardless
  of its content (this exact mistake happened with `TopItemPoint.cs` in
  round 5 and produced CS0246 on first build — fixed once already, not
  repeated here).
- Payment Split (`BuildPaymentSplitChart`, `PieSeries<double>`)
  deliberately untouched, per the explicit instruction to leave it as-is.

**Not yet build-tested.** Carries the same confidence level as round 5's
Top Items change, now applied to two more series — worth checking
specifically: hovering the Revenue Trend line and every Revenue by Category
bar shows Revenue and Profit correctly matched to that point/bar (same
off-by-one risk as round 5, now in two more places), Payment Split's
tooltip is unchanged from before this round, and dark-mode tooltip legibility
(round 4's paint) still holds for the two newly-custom-typed series.

### Post-Stage-1 revision, round 7 (2026-08-25) — Top Items now respects filters (reverses round 5's "all-time, unfiltered" call)

Mahmoud reported Top Items didn't update when picking a category, and I
flagged that this was deliberate (see round 5 and "Explicitly NOT filtered"
below) rather than fixing it silently — per his standing preference, a
conflict with an existing written decision gets stated once, then goes
with whatever he decides. He initially confirmed keeping it unfiltered, then
reversed that: since the **All Time** quick-range button (added round 2)
already covers "what sells best, ever" explicitly, a permanently-unfiltered
exception isn't needed alongside it — so Top Items should just be another
filtered chart like the rest.

**What changed:**
- `BuildTopItemsChart` now takes `sellsFiltered` and is called from
  `ApplyFiltersAndRebuildCharts` (the shared cross-filter pipeline), not
  from `RefreshDashboard` with the raw all-time `_cachedSells`. Date range,
  payment chip, and category chip all now apply to it, same as Payment
  Split, Category Revenue, and Revenue Trend.
- Title changed from "Top-Selling Items (All-Time)" to plain "Top-Selling
  Items" in both `Strings.English.xaml` and `Strings.Arabic.xaml` — the
  "(All-Time)" qualifier would now be actively wrong whenever any filter
  other than the All Time quick-range is active. The shared
  `ActiveRangeText` line already shown above the filter bar covers "what
  period is this" the same way it does for every other chart, so no
  chart-specific date qualifier is needed in the title itself.
- The "Explicitly NOT filtered" line further down this file and the
  Design Deviations section are now historical — they describe why this
  call was originally made, not current behavior. Left in place rather than
  deleted, since the reasoning (why an unfiltered exception seemed useful
  in the first place) is still useful context for why this reversal is a
  deliberate override and not just noticing an oversight.

**Not yet build-tested.** No new LiveCharts API surface here — same
`ColumnSeries<TopItemPoint>`/`Mapping`/`YToolTipLabelFormatter` shape from
rounds 5–6, just fed a different (filtered) list. Worth checking
specifically: Top Items now changes when date range/payment/category
filters change, the bars still correctly rank by Quantity within whatever's
filtered (a category filter could plausibly leave fewer than 5 items —
confirm the chart handles that without erroring), and the title reads
correctly (no leftover "All-Time" text) in both languages.

### Post-Stage-1 revision, round 8 (2026-08-25) — Units sold added to Revenue by Category's tooltip

Mahmoud asked for units sold alongside Revenue/Profit on the Revenue by
Category bars' hover tooltip. Same established pattern as every round since
5: `CategoryRevenuePoint` gained a `Quantity` field (`g.Sum(s => s.Quantity)`
per category), and the tooltip formatter now reads Units sold / Revenue /
Profit, in that order — matching Top Items' own tooltip layout for
consistency between the two bar charts. Reuses the existing
`DashboardUnitsSoldLabel` ("Units sold") resource key already used by Top
Items, no new localization key needed.

**Not yet build-tested.** Same low-risk shape as rounds 6–7 — an existing
custom-model series gaining one more field and one more tooltip line, no new
Mapping/API surface. Worth checking Revenue by Category's tooltip shows all
three numbers correctly matched per bar.

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

**Explicitly NOT filtered:** the Outstanding Customer Debt KPI stays
unfiltered on purpose — it's a live balance snapshot, not a period metric,
and filtering a snapshot by date range wouldn't mean anything real.
(Top-Selling Items used to be unfiltered too, for a similar-sounding but
distinct reason; see round 7 above for why that was reversed — it now
respects every filter like the rest of the charts.)

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

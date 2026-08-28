using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Input;
using PosSystem.App.Localization;
using PosSystem.App.Theming;
using PosSystem.Core.Reporting;

namespace PosSystem.App.ViewModels
{
    /// <summary>
    /// Which quick-range button (if any) is active for the Excel export
    /// section — same enum-plus-IsXSelected-bool shape as Dashboard's own
    /// DashboardQuickRange (see that enum's doc comment); a separate enum
    /// rather than reusing DashboardQuickRange since the two pickers don't
    /// offer the same set of presets (export adds Last 7 Days and This
    /// Year, which Dashboard's filter never needed; export has no All Time,
    /// since "export everything" is just as easily a manually-picked wide
    /// custom range).
    /// </summary>
    public enum ExportQuickRange
    {
        Last7Days,
        Last30Days,
        ThisWeek,
        ThisMonth,
        ThisYear,
        Custom
    }

    /// <summary>
    /// DataContext for SettingsView (rewritten 2026-08-26 into a real,
    /// full settings screen). Four sections, each independent of the
    /// others:
    ///
    /// - Appearance: Language and Theme, both already existed as static
    ///   managers (LocalizationManager/ThemeManager) with their own toggle
    ///   buttons scattered elsewhere (sidebar footer for Theme, a bare
    ///   "Toggle Language" button that used to live directly on this
    ///   screen) -- surfaced here properly as two labeled, two-option
    ///   segmented pickers that show which one is actually active, not
    ///   just a blind toggle. Applies instantly on click (same as those
    ///   old toggle buttons always did) -- no Save step, since there's
    ///   nothing to validate.
    /// - Preferences: Tax Rate and Low Stock Threshold, unchanged from
    ///   before -- see AppSettings' class doc comment for why these two
    ///   exist and what they drive (Checkout's tax line, Inventory's
    ///   stock badges).
    /// - Data & Backup (new): this app has exactly one file that matters
    ///   -- rovaShop.db (Core.Data.Server.fullpath) -- and, until now, no
    ///   way to protect it from being lost short of a manual file copy
    ///   outside the app. Back Up Now copies it, timestamped, into a
    ///   Backups folder next to it; Open Backups Folder reveals that
    ///   folder in Explorer. Deliberately NOT a restore feature -- restoring
    ///   the wrong file over a live database while the app (and its open
    ///   SQLite connections) is running is a real way to corrupt data, and
    ///   this app has no "restart into maintenance mode" concept to do it
    ///   safely. Restore stays a manual, deliberate file-copy action with
    ///   the app closed, same as it is today -- this just makes sure a good
    ///   copy exists to restore FROM.
    /// - About: static, read-only -- app name and version (from this
    ///   assembly's own AssemblyVersion, Properties/AssemblyInfo.cs), so
    ///   there's a real place to point someone asking "what version is
    ///   this" without them having to check file properties in Explorer.
    /// - Export (Phase 11 #3, 2026-08-28): builds an .xlsx report
    ///   (Summary/Bills/Sales Detail sheets — see
    ///   PosSystem.Core.Reporting.SalesExportService's own doc comment for
    ///   the full shape) covering a chosen date range, via the same
    ///   quick-range-button-plus-custom-date-pickers pattern Dashboard's
    ///   filter already established (ExportQuickRange mirrors
    ///   DashboardQuickRange). Gated behind the shared AdminSession — a
    ///   full export of every sale's revenue/profit is at least as
    ///   sensitive as Dashboard's on-screen totals, which are already
    ///   gated, and Phase 11's own admin-password note already flagged
    ///   Excel export as meant to share this same gate once built. Unlike
    ///   Dashboard/Bills' full-screen lock overlay, this follows
    ///   Inventory's smaller inline-unlock-box pattern (just this one card
    ///   swaps to an unlock prompt, the rest of Settings stays reachable),
    ///   since gating the WHOLE Settings screen over one section would also
    ///   block Appearance/Preferences, which aren't sensitive.
    ///
    /// Same validate-in-the-Save-method-then-set-StatusMessage shape the
    /// Preferences section already used (and every other form in this app
    /// -- InventoryViewModel.AddProduct, CustomersViewModel's add-customer
    /// flow) for the one section that still needs it; Appearance and Data
    /// & Backup are single-click actions with nothing to validate, so they
    /// skip straight to doing the thing and reporting the result in the
    /// same shared StatusMessage.
    /// </summary>
    public class SettingsViewModel : ViewModelBase
    {
        private string _taxRatePercentInput = "";
        public string TaxRatePercentInput
        {
            get => _taxRatePercentInput;
            set => SetProperty(ref _taxRatePercentInput, value);
        }

        private string _lowStockThresholdInput = "";
        public string LowStockThresholdInput
        {
            get => _lowStockThresholdInput;
            set => SetProperty(ref _lowStockThresholdInput, value);
        }

        private string _statusMessage = "";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        // Appearance -- see class doc comment. Both pairs of bools exist
        // only so the XAML's segmented-button styles (LanguageEnglish/
        // LanguageArabicButtonStyle, ThemeLight/ThemeDarkButtonStyle in
        // CheckoutStyles.xaml) have a DataTrigger to bind against, same
        // IsXSelected-per-option pattern CheckoutViewModel's
        // IsCashSelected/IsCardSelected/IsPayLaterSelected already
        // established for its own segmented Cash/Card/Pay Later row.
        private bool _isEnglishSelected;
        public bool IsEnglishSelected
        {
            get => _isEnglishSelected;
            private set => SetProperty(ref _isEnglishSelected, value);
        }

        private bool _isArabicSelected;
        public bool IsArabicSelected
        {
            get => _isArabicSelected;
            private set => SetProperty(ref _isArabicSelected, value);
        }

        private bool _isLightThemeSelected;
        public bool IsLightThemeSelected
        {
            get => _isLightThemeSelected;
            private set => SetProperty(ref _isLightThemeSelected, value);
        }

        private bool _isDarkThemeSelected;
        public bool IsDarkThemeSelected
        {
            get => _isDarkThemeSelected;
            private set => SetProperty(ref _isDarkThemeSelected, value);
        }

        // Data & Backup -- see class doc comment. Read-only display values,
        // refreshed on load and after every successful backup so the size
        // shown is never more than one action stale.
        private string _databasePathDisplay = "";
        public string DatabasePathDisplay
        {
            get => _databasePathDisplay;
            private set => SetProperty(ref _databasePathDisplay, value);
        }

        private string _databaseSizeDisplay = "";
        public string DatabaseSizeDisplay
        {
            get => _databaseSizeDisplay;
            private set => SetProperty(ref _databaseSizeDisplay, value);
        }

        // About -- see class doc comment. "2" -> Major.Minor only ("1.0"),
        // not the full four-part AssemblyVersion -- Build/Revision on a
        // WPF desktop app like this one aren't meaningfully different
        // numbers a shop owner would ever need to read off this screen.
        public string AppVersionDisplay { get; } =
            "v" + (Assembly.GetExecutingAssembly().GetName().Version?.ToString(2) ?? "1.0");

        // Admin password (#7, 2026-08-27) — write-only fields (never
        // pre-filled with anything derived from the stored hash — there's
        // no way to recover the plain password from it anyway, and
        // pre-filling a password field with placeholder text invites
        // confusion about whether it's really unchanged or not). Blank +
        // blank + Save clears the password entirely (see
        // AppSettings.SetAdminPassword's doc comment on why that's the
        // deliberate recovery path, not a bug).
        private string _newAdminPasswordInput = "";
        public string NewAdminPasswordInput
        {
            get => _newAdminPasswordInput;
            set => SetProperty(ref _newAdminPasswordInput, value);
        }

        private string _confirmAdminPasswordInput = "";
        public string ConfirmAdminPasswordInput
        {
            get => _confirmAdminPasswordInput;
            set => SetProperty(ref _confirmAdminPasswordInput, value);
        }

        public bool HasAdminPassword => AppSettings.HasAdminPassword;

        public ICommand SaveAdminPasswordCommand { get; }

        private void SaveAdminPassword()
        {
            if (NewAdminPasswordInput != ConfirmAdminPasswordInput)
            {
                StatusMessage = LocalizationManager.GetString("SettingsAdminPasswordMismatch");
                return;
            }

            AppSettings.SetAdminPassword(NewAdminPasswordInput);
            NewAdminPasswordInput = "";
            ConfirmAdminPasswordInput = "";
            OnPropertyChanged(nameof(HasAdminPassword));

            // Setting a NEW password re-locks every gated screen (Dashboard,
            // Inventory's product/category CRUD, Excel export) immediately —
            // someone just turned protection on. Clearing it back to blank
            // un-gates all of them, since there's no password left to enter.
            // See AdminSession.ResetForPasswordChange's doc comment.
            AdminSession.ResetForPasswordChange();

            StatusMessage = LocalizationManager.GetString(
                AppSettings.HasAdminPassword ? "SettingsAdminPasswordSaveSuccess" : "SettingsAdminPasswordCleared");
        }

        public ICommand SaveCommand { get; }
        public ICommand SetLanguageCommand { get; }
        public ICommand SetThemeCommand { get; }
        public ICommand BackupNowCommand { get; }
        public ICommand OpenBackupsFolderCommand { get; }

        // ----- Export (Phase 11 #3, 2026-08-28) — see class doc comment -----

        public bool IsExportUnlocked => AdminSession.IsUnlocked;
        public bool IsExportLocked => !IsExportUnlocked;

        private string _exportUnlockPasswordInput = "";
        public string ExportUnlockPasswordInput
        {
            get => _exportUnlockPasswordInput;
            set => SetProperty(ref _exportUnlockPasswordInput, value);
        }

        private string _exportUnlockError = "";
        public string ExportUnlockError
        {
            get => _exportUnlockError;
            set => SetProperty(ref _exportUnlockError, value);
        }

        public ICommand ExportUnlockCommand { get; }

        private void ExportUnlock()
        {
            if (AdminSession.TryUnlock(ExportUnlockPasswordInput))
            {
                ExportUnlockError = "";
                ExportUnlockPasswordInput = "";
            }
            else
            {
                ExportUnlockError = LocalizationManager.GetString("DashboardUnlockIncorrect");
            }
        }

        // Set true while ApplyExportQuickRange is setting both dates at
        // once, so the ExportStartDate/ExportEndDate setters below don't
        // stomp ActiveExportRange back to Custom mid-assignment — identical
        // reasoning to DashboardViewModel's own _isApplyingQuickRange.
        private bool _isApplyingExportQuickRange;

        private DateTime? _exportStartDate;
        public DateTime? ExportStartDate
        {
            get => _exportStartDate;
            set
            {
                if (!SetProperty(ref _exportStartDate, value)) return;
                if (!_isApplyingExportQuickRange) ActiveExportRange = ExportQuickRange.Custom;
            }
        }

        private DateTime? _exportEndDate;
        public DateTime? ExportEndDate
        {
            get => _exportEndDate;
            set
            {
                if (!SetProperty(ref _exportEndDate, value)) return;
                if (!_isApplyingExportQuickRange) ActiveExportRange = ExportQuickRange.Custom;
            }
        }

        private ExportQuickRange _activeExportRange = ExportQuickRange.Last30Days;
        public ExportQuickRange ActiveExportRange
        {
            get => _activeExportRange;
            private set
            {
                if (!SetProperty(ref _activeExportRange, value)) return;
                OnPropertyChanged(nameof(IsExportRangeLast7Selected));
                OnPropertyChanged(nameof(IsExportRangeLast30Selected));
                OnPropertyChanged(nameof(IsExportRangeThisWeekSelected));
                OnPropertyChanged(nameof(IsExportRangeThisMonthSelected));
                OnPropertyChanged(nameof(IsExportRangeThisYearSelected));
            }
        }

        // Same IsXSelected-bool-per-option pattern Dashboard's own quick
        // range buttons and Checkout's Cash/Card/Pay Later buttons already
        // use.
        public bool IsExportRangeLast7Selected => ActiveExportRange == ExportQuickRange.Last7Days;
        public bool IsExportRangeLast30Selected => ActiveExportRange == ExportQuickRange.Last30Days;
        public bool IsExportRangeThisWeekSelected => ActiveExportRange == ExportQuickRange.ThisWeek;
        public bool IsExportRangeThisMonthSelected => ActiveExportRange == ExportQuickRange.ThisMonth;
        public bool IsExportRangeThisYearSelected => ActiveExportRange == ExportQuickRange.ThisYear;

        public ICommand SetExportQuickRangeCommand { get; }
        public ICommand ExportToExcelCommand { get; }
        public ICommand OpenExportsFolderCommand { get; }

        private void ApplyExportQuickRange(ExportQuickRange range)
        {
            _isApplyingExportQuickRange = true;
            try
            {
                DateTime today = DateTime.Today;
                switch (range)
                {
                    case ExportQuickRange.Last7Days:
                        ExportStartDate = today.AddDays(-6);
                        ExportEndDate = today;
                        break;
                    case ExportQuickRange.Last30Days:
                        ExportStartDate = today.AddDays(-29);
                        ExportEndDate = today;
                        break;
                    case ExportQuickRange.ThisWeek:
                        // Week starts Saturday — same regional convention
                        // DashboardViewModel.ApplyQuickRange already uses.
                        int daysSinceSaturday = ((int)today.DayOfWeek + 1) % 7;
                        ExportStartDate = today.AddDays(-daysSinceSaturday);
                        ExportEndDate = today;
                        break;
                    case ExportQuickRange.ThisMonth:
                        ExportStartDate = new DateTime(today.Year, today.Month, 1);
                        ExportEndDate = today;
                        break;
                    case ExportQuickRange.ThisYear:
                        ExportStartDate = new DateTime(today.Year, 1, 1);
                        ExportEndDate = today;
                        break;
                    default:
                        ExportStartDate = today.AddDays(-29);
                        ExportEndDate = today;
                        break;
                }
                ActiveExportRange = range;
            }
            finally
            {
                _isApplyingExportQuickRange = false;
            }
        }

        /// <summary>
        /// Builds a SalesExportLabels from whichever language is currently
        /// active — see that class's own doc comment for why Core stays
        /// unaware LocalizationManager even exists, and this translation
        /// happens here instead.
        /// </summary>
        private static SalesExportLabels BuildExportLabels()
        {
            string L(string key) => LocalizationManager.GetString(key);
            return new SalesExportLabels
            {
                ReportTitle = L("ExportReportTitle"),
                DateRangeLabel = L("ExportDateRangeLabel"),
                GeneratedLabel = L("ExportGeneratedLabel"),
                SummarySheetName = L("ExportSummarySheetName"),
                TotalRevenueLabel = L("DashboardTodayRevenue"),
                TotalProfitLabel = L("DashboardTodayProfit"),
                TotalTransactionsLabel = L("DashboardTodayTransactions"),
                CashTotalLabel = L("CheckoutCash"),
                CardTotalLabel = L("CheckoutCard"),
                PayLaterTotalLabel = L("CheckoutPayLater"),
                NoDataMessage = L("ExportNoDataMessage"),
                BillsSheetName = L("BillsBrowserTitle"),
                ColBillNumber = L("ExportColBillNumber"),
                ColDate = L("ExportColDate"),
                ColTime = L("ExportColTime"),
                ColCustomer = L("CheckoutCustomerLabel"),
                ColPhone = L("CustomersPhoneField"),
                ColPaymentMethod = L("ExportColPaymentMethod"),
                ColPaymentStatus = L("ExportColPaymentStatus"),
                PaymentStatusPaidLabel = L("ExportPaymentStatusPaid"),
                PaymentStatusPartialLabel = L("ExportPaymentStatusPartial"),
                PaymentStatusUnpaidLabel = L("ExportPaymentStatusUnpaid"),
                ColItems = L("ExportColItems"),
                ColSubtotal = L("CheckoutSubtotal"),
                ColDiscount = L("ExportColDiscount"),
                ColTax = L("CheckoutTax"),
                ColTotal = L("CheckoutTotal"),
                ColPaid = L("CustomersBalancePaidUp"),
                ColRemaining = L("CustomersPaymentAmountLabel"),
                SalesDetailSheetName = L("ExportSalesDetailSheetName"),
                ColProduct = L("ExportColProduct"),
                ColCategory = L("InventoryProductCategoryLabel"),
                ColQuantity = L("InventoryQuantityLabel"),
                ColUnitPrice = L("ExportColUnitPrice"),
                ColUnitCost = L("InventoryProductCostLabel"),
                ColLineTotal = L("ExportColLineTotal"),
                ColProfit = L("ExportColProfit"),
                ColReturned = L("ExportColReturned"),
                ReturnedYesLabel = L("ExportReturnedYes"),
                ReturnedNoLabel = L("ExportReturnedNo"),
                WalkInLabel = L("BillsBrowserWalkInLabel")
            };
        }

        private void ExportToExcel()
        {
            if (!IsExportUnlocked)
            {
                StatusMessage = LocalizationManager.GetString("ExportAdminRequired");
                return;
            }

            DateTime start = (ExportStartDate ?? DateTime.Today.AddDays(-29)).Date;
            DateTime end = (ExportEndDate ?? DateTime.Today).Date;
            if (start > end)
            {
                StatusMessage = LocalizationManager.GetString("ExportInvalidRange");
                return;
            }

            try
            {
                string exportsFolder = Path.Combine(Core.Data.Server.Location, "Exports");
                // Timestamped, same reasoning as BackupNow's filenames below —
                // exporting the same range twice (e.g. re-running "This
                // Month" partway through the month) shouldn't silently
                // overwrite the earlier file.
                string fileName = "SalesReport_" + start.ToString("yyyy-MM-dd") + "_to_" + end.ToString("yyyy-MM-dd")
                    + "_" + DateTime.Now.ToString("HHmmss") + ".xlsx";
                string outputPath = Path.Combine(exportsFolder, fileName);

                SalesExportService.Export(start, end, outputPath, BuildExportLabels());

                StatusMessage = string.Format(LocalizationManager.GetString("ExportSuccess"), fileName);
            }
            catch (Exception ex)
            {
                StatusMessage = LocalizationManager.GetString("ExportError") + " (" + ex.Message + ")";
            }
        }

        private void OpenExportsFolder()
        {
            try
            {
                string exportsFolder = Path.Combine(Core.Data.Server.Location, "Exports");
                Directory.CreateDirectory(exportsFolder);
                Process.Start("explorer.exe", "\"" + exportsFolder + "\"");
            }
            catch (Exception ex)
            {
                StatusMessage = LocalizationManager.GetString("ExportError") + " (" + ex.Message + ")";
            }
        }

        public SettingsViewModel()
        {
            SaveCommand = new RelayCommand(Save);
            SetLanguageCommand = new RelayCommand(p =>
            {
                if (p is AppLanguage language) LocalizationManager.SwitchLanguage(language);
            });
            SetThemeCommand = new RelayCommand(p =>
            {
                if (p is AppTheme theme) ThemeManager.SwitchTheme(theme);
            });
            BackupNowCommand = new RelayCommand(_ => BackupNow());
            OpenBackupsFolderCommand = new RelayCommand(_ => OpenBackupsFolder());
            SaveAdminPasswordCommand = new RelayCommand(_ => SaveAdminPassword());
            ExportUnlockCommand = new RelayCommand(_ => ExportUnlock());
            SetExportQuickRangeCommand = new RelayCommand(p =>
            {
                if (p is ExportQuickRange range) ApplyExportQuickRange(range);
            });
            ExportToExcelCommand = new RelayCommand(_ => ExportToExcel());
            OpenExportsFolderCommand = new RelayCommand(_ => OpenExportsFolder());

            AdminSession.Changed += () =>
            {
                OnPropertyChanged(nameof(IsExportUnlocked));
                OnPropertyChanged(nameof(IsExportLocked));
            };

            // Neither manager is owned by this ViewModel -- Theme can
            // change from the sidebar's own toggle button (still there,
            // untouched) and Language could in principle change from
            // anywhere else that ever calls LocalizationManager.SwitchLanguage
            // directly -- so both selections have to stay live-synced via
            // these events rather than only being set once at construction.
            LocalizationManager.LanguageChanged += _ => RefreshLanguageSelection();
            ThemeManager.ThemeChanged += _ => RefreshThemeSelection();

            RefreshLanguageSelection();
            RefreshThemeSelection();
            RefreshDatabaseInfo();
            LoadFromAppSettings();
            ApplyExportQuickRange(ExportQuickRange.Last30Days);
        }

        private void RefreshLanguageSelection()
        {
            IsEnglishSelected = LocalizationManager.Current == AppLanguage.English;
            IsArabicSelected = LocalizationManager.Current == AppLanguage.Arabic;
        }

        private void RefreshThemeSelection()
        {
            IsLightThemeSelected = ThemeManager.Current == AppTheme.Light;
            IsDarkThemeSelected = ThemeManager.Current == AppTheme.Dark;
        }

        private void RefreshDatabaseInfo()
        {
            DatabasePathDisplay = Core.Data.Server.fullpath;
            try
            {
                var info = new FileInfo(Core.Data.Server.fullpath);
                DatabaseSizeDisplay = info.Exists
                    ? FormatFileSize(info.Length)
                    : LocalizationManager.GetString("SettingsDatabaseNotFound");
            }
            catch
            {
                // Path/permissions issue reading file info -- not fatal,
                // just leave the size blank rather than blocking the rest
                // of this screen from working.
                DatabaseSizeDisplay = "";
            }
        }

        private static string FormatFileSize(long bytes)
        {
            double kb = bytes / 1024.0;
            if (kb < 1024) return kb.ToString("0.0", CultureInfo.InvariantCulture) + " KB";
            return (kb / 1024.0).ToString("0.0", CultureInfo.InvariantCulture) + " MB";
        }

        private void BackupNow()
        {
            try
            {
                string backupsFolder = Path.Combine(Core.Data.Server.Location, "Backups");
                Directory.CreateDirectory(backupsFolder);

                // Timestamped, not overwritten in place -- the whole point
                // of a backup is surviving a mistake made AFTER it was
                // taken, which a single always-overwritten "backup.db"
                // can't do (the mistake gets backed up too, over the only
                // good copy).
                string fileName = "rovaShop_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".db";
                string destinationPath = Path.Combine(backupsFolder, fileName);

                File.Copy(Core.Data.Server.fullpath, destinationPath, overwrite: false);

                RefreshDatabaseInfo();
                StatusMessage = string.Format(LocalizationManager.GetString("SettingsBackupSuccess"), fileName);
            }
            catch (Exception ex)
            {
                StatusMessage = LocalizationManager.GetString("SettingsBackupError") + " (" + ex.Message + ")";
            }
        }

        private void OpenBackupsFolder()
        {
            try
            {
                string backupsFolder = Path.Combine(Core.Data.Server.Location, "Backups");
                Directory.CreateDirectory(backupsFolder);
                Process.Start("explorer.exe", "\"" + backupsFolder + "\"");
            }
            catch (Exception ex)
            {
                StatusMessage = LocalizationManager.GetString("SettingsBackupError") + " (" + ex.Message + ")";
            }
        }

        private void LoadFromAppSettings()
        {
            TaxRatePercentInput = AppSettings.TaxRatePercent.ToString(CultureInfo.InvariantCulture);
            LowStockThresholdInput = AppSettings.LowStockThreshold.ToString(CultureInfo.InvariantCulture);
        }

        private void Save()
        {
            if (!double.TryParse(TaxRatePercentInput, NumberStyles.Float, CultureInfo.InvariantCulture, out double taxRate)
                || taxRate < 0 || taxRate > 100)
            {
                StatusMessage = LocalizationManager.GetString("SettingsTaxRateInvalid");
                return;
            }

            if (!double.TryParse(LowStockThresholdInput, NumberStyles.Float, CultureInfo.InvariantCulture, out double lowStockThreshold)
                || lowStockThreshold < 0)
            {
                StatusMessage = LocalizationManager.GetString("SettingsLowStockThresholdInvalid");
                return;
            }

            try
            {
                AppSettings.Save(taxRate, lowStockThreshold);
                StatusMessage = LocalizationManager.GetString("SettingsSaveSuccess");
            }
            catch (Exception ex)
            {
                StatusMessage = LocalizationManager.GetString("SettingsSaveError") + " (" + ex.Message + ")";
            }
        }
    }
}

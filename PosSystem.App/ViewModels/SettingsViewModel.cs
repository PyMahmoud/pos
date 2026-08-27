using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows.Input;
using PosSystem.App.Localization;
using PosSystem.App.Theming;

namespace PosSystem.App.ViewModels
{
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

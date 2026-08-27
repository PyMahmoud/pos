using System;

namespace PosSystem.App
{
    /// <summary>
    /// Static, app-wide "is the admin-gated area currently unlocked"
    /// session flag — added 2026-08-27 (item #7's extension beyond just
    /// Dashboard). Originally each gated screen (Dashboard, then Inventory)
    /// carried its own private "unlocked" bool; this replaced that once
    /// Mahmoud confirmed the same password should gate multiple screens
    /// (Dashboard access, Inventory's product/category CRUD, and the
    /// upcoming Excel export) — unlocking once on ANY of them now unlocks
    /// all of them for the rest of the app session, rather than prompting
    /// again per screen. Same static/event-driven shape as
    /// CustomerDataEvents/InventoryDataEvents/OrderEvents elsewhere in this
    /// app: several ViewModels that don't otherwise share state each need
    /// to read and react to this one flag.
    ///
    /// Session-scoped only — deliberately not persisted anywhere (no
    /// settings-table row, no "remember me"). Every fresh app launch starts
    /// locked again if a password is set. That's a real design choice, not
    /// an oversight: this is a single-till shop app, one login the whole
    /// shift needs to share, not a scheme for distinguishing individual
    /// staff logins.
    ///
    /// If AppSettings.HasAdminPassword is false (no password ever set),
    /// IsUnlocked reports true unconditionally — a fresh install, or a shop
    /// that never opted into this feature, sees every "gated" screen exactly
    /// as open as it was before #7 existed. This is checked live against
    /// AppSettings on every read rather than cached, so clearing the
    /// password from Settings immediately un-gates everything without
    /// requiring a restart.
    /// </summary>
    public static class AdminSession
    {
        private static bool _isUnlockedThisSession;

        /// <summary>
        /// Fired whenever the effective unlocked/locked state changes —
        /// after a successful TryUnlock, and from ResetForPasswordChange.
        /// Every gated ViewModel subscribes to re-raise its own
        /// IsUnlocked/IsLocked-style properties (see DashboardViewModel,
        /// InventoryViewModel).
        /// </summary>
        public static event Action Changed;

        public static bool IsUnlocked => !AppSettings.HasAdminPassword || _isUnlockedThisSession;

        /// <summary>
        /// Attempts to unlock the session with the given password. Returns
        /// true (and unlocks) on a correct password, or when no admin
        /// password is set at all (nothing to check against — matches
        /// AppSettings.VerifyAdminPassword's own "no password set means
        /// anything verifies" behavior, kept consistent here rather than
        /// re-deciding it a second way).
        /// </summary>
        public static bool TryUnlock(string attempt)
        {
            if (!AppSettings.VerifyAdminPassword(attempt)) return false;

            _isUnlockedThisSession = true;
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// Called by SettingsViewModel right after AppSettings.SetAdminPassword
        /// (whether that set a brand-new password or cleared it back to
        /// none). Always re-locks: setting a NEW password should require the
        /// person to actually enter it before continuing (they just proved
        /// they know the OLD one to get this far, not the new one they typed
        /// once with no confirmation of correctness beyond matching a second
        /// text box); clearing the password back to none is harmless to
        /// re-lock too, since IsUnlocked above reports true unconditionally
        /// the moment HasAdminPassword is false anyway.
        /// </summary>
        public static void ResetForPasswordChange()
        {
            _isUnlockedThisSession = false;
            Changed?.Invoke();
        }
    }
}

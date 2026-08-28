using System;

namespace PosSystem.App
{
    /// <summary>
    /// Contact details shown on Settings' "Contact Support" card (Phase 11
    /// #4). Plain constants, not a Settings-table entry like AppSettings --
    /// this is Mahmoud's own support contact info, not something a shop
    /// owner using the app would ever need to change from inside it, so
    /// there's no in-app editor for these. Edit the values below and
    /// rebuild whenever the real number/email is ready or changes.
    ///
    /// TODO(Mahmoud): replace the placeholder values below with your real
    /// phone number, WhatsApp number, and support email before shipping to
    /// the client -- everything else (the card, the buttons, the tel:/
    /// wa.me/mailto: links) is already wired up and working against
    /// whatever is here.
    /// </summary>
    public static class ContactInfo
    {
        /// <summary>
        /// Shown on screen exactly as typed here -- format it however you
        /// want it to read, e.g. "+20 10 1234 5678".
        /// </summary>
        public const string PhoneDisplay = "+20 100 000 0000";

        /// <summary>
        /// Same number used to build the "tel:" link the Call button opens
        /// -- digits only plus a single leading "+", no spaces or dashes
        /// (some phone/dialer apps on Windows are picky about extra
        /// formatting characters in a tel: URI).
        /// </summary>
        public const string PhoneUri = "+201000000000";

        /// <summary>
        /// WhatsApp number in wa.me's own format: country code + number,
        /// digits only, NO leading "+" (e.g. Egypt "201000000000", not
        /// "+201000000000").
        /// </summary>
        public const string WhatsAppNumber = "201000000000";

        public const string SupportEmail = "support@example.com";
    }
}

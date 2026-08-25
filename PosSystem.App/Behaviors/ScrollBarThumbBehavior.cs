namespace PosSystem.App.Behaviors
{
    // SUPERSEDED, 2026-08-25 — do not re-attach this to anything.
    //
    // This behavior tried to fix the ScrollBar thumb-too-short bug by
    // setting Thumb.Height directly. Confirmed (by checking the actual
    // PART_Track type in Themes/CommonStyles.xaml's ScrollBar
    // ControlTemplate) that this had zero effect: stock WPF Track.
    // ArrangeOverride computes the Thumb's Arrange rect itself from
    // Minimum/Maximum/ViewportSize and calls Thumb.Arrange() with that
    // rect directly — it never reads Height/MinHeight/Width/MinWidth off
    // the Thumb when deciding what to Arrange it to. Debug.WriteLine
    // output confirmed this behavior WAS computing correct values and
    // WAS setting Thumb.Height successfully — the value just never
    // mattered, because Track's own Arrange call (which runs on every
    // layout pass, unconditionally) overwrote it every time regardless.
    //
    // The real fix: Behaviors/MinLengthTrack.cs, a Track subclass that
    // calls Thumb.Arrange() itself, AFTER base Track layout — which
    // genuinely is authoritative, since the last Arrange call wins. It
    // was written a day before this file, already correct, and simply
    // was never wired in as the ScrollBar template's actual PART_Track
    // type until this fix. See CommonStyles.xaml for where that happens
    // now (behaviors:MinLengthTrack, not the stock Track).
    //
    // This class is left in the repo only because this AI session has no
    // file-delete capability — it should be deleted outright next time
    // someone's actually in Visual Studio. There is no code in this file
    // anymore; the original attached-property implementation has been
    // removed so nothing can accidentally reference or re-attach it.
}

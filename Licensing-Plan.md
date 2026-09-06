# Licensing Plan — hardware-locked, expiring, obfuscated

**Status: planning only, no code written yet.**

**Goal:** prevent RovaShop POS from being copied off one pharma client's
machine and reused elsewhere for free. Every install must be tied to a
specific machine and a specific paid time window, verifiable **fully
offline** (the app must keep working with no internet on old client
hardware), and hard to strip out even by someone who decompiles the app.

**Scope note:** this only applies to the code Baraa and Mahmoud wrote on
top of the original fork (UI, pharma features, theming, licensing itself).
The inherited `PosSystem.Core` base from `mohamedelareeg/WPF-POS` is
MIT-licensed and redistribution of *that* layer can't be restricted — keep
the licensing boundary around the added layer, not the MIT base.

---

## Approach selected

Combination of:
1. **Hardware-locked license keys** (offline validation)
2. **Time-based expiry**, admin-selectable duration per key
3. **Code obfuscation** (ConfuserEx) as a post-build step

Not doing (for now): online activation server. Everything must validate
locally since client machines may have no reliable internet.

---

## Fingerprint design

| Component | Role | Match rule |
|---|---|---|
| CPU ID | Hard anchor | Must match exactly |
| Motherboard UUID | Hard anchor | Must match exactly |
| Disk serial | Soft signal | Allowed to change (e.g. disk upgrade) |
| MAC address | Soft signal | Allowed to change (e.g. NIC swap) |

**Validation rule:** license is valid only if CPU ID **and** motherboard
UUID both match what's in the signed license. Disk serial and MAC are
collected and stored for support/debugging but never block validation.

**Known risk:** cheap OEM boards sometimes report a garbage or
non-unique motherboard UUID (zeros, or duplicated across a batch from the
same manufacturer run). The activation screen must display the raw
fingerprint values so Baraa can sanity-check before issuing a key. If this
turns out to be a real problem on client hardware, fallback hard anchor is
CPU ID + a hash of BIOS serial + disk count/model, instead of trusting
motherboard UUID blindly.

---

## License key format

**Status: implemented** — `Core.Licensing/Signing/LicenseData.cs`,
`RsaKeyPairGenerator.cs`, `LicenseSigner.cs`, `LicenseVerifier.cs`.

- A signed blob containing: machine fingerprint (CPU ID + motherboard
  UUID, hashed), issue date, expiry date, license tier/notes.
- Signed with **RSA (3072-bit), not Ed25519** — decided during
  implementation. Ed25519 has no native .NET Framework support and would
  need a NuGet package, which can't be restored from Baraa's Linux
  environment (same blocker hit with ClosedXML). RSA
  (`RSACryptoServiceProvider`) is built into the framework, zero
  dependencies, and verification cost is negligible even on old client
  hardware — signing only happens on Baraa/Mahmoud's own machine.
- Uses a **private key Baraa holds and never ships**.
- The POS app only ever embeds the **public** key, used to verify the
  signature. Even a fully decompiled app can't forge a valid key without
  the private key.
- Stored as a small file (`license.dat`) or a row in the SQLite DB,
  encrypted at rest either way.

**Status: implemented** — `Core.Licensing/Storage/LicenseStorage.cs`.
Stored as `Documents\PosSystem\license.dat` (same folder the database
lives in, for the same reasons: visible, survives updates/uninstalls).
Encrypted at rest with **Windows DPAPI, machine scope**
(`DataProtectionScope.LocalMachine`) — chosen as a second, independent
binding layer on top of the RSA signature: a `license.dat` copied off the
machine can't be decrypted at all on another machine, regardless of
whether its CPU/motherboard happen to match. `Core.Licensing`
deliberately does not reference `PosSystem.Core`, so the folder name
constant is duplicated rather than shared — keep both in sync if the DB
location ever moves again.

---

## Validation logic (in the POS app)

**Status: implemented** — `Core.Licensing/Validation/LicenseValidator.cs`
(+ `LicenseStatus.cs`), wired into `App.xaml.cs.OnStartup` and
`Views/ActivationWindow.xaml(.cs)`.

On startup:
1. Read the stored license blob.
2. Verify the signature against the embedded public key. Reject if invalid
   or missing.
3. Recompute the current machine's CPU ID + motherboard UUID and compare
   against the license. Reject if either hard anchor doesn't match.
4. Check expiry date against system time.
5. **Clock-rollback guard:** store a "last seen date" on disk every
   session. If current system time is earlier than last-seen, treat as
   tampered and reject — this stops someone from just winding the clock
   back to dodge expiry.
6. On any failure: fail closed, but show a clear, friendly screen
   ("License invalid or expired — contact [vendor]") rather than crashing
   or silently degrading. Never leak why validation failed beyond that
   (don't hand a would-be cracker a debug map).

**Decision made during implementation:** hard-lock immediately on
expiry — no grace period. `LicenseValidator` rejects the moment
`DateTime.UtcNow` passes `ExpiresUtc`, showing the same generic
"invalid or expired" message as every other failure case. Simpler to
reason about and matches the plan's anti-info-leak principle (a grace
period with its own countdown/banner would need distinct UI and would
telegraph more about internal state to someone probing the app).

---

## Admin key-generation tool

**Status: implemented** — `LicenseAdminTool` project (console app),
references `Core.Licensing`. Not shipped to clients — lives in the
private repo only, run locally by Baraa/Mahmoud.

A real 3072-bit RSA keypair was generated 2026-09-04:
- **Private key**: `PrivateSigningKey.xml` at the repo root, plain and
  uncompiled. Decision history on this file, for the record: committed
  initially ("repo is private anyway") → reconsidered and gitignored
  → reconsidered again and committed once more, explicitly so Mahmoud
  can use `LicenseAdminTool` too, given the repo will stay private through
  release. **Current state: committed, not gitignored.**
  The underlying risk hasn't changed and is worth restating plainly: git
  history is permanent, so a single accidental repo leak (public flip,
  clone ending up somewhere it shouldn't, a compromised collaborator
  account) exposes this key forever, and possession of it means being
  able to forge a valid license for any machine, silently, with no way
  for Baraa to know it happened. The mitigation isn't prevention at that
  point, it's response: if it's ever exposed, generate a new keypair,
  update `LicensePublicKey.Xml` below, and reissue every outstanding
  client license — old licenses signed with the old key stop validating
  the moment the public key is swapped in a new app build.
- **Public key**: embedded as `Core.Licensing/Signing/LicensePublicKey.cs`
  (a plain constant) — ships inside `PosSystem.App`, safe to expose even
  in a decompiled build.

Tool is interactive (`LicenseAdminTool/Program.cs`):

Inputs:
- Client's machine fingerprint (CPU ID + motherboard UUID) — client's copy
  of the POS app shows this on an "activation" screen so it can be sent to
  Baraa/Mahmoud (email/WhatsApp/etc.), no server round-trip needed.
- **License duration — selectable, not fixed.** Admin picks the length
  (e.g. 6 months, 1 year, 2 years, custom number of days) at key-generation
  time. Tool computes the expiry date from "now + duration" and signs it
  in.
- Optional: tier/notes field for internal tracking.

Output: a signed license blob file to hand to the client (email, USB,
whatever's convenient — offline distribution is fine and expected).

**Open decision:** console app first (fastest to build, fine for
Baraa/Mahmoud's own use) vs. small WPF GUI. Leaning console for v1, GUI
later if key generation becomes frequent enough to be annoying.

---

## Obfuscation

**Status: implemented (build wiring), not yet actually run** — needs the
ConfuserEx CLI installed on a real Windows machine to verify (see below).

- **`ConfuserEx`** (free, works with old-style `.csproj`) run as a
  post-build step on Release builds only — `Core.Licensing.csproj`'s
  `ObfuscateReleaseBuild` target, `AfterTargets="Build"`,
  `Condition="'$(Configuration)' == 'Release'"`. Debug builds are
  untouched.
- **Scoped to `Core.Licensing.dll` only**, not `PosSystem.App.exe` —
  matches the original plan below, and deliberately avoids the much
  higher risk of obfuscating a WPF executable (x:Class/XAML type
  resolution, `InitializeComponent`, resource lookups can all break in
  ways that need a real WPF runtime to catch, which wasn't available
  while writing this). If `PosSystem.App.exe` obfuscation is wanted
  later, it needs its own scoped rule set and real on-machine testing,
  not a blind copy-paste of this config.
- **Opt-in, not required to build**: if the CLI tool isn't found at
  `$(SolutionDir)tools\ConfuserEx\Confuser.CLI.exe`, the target logs a
  message and skips — Release builds still succeed with the plain
  (unobfuscated) DLL, including on GitHub Actions CI today, which has no
  reason to have the tool installed yet.
- **One-time setup** (on whichever machine should actually produce
  obfuscated builds): download the ConfuserEx CLI —
  `mkaring/ConfuserEx` on GitHub (the original `yck1509/ConfuserEx` is
  archived) — and place `Confuser.CLI.exe` at
  `\tools\ConfuserEx\Confuser.CLI.exe` at the repo root. Never commit the
  tool itself; `.gitignore` already excludes `/tools/ConfuserEx/`.
- **Important manual step, not yet automated**: the target writes the
  obfuscated DLL to `Core.Licensing\bin\Release\Obfuscated\` — it does
  **not** automatically overwrite the plain copy that MSBuild copies into
  `PosSystem.App\bin\Release\` via the project reference. Before
  packaging a build for a client, the obfuscated
  `Core.Licensing\bin\Release\Obfuscated\Core.Licensing.dll` needs to be
  copied over the plain one sitting in `PosSystem.App\bin\Release\`.
  Automating that swap as its own build step is a reasonable fast-follow
  once the manual flow is confirmed working, rather than something to
  get right blind right now.

Original plan (still accurate as the underlying reasoning):
- **ConfuserEx** (free, works with old-style `.csproj`) run as a
  post-build step on Release builds only — keep Debug builds clean for
  normal development.
- Focus obfuscation on the licensing assembly/methods specifically —
  that's the part someone would target to patch out.
- Obfuscation raises the bar, it does **not** make cracking impossible.
  It's a layer on top of A–C above, not a substitute for them — a cracked
  binary should still need a validly signed key for a specific machine to
  actually run.

---

## Open questions / decisions still pending

- [x] RSA vs Ed25519 for signing — **decided: RSA 3072-bit**, no NuGet
      dependency needed (see License key format section above).
- [x] Hard-lock vs grace period on expiry — **decided: hard-lock**, no
      grace period (see "Validation logic" section above).
- [x] Console vs GUI for the admin key-gen tool — **decided: console**
      (`LicenseAdminTool`), fast to build and sufficient for occasional
      use by Baraa/Mahmoud.
- [x] Where the licensing code lives — **decided: new `Core.Licensing`
      project**, added to `PosSystem.sln`, keeps it cleanly separable and
      easier to obfuscate/exclude later.
- [ ] Exact WMI/P-Invoke calls to read CPU ID and motherboard UUID
      reliably on old, low-spec Windows machines — needs testing on
      representative hardware, not just Baraa's dev machine.

---

## Suggested build order

1. Fingerprint collection (CPU ID, motherboard UUID, disk serial, MAC) +
   an activation screen that displays raw values.
2. Signing/verification core (keypair generation, sign, verify) as a
   small standalone piece, tested independently before wiring into the app.
3. License file format + encrypted storage.
4. Admin key-generation tool (console, with duration selectable per key).
5. Startup validation flow in the POS app (including clock-rollback guard
   and the friendly failure screen).
6. Wire ConfuserEx into the Release build pipeline, scoped to the
   licensing assembly first.
7. End-to-end test: generate a key for a real fingerprint, confirm
   validation passes; tamper with system clock, confirm rejection; change
   disk serial only, confirm it still passes; change CPU/motherboard,
   confirm it's rejected.

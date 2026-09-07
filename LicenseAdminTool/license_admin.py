#!/usr/bin/env python3
"""
RovaShop POS -- License Admin Tool (cross-platform)

Admin-only tool for issuing signed RovaShop POS licenses. Works identically
on Linux and Windows -- no Visual Studio, no .NET Framework, just Python 3
plus the `cryptography` package. NEVER shipped to a client; run only on
Baraa's/Mahmoud's own machine. See Licensing-Plan.md, Phase 4.

This is a drop-in replacement for the C# LicenseAdminTool console app --
it produces byte-for-byte compatible license blobs (see license_core.py
for the shared wire-format implementation, tested against the repo's real
keypair), so a license generated here activates correctly in the WPF
client, and vice versa. Use whichever is more convenient on the machine
you're on; both read the same PrivateSigningKey.xml at the repo root.

Setup (one time):
    pip install cryptography

Usage:
    python3 license_admin.py generate
        Interactive flow: paste the client's Hard Anchor Hash, pick a
        duration, get a signed .lic file + blob printed to the console.

    python3 license_admin.py verify <path-to-.lic-or-blob-text>
        Sanity-checks a license blob locally (signature + expiry) without
        needing to run the actual WPF app -- handy for confirming a
        license looks right before sending it to a client.
"""

import argparse
import os
import re
import sys
from datetime import datetime, timedelta, timezone

try:
    from license_core import load_private_key, load_public_key, public_key_from_string, sign, verify
except ImportError:
    print("Could not import license_core.py -- make sure it's in the same folder as this script.")
    sys.exit(1)

# Must always match Core.Licensing/Signing/LicensePublicKey.cs exactly.
# Used by `verify` so you can sanity-check a license without needing the
# private key at all -- this constant is safe to keep here since it's the
# PUBLIC half only.
EMBEDDED_PUBLIC_KEY_XML = (
    "<RSAKeyValue><Modulus>6HNF+rt9gQWR14Ki17QL6zkeI91Qt4JV1rQNuOx2g56QQNyNZyToh7i7+RpG44e3q"
    "zT69QTcSZwItiILO4CnqKxh5kubmIhPM2Ss5krkrAX+ErgcmotWdC5Dkvtrx9JfbPKvF98Po5YP30wnjZqODHxo"
    "W4vGZRInlDc7UnxtuHiX/7FCQBHiyYZAWzjMM90gG4w87Bff+ujasRqeDOrVvfuGTkjdmGaXYqyW09jV/dcfmXu"
    "8TS+o1Vcr55bh1+zK+HP9hhRLjpZJFz+0pfKsSqiz4ZQK5SWrOkJvYGQ1prhWN85RyMfvVoXYiyNKU24jpZl8Sxz"
    "t2fgGa4abL7qic9lq2rdvYZkc1BcoMyofOwmwhjmS4qT0B/BT/aOuuw0Ka+U4n9qzrph9kQCh+8OrcAm36/W978"
    "zaAr0ovIBNizu/3++hI+9dxyDyvUUA9CGeEx2tCfNBoIwlf5ZXC2SxPoGZP+zLuR/sKFrDZJVRtDOTaNGfz/Glf/"
    "d3SS2aRpg3</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>"
)


def find_private_key_file(start_dir):
    """Walks up from start_dir looking for PrivateSigningKey.xml, which
    lives at the repo root -- same search strategy as the C# tool's
    FindPrivateKeyFile(), so behavior is consistent between the two."""
    current = os.path.abspath(start_dir)
    for _ in range(8):
        candidate = os.path.join(current, "PrivateSigningKey.xml")
        if os.path.isfile(candidate):
            return candidate
        parent = os.path.dirname(current)
        if parent == current:
            break
        current = parent
    return None


def sanitize_for_filename(text):
    return re.sub(r"[^A-Za-z0-9_-]+", "_", text).strip("_") or "license"


def cmd_generate(args):
    key_path = args.key or find_private_key_file(os.path.dirname(os.path.abspath(__file__)))
    if not key_path:
        key_path = input("Couldn't auto-locate PrivateSigningKey.xml. Enter its full path: ").strip()

    if not key_path or not os.path.isfile(key_path):
        print("Private key file not found: %s" % key_path)
        sys.exit(1)

    try:
        private_key = load_private_key(key_path)
    except ValueError as ex:
        print("Error loading private key: %s" % ex)
        sys.exit(1)

    print("Using private key: %s\n" % key_path)

    hard_anchor_hash = input("Hard Anchor Hash (from the client's activation screen): ").strip()
    if not hard_anchor_hash:
        print("A Hard Anchor Hash is required.")
        sys.exit(1)

    print("\nLicense duration:")
    print("  1) 6 months (182 days)")
    print("  2) 1 year   (365 days)")
    print("  3) 2 years  (730 days)")
    print("  4) Custom (enter number of days)")
    choice = input("Choice [1-4]: ").strip()

    days_map = {"1": 182, "2": 365, "3": 730}
    if choice in days_map:
        duration_days = days_map[choice]
    elif choice == "4":
        try:
            duration_days = int(input("Number of days: ").strip())
            if duration_days <= 0:
                raise ValueError
        except ValueError:
            print("Invalid input, defaulting to 365 days.")
            duration_days = 365
    else:
        print("Unrecognized choice, defaulting to 1 year.")
        duration_days = 365

    note = input("\nNote for your own records (e.g. client name) [optional]: ").strip()

    issued_utc = datetime.now(timezone.utc).replace(tzinfo=None)
    expires_utc = issued_utc + timedelta(days=duration_days)

    blob = sign(private_key, hard_anchor_hash, issued_utc, expires_utc, note)

    print("\n=== License generated ===")
    print("Issued (UTC):  %s" % issued_utc)
    print("Expires (UTC): %s  (%d days)" % (expires_utc, duration_days))
    print("Note:          %s" % (note or "(none)"))
    print("\nLicense blob (send this to the client / paste into the activation screen):\n")
    print(blob)

    filename = "%s_%s.lic" % (sanitize_for_filename(note), issued_utc.strftime("%Y%m%d_%H%M%S"))
    out_path = os.path.join(os.getcwd(), filename)
    try:
        with open(out_path, "w") as f:
            f.write(blob)
        print("\nAlso saved to: %s" % out_path)
    except OSError as ex:
        print("\n(Couldn't save to file: %s -- the blob above is still valid, copy it by hand.)" % ex)


def _looks_like_private(path):
    with open(path, "r") as f:
        return "<D>" in f.read()


def cmd_verify(args):
    if os.path.isfile(args.blob_or_path):
        with open(args.blob_or_path, "r") as f:
            blob = f.read().strip()
    else:
        blob = args.blob_or_path.strip()

    if args.key:
        public_key = load_private_key(args.key).public_key() if _looks_like_private(args.key) else load_public_key(args.key)
    else:
        public_key = public_key_from_string(EMBEDDED_PUBLIC_KEY_XML)

    ok, data, err = verify(public_key, blob)

    if not ok:
        print("INVALID: %s" % err)
        sys.exit(1)

    now = datetime.now(timezone.utc).replace(tzinfo=None)
    expired = now > data["expires_utc"]

    print("Signature: VALID")
    print("Hard Anchor Hash: %s" % data["hard_anchor_hash"])
    print("Issued (UTC):     %s" % data["issued_utc"])
    print("Expires (UTC):    %s  %s" % (data["expires_utc"], "*** EXPIRED ***" if expired else "(active)"))
    print("Note:             %s" % (data["note"] or "(none)"))
    print()
    print("Note: this only checks the signature and dates -- it can't check")
    print("hardware match, since that requires running on the actual client")
    print("machine (see Core.Licensing.Validation.LicenseValidator in the app).")


def main():
    parser = argparse.ArgumentParser(description="RovaShop POS License Admin Tool")
    subparsers = parser.add_subparsers(dest="command", required=True)

    gen_parser = subparsers.add_parser("generate", help="Interactively issue a new signed license")
    gen_parser.add_argument("--key", help="Path to PrivateSigningKey.xml (auto-detected if omitted)")
    gen_parser.set_defaults(func=cmd_generate)

    verify_parser = subparsers.add_parser("verify", help="Check a license blob's signature and expiry locally")
    verify_parser.add_argument("blob_or_path", help="A .lic file path, or the blob text itself")
    verify_parser.add_argument("--key", help="Path to a public (or private) key XML; defaults to the embedded public key")
    verify_parser.set_defaults(func=cmd_verify)

    args = parser.parse_args()
    args.func(args)


if __name__ == "__main__":
    main()

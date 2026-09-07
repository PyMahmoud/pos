"""
Core signing/verification logic for RovaShop POS licenses.

Deliberately implements the EXACT SAME wire format as the C# side
(Core.Licensing/Signing/LicenseData.cs, LicenseSigner.cs, LicenseVerifier.cs)
so a license generated here validates correctly inside the WPF client, and
vice versa. If either side's canonical string format or signature scheme
ever changes, THIS FILE MUST CHANGE TO MATCH, or licenses silently stop
being compatible between the two.

Wire format recap:
  canonical string = "POSLIC1|{hard_anchor_hash}|{issued_ticks}|{expires_ticks}|{note_b64}"
  blob             = base64(utf8(canonical)) + "." + base64(signature)
  signature        = RSA PKCS#1 v1.5 over SHA-256 of the canonical UTF-8 bytes
  ticks            = .NET DateTime.Ticks (100ns units since 0001-01-01 UTC)

Verified against the repo's real keypair during development: real private
key parsed, signed, verified with the matching public key, tamper-detection
confirmed to reject a corrupted blob, and the ticks conversion round-trips
to within microsecond precision. See Licensing-Plan.md for the full history.
"""

import base64
import xml.etree.ElementTree as ET
from datetime import datetime, timedelta, timezone

from cryptography.exceptions import InvalidSignature
from cryptography.hazmat.backends import default_backend
from cryptography.hazmat.primitives import hashes
from cryptography.hazmat.primitives.asymmetric import padding, rsa

FORMAT_TAG = "POSLIC1"

# Seconds between 0001-01-01T00:00:00 and 1970-01-01T00:00:00 (the .NET
# DateTime epoch vs the Unix epoch) — this constant is what lets us convert
# to/from .NET's Ticks representation without needing .NET itself.
_DOTNET_EPOCH_OFFSET_SECONDS = 62135596800
_TICKS_PER_SECOND = 10_000_000


def dotnet_ticks(dt):
    """Convert a UTC datetime to .NET DateTime.Ticks (matches LicenseData.cs)."""
    if dt.tzinfo is not None:
        dt = dt.astimezone(timezone.utc).replace(tzinfo=None)
    unix_seconds = (dt - datetime(1970, 1, 1)).total_seconds()
    return int(round((unix_seconds + _DOTNET_EPOCH_OFFSET_SECONDS) * _TICKS_PER_SECOND))


def from_dotnet_ticks(ticks):
    """Inverse of dotnet_ticks() — returns a naive UTC datetime."""
    unix_seconds = ticks / _TICKS_PER_SECOND - _DOTNET_EPOCH_OFFSET_SECONDS
    return datetime(1970, 1, 1) + timedelta(seconds=unix_seconds)


def _b64_field(el):
    return base64.b64decode(el.text) if el is not None and el.text else None


def _int_from_bytes(b):
    return int.from_bytes(b, "big")


def load_rsa_xml_fields(xml_path):
    """Parses a .NET RSACryptoServiceProvider.ToXmlString() file (public or
    private) and returns the raw byte fields as a dict. Works for either
    variant — private-only fields will just be None if this is a public key."""
    root = ET.parse(xml_path).getroot()
    fields = {}
    for tag in ("Modulus", "Exponent", "D", "P", "Q", "DP", "DQ", "InverseQ"):
        fields[tag] = _b64_field(root.find(tag))
    return fields


def load_private_key(xml_path):
    f = load_rsa_xml_fields(xml_path)
    if f["D"] is None:
        raise ValueError(
            "This XML has no <D> element — it looks like a PUBLIC key, "
            "not the private signing key. Point this at PrivateSigningKey.xml."
        )
    n = _int_from_bytes(f["Modulus"])
    e = _int_from_bytes(f["Exponent"])
    d = _int_from_bytes(f["D"])
    p = _int_from_bytes(f["P"])
    q = _int_from_bytes(f["Q"])
    dmp1 = _int_from_bytes(f["DP"])
    dmq1 = _int_from_bytes(f["DQ"])
    iqmp = _int_from_bytes(f["InverseQ"])
    public_numbers = rsa.RSAPublicNumbers(e, n)
    private_numbers = rsa.RSAPrivateNumbers(p, q, d, dmp1, dmq1, iqmp, public_numbers)
    return private_numbers.private_key(default_backend())


def load_public_key(xml_path):
    f = load_rsa_xml_fields(xml_path)
    n = _int_from_bytes(f["Modulus"])
    e = _int_from_bytes(f["Exponent"])
    return rsa.RSAPublicNumbers(e, n).public_key(default_backend())


def public_key_from_string(xml_string):
    root = ET.fromstring(xml_string)
    n = _int_from_bytes(base64.b64decode(root.find("Modulus").text))
    e = _int_from_bytes(base64.b64decode(root.find("Exponent").text))
    return rsa.RSAPublicNumbers(e, n).public_key(default_backend())


def to_canonical_string(hard_anchor_hash, issued_utc, expires_utc, note):
    hash_norm = (hard_anchor_hash or "").strip().lower()
    note_b64 = base64.b64encode((note or "").encode("utf-8")).decode("ascii")
    return "|".join([
        FORMAT_TAG,
        hash_norm,
        str(dotnet_ticks(issued_utc)),
        str(dotnet_ticks(expires_utc)),
        note_b64,
    ])


def parse_canonical_string(canonical):
    parts = canonical.split("|")
    if len(parts) != 5 or parts[0] != FORMAT_TAG:
        raise ValueError("Unrecognized license payload format.")
    return {
        "hard_anchor_hash": parts[1],
        "issued_utc": from_dotnet_ticks(int(parts[2])),
        "expires_utc": from_dotnet_ticks(int(parts[3])),
        "note": base64.b64decode(parts[4]).decode("utf-8"),
    }


def sign(private_key, hard_anchor_hash, issued_utc, expires_utc, note):
    canonical = to_canonical_string(hard_anchor_hash, issued_utc, expires_utc, note)
    canonical_bytes = canonical.encode("utf-8")
    signature = private_key.sign(canonical_bytes, padding.PKCS1v15(), hashes.SHA256())
    return base64.b64encode(canonical_bytes).decode("ascii") + "." + base64.b64encode(signature).decode("ascii")


def verify(public_key, blob):
    """Mirrors LicenseVerifier.Verify(). Returns (is_valid, data_or_none, error_or_none)."""
    parts = blob.split(".")
    if len(parts) != 2:
        return False, None, "License blob is malformed."

    try:
        canonical_bytes = base64.b64decode(parts[0])
        signature = base64.b64decode(parts[1])
    except Exception:
        return False, None, "License blob is malformed."

    try:
        public_key.verify(signature, canonical_bytes, padding.PKCS1v15(), hashes.SHA256())
    except InvalidSignature:
        return False, None, "Signature verification failed — forged or corrupted."

    try:
        data = parse_canonical_string(canonical_bytes.decode("utf-8"))
    except Exception as ex:
        return False, None, "License payload is malformed: %s" % ex

    return True, data, None

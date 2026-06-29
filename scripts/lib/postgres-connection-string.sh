#!/usr/bin/env bash
# Shared POSTGRES_CONNECTION_STRING validation for CD and local checks.

validate_postgres_connection_string() {
  python3 - "$1" <<'PY'
import re
import sys
import urllib.parse

conn = sys.argv[1].strip().strip('"').strip("'")

ALLOWED_URI_SSL = {"require", "verify-ca", "verify-full"}
NPGSQL_SSL_PATTERN = re.compile(r"SSL\s*Mode\s*=\s*(Require|VerifyCA|VerifyFull)\b", re.I)


def fail(message: str) -> None:
    print(f"POSTGRES_CONNECTION_STRING validation failed: {message}", file=sys.stderr)
    print(
        "Use Npgsql format (Host=...;Database=...;Username=...;Password=...; "
        "SSL Mode=Require|VerifyCA|VerifyFull) "
        "or a complete URI (postgresql://user:pass@host/db?sslmode=require|verify-ca|verify-full).",
        file=sys.stderr,
    )
    raise SystemExit(1)


def is_neon_host(host: str) -> bool:
    return host.endswith(".neon.tech")


def npgsql_ssl_mode_ok(raw: str) -> bool:
    return bool(NPGSQL_SSL_PATTERN.search(raw))


if re.search(r"\?sslmode(?:$|[&#])", conn, re.I):
    fail("URI ends with '?sslmode' without a value (truncated connection string).")

if re.search(r"(?:^|[?&])sslmode=(?:$|[&#])", conn, re.I):
    fail("URI has sslmode= with no value.")

if re.match(r"^postgres(ql)?://", conn, re.I):
    parsed = urllib.parse.urlparse(conn)
    if not parsed.hostname:
        fail("URI must include a hostname.")
    if not parsed.username or parsed.password is None:
        fail("URI must include username and password.")
    if not urllib.parse.unquote(parsed.path.lstrip("/")):
        fail("URI must include database name in the path.")

    query = urllib.parse.parse_qs(parsed.query, keep_blank_values=True)
    sslmode = (query.get("sslmode") or [""])[0].lower()
    if is_neon_host(parsed.hostname) and sslmode not in ALLOWED_URI_SSL:
        fail(
            "Neon requires ?sslmode=require, verify-ca, or verify-full in the URI."
        )
else:
    host_match = re.search(
        r"(?:^|;)\s*(?:Host|Server|Data Source)\s*=\s*([^;]*)",
        conn,
        re.I,
    )
    host = host_match.group(1).strip() if host_match else ""
    if host and is_neon_host(host) and not npgsql_ssl_mode_ok(conn):
        fail(
            "Neon requires SSL Mode=Require, VerifyCA, or VerifyFull "
            "in the Npgsql connection string."
        )
PY
}

postgres_exporter_sslmode_from_connection_string() {
  python3 - "$1" <<'PY'
import re
import sys
import urllib.parse

conn = sys.argv[1].strip().strip('"').strip("'")

ALLOWED_URI = {"require", "verify-ca", "verify-full"}
NPGSQL_TO_URI = {
    "require": "require",
    "verifyca": "verify-ca",
    "verifyfull": "verify-full",
}
NPGSQL_SSL_PATTERN = re.compile(r"SSL\s*Mode\s*=\s*(Require|VerifyCA|VerifyFull)\b", re.I)


def from_npgsql(raw: str) -> str:
    match = NPGSQL_SSL_PATTERN.search(raw)
    if not match:
        return "disable"
    key = match.group(1).replace(" ", "").lower()
    return NPGSQL_TO_URI.get(key, "disable")


def from_uri(raw: str) -> str:
    parsed = urllib.parse.urlparse(raw)
    query = urllib.parse.parse_qs(parsed.query, keep_blank_values=True)
    sslmode = ((query.get("sslmode") or ["require"])[0] or "require").lower()
    return sslmode if sslmode in ALLOWED_URI else "disable"


if re.match(r"^postgres(ql)?://", conn, re.I):
    print(from_uri(conn))
else:
    print(from_npgsql(conn))
PY
}

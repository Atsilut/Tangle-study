#!/usr/bin/env python3
import re
import sys
import urllib.parse

def fail(message: str) -> None:
    print(f"POSTGRES_CONNECTION_STRING validation failed: {message}", file=sys.stderr)
    sys.exit(1)

def get_key_value(key: str, raw: str) -> str:
    match = re.search(rf"(?:^|;)\s*{re.escape(key)}\s*=\s*([^;]*)", raw, re.I)
    return match.group(1).strip() if match else ""

def get_first(raw: str, keys: list[str]) -> str:
    for key in keys:
        val = get_key_value(key, raw)
        if val: return val
    return ""

def process_npgsql(raw: str) -> str:
    host = get_first(raw, ["Host", "Server", "Data Source"])
    database = get_first(raw, ["Database", "Initial Catalog"])
    username = get_first(raw, ["Username", "User ID", "User Id", "Uid", "User"])
    password = get_first(raw, ["Password", "Pwd"])
    port = get_first(raw, ["Port"]) or "5432"

    if not all([host, database, username, password]):
        fail("Missing required Npgsql fields (Host, Database, Username, Password).")

    ssl_match = re.search(r"SSL\s*Mode\s*=\s*(Require|VerifyCA|VerifyFull)\b", raw, re.I)
    sslmode = ssl_match.group(1).lower().replace(" ", "") if ssl_match else ""

    if host.endswith(".neon.tech") and sslmode not in ["require", "verifyca", "verifyfull"]:
        fail("Neon requires SSL Mode=Require, VerifyCA, or VerifyFull in the Npgsql string.")

    sslmode_map = {"require": "require", "verifyca": "verify-ca", "verifyfull": "verify-full"}
    mapped_sslmode = sslmode_map.get(sslmode, "disable")

    user = urllib.parse.quote(username, safe="")
    pw = urllib.parse.quote(password, safe="")
    return f"postgresql://{user}:{pw}@{host}:{port}/{database}?sslmode={mapped_sslmode}"

def process_uri(raw: str) -> str:
    if re.search(r"\?sslmode(?:$|[&#])", raw, re.I) or re.search(r"(?:^|[?&])sslmode=(?:$|[&#])", raw, re.I):
        fail("URI has sslmode= with no value (truncated connection string).")

    parsed = urllib.parse.urlparse(raw)
    if not parsed.hostname: fail("URI must include a hostname.")
    if not parsed.username or parsed.password is None: fail("URI must include username and password.")
    database = urllib.parse.unquote(parsed.path.lstrip("/"))
    if not database: fail("URI must include database name in the path.")

    query = urllib.parse.parse_qs(parsed.query, keep_blank_values=True)
    sslmode = (query.get("sslmode") or [""])[0].lower()

    if parsed.hostname.endswith(".neon.tech") and sslmode not in ["require", "verify-ca", "verify-full"]:
        fail("Neon requires ?sslmode=require, verify-ca, or verify-full in the URI.")

    if not sslmode: sslmode = "disable"
    port = parsed.port or 5432
    user = urllib.parse.quote(urllib.parse.unquote(parsed.username), safe="")
    pw = urllib.parse.quote(urllib.parse.unquote(parsed.password), safe="")
    return f"postgresql://{user}:{pw}@{parsed.hostname}:{port}/{database}?sslmode={sslmode}"

def main():
    raw = sys.stdin.read().strip().strip('"').strip("'")
    if not raw and len(sys.argv) > 1:
        raw = sys.argv[1].strip().strip('"').strip("'")

    if not raw:
        fail("Connection string is empty.")

    if re.match(r"^postgres(ql)?://", raw, re.I):
        print(process_uri(raw))
    else:
        print(process_npgsql(raw))

if __name__ == "__main__":
    main()
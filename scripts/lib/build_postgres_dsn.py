#!/usr/bin/env python3
import re
import sys
import urllib.parse


def get_key_value(key: str, raw: str) -> str:
    match = re.search(rf"(?:^|;)\s*{re.escape(key)}\s*=\s*([^;]*)", raw, re.I)
    return match.group(1).strip() if match else ""


def get_first(raw: str, keys: list[str]) -> str:
    for key in keys:
        value = get_key_value(key, raw)
        if value:
            return value
    return ""


def build_from_npgsql(raw: str) -> str:
    host = get_first(raw, ["Host", "Server", "Data Source"])
    database = get_first(raw, ["Database", "Initial Catalog"])
    username = get_first(raw, ["Username", "User ID", "User Id", "Uid", "User"])
    password = get_first(raw, ["Password", "Pwd"])
    port = get_first(raw, ["Port"]) or "5432"

    ssl_match = re.search(r"SSL\s*Mode\s*=\s*(Require|VerifyCA|VerifyFull)\b", raw, re.I)
    sslmode = (
        {
            "require": "require",
            "verifyca": "verify-ca",
            "verifyfull": "verify-full",
        }.get((ssl_match.group(1).lower() if ssl_match else ""), "require")
    )

    if not all([host, database, username, password]):
        missing = [
            name for name, value in [
                ("Host", host),
                ("Database", database),
                ("Username", username),
                ("Password", password),
            ] if not value
        ]
        raise SystemExit(f"Missing fields: {', '.join(missing)}")

    user = urllib.parse.quote(username, safe="")
    pw = urllib.parse.quote(password, safe="")

    return f"postgresql://{user}:{pw}@{host}:{port}/{database}?sslmode={sslmode}"


def build_from_uri(raw: str) -> str:
    parsed = urllib.parse.urlparse(raw)

    if parsed.scheme not in ("postgres", "postgresql"):
        raise SystemExit(f"Unsupported scheme: {parsed.scheme}")

    if not parsed.hostname:
        raise SystemExit("Missing hostname")

    if not parsed.username or parsed.password is None:
        raise SystemExit("Missing credentials")

    database = urllib.parse.unquote(parsed.path.lstrip("/"))
    if not database:
        raise SystemExit("Missing database name")

    query = urllib.parse.parse_qs(parsed.query)
    sslmode = (query.get("sslmode", ["require"])[0] or "require").lower()

    if sslmode not in {"require", "verify-ca", "verify-full"}:
        sslmode = "require"

    user = urllib.parse.quote(urllib.parse.unquote(parsed.username), safe="")
    pw = urllib.parse.quote(urllib.parse.unquote(parsed.password), safe="")
    port = parsed.port or 5432

    return f"postgresql://{user}:{pw}@{parsed.hostname}:{port}/{database}?sslmode={sslmode}"


def main():
    raw = sys.stdin.read().strip()

    if raw.startswith("postgres://") or raw.startswith("postgresql://"):
        print(build_from_uri(raw))
    else:
        print(build_from_npgsql(raw))


if __name__ == "__main__":
    main()
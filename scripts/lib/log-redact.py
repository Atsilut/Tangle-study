#!/usr/bin/env python3
"""Redact secrets and infrastructure endpoints from CD log output."""

from __future__ import annotations

import re
import sys


def redact(text: str) -> str:
    def sub(pattern: str, repl: str, flags: int = 0) -> None:
        nonlocal text
        text = re.sub(pattern, repl, text, flags=flags)

    sub(r"postgres(?:ql)?://[^\s\"'<>]+", "postgresql://[redacted]", re.I)
    sub(r"(?i)(Password|Pwd)\s*=\s*[^;\s\"'<>]+", r"\1=[redacted]")
    sub(r"(?i)(Username|User ID|User Id|Uid|User)\s*=\s*[^;\s\"'<>]+", r"\1=[redacted]")
    sub(r"(?i)(Host|Server|Data Source)\s*=\s*[^;\s\"'<>]+", r"\1=[redacted]")
    sub(r"(?i)Database\s*=\s*[^;\s\"'<>]+", "Database=[redacted]")
    sub(r"redis://[^\s\"'<>]+", "redis://[redacted]", re.I)
    sub(
        r"(?i)(Redis__ConnectionString|REDIS_URL|REDIS_ADDR|ConnectionStrings__DefaultConnection)\s*=\s*[^\s\"'<>]+",
        r"\1=[redacted]",
    )
    sub(
        r"[a-z0-9-]+\.internal\.[a-z0-9.-]+\.azurecontainerapps\.io(?::\d+)?",
        "[redis-internal-host]",
        re.I,
    )
    sub(
        r"(?i)[a-z0-9-]+\.internal\.[a-z0-9.-]+\.azurecontainerapps\.io:6379(?:,[^\s\"'<>]+)?",
        "[redis-connection-string]",
    )
    sub(
        r"(?i)\b[a-z0-9-]+:6379(?:,abortConnect=[^,\s\"'<>]+(?:,[^\s\"'<>]+)*)?",
        "[redis-connection-string]",
    )
    sub(r"\b[a-z0-9-]+\.neon\.tech\b", "[postgres-host]", re.I)
    return text


def main() -> int:
    sys.stdout.write(redact(sys.stdin.read()))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

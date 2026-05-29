//! Retry policy and re-enqueue helpers (implemented in a later milestone).

#![allow(dead_code)]

use std::time::Duration;

pub fn backoff_delay(attempt: u32) -> Duration {
    let base_ms = 1_000u64.saturating_mul(2u64.saturating_pow(attempt.saturating_sub(1)));
    let capped_ms = base_ms.min(60_000);
    Duration::from_millis(capped_ms)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn backoff_caps_at_sixty_seconds() {
        assert_eq!(backoff_delay(1), Duration::from_millis(1_000));
        assert_eq!(backoff_delay(10), Duration::from_millis(60_000));
    }
}

//! Backoff and retry eligibility for pending stream messages.

/// Minimum idle time (ms) before a pending message may be reclaimed for retry.
pub fn backoff_delay_ms(
    times_delivered: u32,
    base_ms: u64,
    max_ms: u64,
    jitter_pct: f64,
    message_id: &str,
) -> u64 {
    let attempt = times_delivered.max(1);
    let exponent = u32::from(attempt.saturating_sub(1));
    let base = base_ms.saturating_mul(2u64.saturating_pow(exponent));
    let capped = base.min(max_ms);
    apply_jitter(capped, message_id, jitter_pct)
}

pub fn is_terminal(times_delivered: u32, max_attempts: u32) -> bool {
    times_delivered >= max_attempts
}

pub fn eligible_for_retry(
    last_delivered_ms: u64,
    times_delivered: u32,
    max_attempts: u32,
    base_ms: u64,
    max_ms: u64,
    jitter_pct: f64,
    message_id: &str,
) -> bool {
    if is_terminal(times_delivered, max_attempts) {
        return false;
    }

    let required_idle = backoff_delay_ms(
        times_delivered,
        base_ms,
        max_ms,
        jitter_pct,
        message_id,
    );
    last_delivered_ms >= required_idle
}

fn apply_jitter(ms: u64, message_id: &str, jitter_pct: f64) -> u64 {
    if jitter_pct <= 0.0 {
        return ms;
    }

    let hash = message_id
        .bytes()
        .fold(0u64, |acc, b| acc.wrapping_mul(31).wrapping_add(u64::from(b)));
    let factor = (hash % 1000) as f64 / 1000.0;
    let jitter = (ms as f64 * jitter_pct * factor) as u64;
    ms.saturating_add(jitter)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn backoff_doubles_and_caps() {
        assert_eq!(
            backoff_delay_ms(1, 1_000, 60_000, 0.0, "1-0"),
            1_000
        );
        assert_eq!(
            backoff_delay_ms(2, 1_000, 60_000, 0.0, "1-0"),
            2_000
        );
        assert_eq!(
            backoff_delay_ms(10, 1_000, 60_000, 0.0, "1-0"),
            60_000
        );
    }

    #[test]
    fn jitter_is_deterministic_per_message_id() {
        let a = backoff_delay_ms(1, 1_000, 60_000, 0.2, "1-0");
        let b = backoff_delay_ms(1, 1_000, 60_000, 0.2, "1-0");
        assert_eq!(a, b);
        assert!(a >= 1_000);
        assert!(a <= 1_200);
    }

    #[test]
    fn terminal_when_delivery_count_reaches_max() {
        assert!(!is_terminal(4, 5));
        assert!(is_terminal(5, 5));
    }

    #[test]
    fn eligible_when_idle_exceeds_backoff() {
        assert!(eligible_for_retry(
            2_000,
            1,
            5,
            1_000,
            60_000,
            0.0,
            "1-0"
        ));
        assert!(!eligible_for_retry(
            500,
            1,
            5,
            1_000,
            60_000,
            0.0,
            "1-0"
        ));
    }
}

use std::fmt;

/// Marker for poison-pill stream entries that should be acked, not retried.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct MalformedJob;

impl fmt::Display for MalformedJob {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "malformed stream job")
    }
}

impl std::error::Error for MalformedJob {}

pub fn is_malformed(err: &anyhow::Error) -> bool {
    err.chain()
        .any(|cause| cause.downcast_ref::<MalformedJob>().is_some())
}

pub fn wrap_malformed(err: impl Into<anyhow::Error>) -> anyhow::Error {
    let inner = err.into();
    anyhow::Error::new(MalformedJob).context(inner)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn is_malformed_detects_marker_in_chain() {
        let err = wrap_malformed(anyhow::anyhow!("decode failed"));
        assert!(is_malformed(&err));
    }

    #[test]
    fn is_malformed_rejects_unmarked_errors() {
        let err = anyhow::anyhow!("handler failed");
        assert!(!is_malformed(&err));
    }

    #[test]
    fn wrap_malformed_attaches_marker() {
        let err = wrap_malformed(anyhow::anyhow!("bad payload"));
        assert!(is_malformed(&err));
    }
}

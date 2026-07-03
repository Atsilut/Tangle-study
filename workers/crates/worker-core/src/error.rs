use std::error::Error as StdError;
use std::fmt;

/// Marker for poison-pill stream entries that should be acked, not retried.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct MalformedJob;

impl fmt::Display for MalformedJob {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "malformed stream job")
    }
}

impl StdError for MalformedJob {}

#[derive(Debug)]
struct MalformedJobWithSource {
    source: anyhow::Error,
}

impl fmt::Display for MalformedJobWithSource {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        self.source.fmt(f)
    }
}

impl StdError for MalformedJobWithSource {
    fn source(&self) -> Option<&(dyn StdError + 'static)> {
        Some(self.source.as_ref())
    }
}

pub fn is_malformed(err: &anyhow::Error) -> bool {
    err.chain()
        .any(|cause| cause.downcast_ref::<MalformedJobWithSource>().is_some())
}

pub fn wrap_malformed(err: impl Into<anyhow::Error>) -> anyhow::Error {
    anyhow::Error::new(MalformedJobWithSource {
        source: err.into(),
    })
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

    #[test]
    fn wrap_malformed_preserves_inner_chain() {
        let err = wrap_malformed(anyhow::anyhow!("decode failed"));
        assert!(is_malformed(&err));
        assert!(
            err.chain()
                .any(|cause| cause.to_string().contains("decode failed"))
        );
    }
}

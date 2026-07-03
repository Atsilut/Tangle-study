use anyhow::{bail, Result};
use serde::{Deserialize, Serialize};

/// Matches `Api.Global.Queue.LocationClusterJob` (System.Text.Json web defaults).
#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
#[serde(rename_all = "camelCase")]
pub struct LocationClusterJob {
    pub min_latitude: f64,
    pub max_latitude: f64,
    pub min_longitude: f64,
    pub max_longitude: f64,
    pub zoom: i32,
}

impl LocationClusterJob {
    pub fn validate(&self) -> Result<()> {
        if self.min_latitude > self.max_latitude {
            bail!("min_latitude must be less than or equal to max_latitude");
        }
        if self.min_longitude > self.max_longitude {
            bail!("min_longitude must be less than or equal to max_longitude");
        }
        if !(2..=4).contains(&self.zoom) {
            bail!("zoom must be between 2 and 4");
        }
        Ok(())
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn validate_accepts_in_range_bounds_and_zoom() {
        LocationClusterJob {
            min_latitude: 37.0,
            max_latitude: 38.0,
            min_longitude: 126.0,
            max_longitude: 127.0,
            zoom: 3,
        }
        .validate()
        .unwrap();
    }

    #[test]
    fn validate_rejects_inverted_latitude() {
        let err = LocationClusterJob {
            min_latitude: 38.0,
            max_latitude: 37.0,
            min_longitude: 126.0,
            max_longitude: 127.0,
            zoom: 3,
        }
        .validate()
        .unwrap_err();
        assert!(err.to_string().contains("min_latitude"));
    }

    #[test]
    fn validate_rejects_inverted_longitude() {
        let err = LocationClusterJob {
            min_latitude: 37.0,
            max_latitude: 38.0,
            min_longitude: 127.0,
            max_longitude: 126.0,
            zoom: 3,
        }
        .validate()
        .unwrap_err();
        assert!(err.to_string().contains("min_longitude"));
    }

    #[test]
    fn validate_rejects_zoom_outside_map_range() {
        let err = LocationClusterJob {
            min_latitude: 37.0,
            max_latitude: 38.0,
            min_longitude: 126.0,
            max_longitude: 127.0,
            zoom: 10,
        }
        .validate()
        .unwrap_err();
        assert!(err.to_string().contains("zoom must be between 2 and 4"));
    }
}

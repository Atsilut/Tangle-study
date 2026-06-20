use std::collections::HashMap;

#[derive(Debug, Clone, PartialEq)]
pub struct PinPoint {
    pub id: i64,
    pub latitude: f64,
    pub longitude: f64,
}

#[derive(Debug, Clone, PartialEq)]
pub struct ClusterPoint {
    pub latitude: f64,
    pub longitude: f64,
    pub pin_count: usize,
    pub sample_pin_id: Option<i64>,
}

pub fn cluster_pins(pins: &[PinPoint], zoom: u32) -> Vec<ClusterPoint> {
    if pins.is_empty() {
        return Vec::new();
    }

    let cell_size = (360.0 / (256.0 * 2f64.powi(zoom as i32))).max(0.000_001);
    let mut cells: HashMap<(i32, i32), Vec<&PinPoint>> = HashMap::new();

    for pin in pins {
        let cell_lat = (pin.latitude / cell_size).floor() as i32;
        let cell_lng = (pin.longitude / cell_size).floor() as i32;
        cells.entry((cell_lat, cell_lng)).or_default().push(pin);
    }

    let mut clusters = Vec::with_capacity(cells.len());
    for group in cells.into_values() {
        let count = group.len();
        let latitude = group.iter().map(|pin| pin.latitude).sum::<f64>() / count as f64;
        let longitude = group.iter().map(|pin| pin.longitude).sum::<f64>() / count as f64;
        let sample_pin_id = group.iter().map(|pin| pin.id).min();
        clusters.push(ClusterPoint {
            latitude,
            longitude,
            pin_count: count,
            sample_pin_id,
        });
    }

    clusters.sort_by(|left, right| {
        left.latitude
            .partial_cmp(&right.latitude)
            .unwrap_or(std::cmp::Ordering::Equal)
            .then_with(|| {
                left.longitude
                    .partial_cmp(&right.longitude)
                    .unwrap_or(std::cmp::Ordering::Equal)
            })
    });

    clusters
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn clusters_nearby_pins_together() {
        let pins = vec![
            PinPoint {
                id: 1,
                latitude: 37.5665,
                longitude: 126.9780,
            },
            PinPoint {
                id: 2,
                latitude: 37.5666,
                longitude: 126.9781,
            },
        ];

        let clusters = cluster_pins(&pins, 12);
        assert_eq!(clusters.len(), 1);
        assert_eq!(clusters[0].pin_count, 2);
        assert_eq!(clusters[0].sample_pin_id, Some(1));
    }

    #[test]
    fn empty_pins_returns_empty_clusters() {
        assert!(cluster_pins(&[], 6).is_empty());
    }
}

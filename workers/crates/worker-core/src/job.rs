use anyhow::{bail, Result};
use serde::{Deserialize, Serialize};

/// Matches `Api.Global.Queue.ChatMessageCreatedJob` (System.Text.Json web defaults).
#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct ChatMessageCreatedJob {
    pub message_id: i64,
    pub chat_room_id: i64,
    pub sender_user_id: i64,
    pub body: String,
    pub sent_at: String,
}

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

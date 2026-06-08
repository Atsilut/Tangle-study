use std::path::Path;

use anyhow::{bail, Context, Result};
use serde::Deserialize;
use tokio::process::Command;

/// Minimum video bitrate used for feasibility checks at the most aggressive scale (360p).
pub const MIN_VIDEO_BITRATE_BPS: u64 = 300_000;

/// Default audio bitrate assumed when planning encodes and checking feasibility.
pub const DEFAULT_AUDIO_BITRATE_BPS: u64 = 128_000;

/// Default utilization ratio for pre-encode feasibility (second-attempt target).
pub const DEFAULT_FEASIBILITY_RATIO: f64 = 0.90;

#[derive(Debug, Clone, PartialEq)]
pub struct MediaProbe {
    pub duration_secs: f64,
    pub width: u32,
    pub height: u32,
    pub video_bit_rate: Option<u64>,
    pub audio_bit_rate: Option<u64>,
    pub format_size: Option<u64>,
    pub is_image: bool,
}

/// Runs ffprobe and parses the media metadata needed for encode planning.
pub async fn probe_media(input: &Path) -> Result<MediaProbe> {
    let input_str = input
        .to_str()
        .with_context(|| format!("input path is not valid utf-8: {}", input.display()))?;

    let output = Command::new("ffprobe")
        .args([
            "-v",
            "error",
            "-show_entries",
            "format=duration,size,bit_rate",
            "-show_entries",
            "stream=codec_type,width,height,bit_rate",
            "-of",
            "json",
            input_str,
        ])
        .output()
        .await
        .context("spawn ffprobe")?;

    if !output.status.success() {
        let stderr = String::from_utf8_lossy(&output.stderr);
        bail!("ffprobe exited with status {}: {stderr}", output.status);
    }

    parse_ffprobe_json(&output.stdout)
}

/// Returns an estimated output size in bytes from bitrates and duration.
pub fn estimate_output_bytes(total_bitrate_bps: u64, duration_secs: f64) -> u64 {
    ((total_bitrate_bps as f64 * duration_secs) / 8.0).ceil() as u64
}

/// Fails fast when even the most aggressive encode cannot fit within `target_max_bytes * min_ratio`.
pub fn ensure_feasible(probe: &MediaProbe, target_max_bytes: i64, min_ratio: f64) -> Result<()> {
    if probe.is_image {
        return Ok(());
    }

    if probe.duration_secs <= 0.0 {
        bail!("cannot determine media duration for feasibility check");
    }

    let min_total_bps = MIN_VIDEO_BITRATE_BPS + DEFAULT_AUDIO_BITRATE_BPS;
    let min_estimated_bytes =
        estimate_output_bytes(min_total_bps, probe.duration_secs);
    let target_budget = ((target_max_bytes as f64) * min_ratio).floor() as u64;

    if min_estimated_bytes > target_budget {
        bail!(
            "media duration {:.1}s cannot be compressed below {min_estimated_bytes} bytes at minimum quality (limit {target_max_bytes} at {:.0}% utilization)",
            probe.duration_secs,
            min_ratio * 100.0
        );
    }

    Ok(())
}

fn parse_ffprobe_json(bytes: &[u8]) -> Result<MediaProbe> {
    let raw: FfprobeOutput =
        serde_json::from_slice(bytes).context("parse ffprobe json output")?;

    let format = raw.format.unwrap_or_default();
    let streams = raw.streams.unwrap_or_default();

    let duration_secs = format
        .duration
        .as_deref()
        .and_then(parse_positive_f64)
        .unwrap_or(0.0);
    let format_size = format.size.as_deref().and_then(parse_positive_u64);
    let format_bit_rate = format.bit_rate.as_deref().and_then(parse_positive_u64);

    let mut video_width = 0_u32;
    let mut video_height = 0_u32;
    let mut video_bit_rate = None;
    let mut audio_bit_rate = None;
    let mut has_video = false;
    let mut has_audio = false;

    for stream in streams {
        match stream.codec_type.as_deref() {
            Some("video") => {
                has_video = true;
                if let Some(width) = stream.width.filter(|value| *value > 0) {
                    video_width = width;
                }
                if let Some(height) = stream.height.filter(|value| *value > 0) {
                    video_height = height;
                }
                if let Some(bit_rate) = stream.bit_rate.as_deref().and_then(parse_positive_u64) {
                    video_bit_rate = Some(bit_rate);
                }
            }
            Some("audio") => {
                has_audio = true;
                if let Some(bit_rate) = stream.bit_rate.as_deref().and_then(parse_positive_u64) {
                    audio_bit_rate = Some(bit_rate);
                }
            }
            _ => {}
        }
    }

    let is_image = has_video && !has_audio && duration_secs <= 0.0;

    if video_width == 0 || video_height == 0 {
        bail!("ffprobe did not report video dimensions");
    }

    Ok(MediaProbe {
        duration_secs,
        width: video_width,
        height: video_height,
        video_bit_rate: video_bit_rate.or(format_bit_rate),
        audio_bit_rate,
        format_size,
        is_image,
    })
}

fn parse_positive_f64(value: &str) -> Option<f64> {
    let parsed = value.trim().parse::<f64>().ok()?;
    (parsed > 0.0).then_some(parsed)
}

fn parse_positive_u64(value: &str) -> Option<u64> {
    let parsed = value.trim().parse::<u64>().ok()?;
    (parsed > 0).then_some(parsed)
}

#[derive(Debug, Default, Deserialize)]
struct FfprobeOutput {
    format: Option<FfprobeFormat>,
    streams: Option<Vec<FfprobeStream>>,
}

#[derive(Debug, Default, Deserialize)]
struct FfprobeFormat {
    duration: Option<String>,
    size: Option<String>,
    bit_rate: Option<String>,
}

#[derive(Debug, Deserialize)]
struct FfprobeStream {
    codec_type: Option<String>,
    width: Option<u32>,
    height: Option<u32>,
    bit_rate: Option<String>,
}

#[cfg(test)]
mod tests {
    use super::*;

    const VIDEO_JSON: &str = r#"{
        "format": {
            "duration": "120.5",
            "size": "500000000",
            "bit_rate": "33195020"
        },
        "streams": [
            {
                "codec_type": "video",
                "width": 1920,
                "height": 1080,
                "bit_rate": "32000000"
            },
            {
                "codec_type": "audio",
                "bit_rate": "128000"
            }
        ]
    }"#;

    const IMAGE_JSON: &str = r#"{
        "format": {
            "size": "4500000"
        },
        "streams": [
            {
                "codec_type": "video",
                "width": 4000,
                "height": 3000
            }
        ]
    }"#;

    #[test]
    fn parses_video_probe_json() {
        let probe = parse_ffprobe_json(VIDEO_JSON.as_bytes()).unwrap();
        assert!((probe.duration_secs - 120.5).abs() < f64::EPSILON);
        assert_eq!(probe.width, 1920);
        assert_eq!(probe.height, 1080);
        assert_eq!(probe.video_bit_rate, Some(32_000_000));
        assert_eq!(probe.audio_bit_rate, Some(128_000));
        assert_eq!(probe.format_size, Some(500_000_000));
        assert!(!probe.is_image);
    }

    #[test]
    fn parses_image_probe_json() {
        let probe = parse_ffprobe_json(IMAGE_JSON.as_bytes()).unwrap();
        assert_eq!(probe.duration_secs, 0.0);
        assert_eq!(probe.width, 4000);
        assert_eq!(probe.height, 3000);
        assert!(probe.is_image);
        assert_eq!(probe.format_size, Some(4_500_000));
    }

    #[test]
    fn estimate_output_bytes_rounds_up() {
        let bytes = estimate_output_bytes(428_000, 10.0);
        assert_eq!(bytes, 535_000);
    }

    #[test]
    fn ensure_feasible_accepts_short_video_within_limit() {
        let probe = MediaProbe {
            duration_secs: 120.0,
            width: 1920,
            height: 1080,
            video_bit_rate: None,
            audio_bit_rate: None,
            format_size: None,
            is_image: false,
        };

        ensure_feasible(&probe, 157_286_400, DEFAULT_FEASIBILITY_RATIO).unwrap();
    }

    #[test]
    fn ensure_feasible_rejects_impossibly_long_video() {
        let probe = MediaProbe {
            duration_secs: 86_400.0,
            width: 1920,
            height: 1080,
            video_bit_rate: None,
            audio_bit_rate: None,
            format_size: None,
            is_image: false,
        };

        let err = ensure_feasible(&probe, 157_286_400, DEFAULT_FEASIBILITY_RATIO)
            .unwrap_err()
            .to_string();
        assert!(err.contains("cannot be compressed below"));
    }

    #[test]
    fn ensure_feasible_skips_image_media() {
        let probe = MediaProbe {
            duration_secs: 0.0,
            width: 4000,
            height: 3000,
            video_bit_rate: None,
            audio_bit_rate: None,
            format_size: Some(4_500_000),
            is_image: true,
        };

        ensure_feasible(&probe, 1_024, DEFAULT_FEASIBILITY_RATIO).unwrap();
    }
}

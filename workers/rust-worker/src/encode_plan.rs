use std::path::Path;

use anyhow::{bail, Context, Result};

use crate::probe::{self, MediaProbe, DEFAULT_AUDIO_BITRATE_BPS, MIN_VIDEO_BITRATE_BPS};

pub const FIRST_TARGET_RATIO: f64 = 0.95;
pub const SECOND_TARGET_RATIO: f64 = 0.90;
pub const QUALITY_BUMP_TARGET_RATIO: f64 = 0.825;
pub const MIN_UTILIZATION_RATIO: f64 = 0.70;
pub const MAX_UTILIZATION_RATIO: f64 = 1.00;
pub const CONTAINER_OVERHEAD_RATIO: f64 = 0.98;

const SCALE_HEIGHT_HD: u32 = 720;
const SCALE_HEIGHT_SD: u32 = 480;
const SCALE_HEIGHT_LOW: u32 = 360;

const BITRATE_THRESHOLD_HD: u64 = 600_000;
const BITRATE_THRESHOLD_SD: u64 = 350_000;

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct EncodePlan {
    pub args: Vec<String>,
    pub planned_budget_bytes: u64,
}

pub fn target_bytes(limit: i64, ratio: f64) -> u64 {
    ((limit as f64) * ratio * CONTAINER_OVERHEAD_RATIO).floor() as u64
}

pub fn is_under_quality_floor(actual: u64, limit: i64) -> bool {
    (actual as f64) < (limit as f64) * MIN_UTILIZATION_RATIO
}

pub fn exceeds_hard_cap(actual: u64, limit: i64) -> bool {
    (actual as f64) > (limit as f64) * MAX_UTILIZATION_RATIO
}

pub fn in_acceptance_band(actual: u64, limit: i64) -> bool {
    !is_under_quality_floor(actual, limit) && !exceeds_hard_cap(actual, limit)
}

pub fn build_video_plan(
    probe: &MediaProbe,
    target_max_bytes: i64,
    target_ratio: f64,
    input: &Path,
    output: &Path,
) -> Result<EncodePlan> {
    if probe.duration_secs <= 0.0 {
        bail!("cannot build video encode plan without duration");
    }

    let budget_bytes = target_bytes(target_max_bytes, target_ratio);
    let total_bps = ((budget_bytes as f64) * 8.0 / probe.duration_secs).floor() as u64;
    let video_bps = total_bps
        .saturating_sub(DEFAULT_AUDIO_BITRATE_BPS)
        .max(MIN_VIDEO_BITRATE_BPS);
    let scale_height = choose_scale_height(video_bps);
    let max_rate = ((video_bps as f64) * 1.2).round() as u64;
    let buf_size = video_bps * 2;

    let input_str = path_to_str(input)?;
    let output_str = path_to_str(output)?;
    let planned_budget_bytes = probe::estimate_output_bytes(
        video_bps + DEFAULT_AUDIO_BITRATE_BPS,
        probe.duration_secs,
    );

    Ok(EncodePlan {
        args: vec![
            "-y".to_owned(),
            "-i".to_owned(),
            input_str.to_owned(),
            "-vf".to_owned(),
            format!("scale=-2:{scale_height}"),
            "-c:v".to_owned(),
            "libx264".to_owned(),
            "-preset".to_owned(),
            "veryfast".to_owned(),
            "-b:v".to_owned(),
            video_bps.to_string(),
            "-maxrate".to_owned(),
            max_rate.to_string(),
            "-bufsize".to_owned(),
            buf_size.to_string(),
            "-c:a".to_owned(),
            "aac".to_owned(),
            "-b:a".to_owned(),
            format!("{}k", DEFAULT_AUDIO_BITRATE_BPS / 1000),
            "-movflags".to_owned(),
            "+faststart".to_owned(),
            output_str.to_owned(),
        ],
        planned_budget_bytes,
    })
}

pub fn build_image_plan(
    probe: &MediaProbe,
    input_size: u64,
    target_max_bytes: i64,
    target_ratio: f64,
    input: &Path,
    output: &Path,
) -> Result<EncodePlan> {
    let budget_bytes = target_bytes(target_max_bytes, target_ratio);
    let input_str = path_to_str(input)?;
    let output_str = path_to_str(output)?;

    let mut args = vec![
        "-y".to_owned(),
        "-i".to_owned(),
        input_str.to_owned(),
    ];

    if let Some(scale_filter) = image_scale_filter(probe, input_size, budget_bytes) {
        args.push("-vf".to_owned());
        args.push(scale_filter);
    }

    args.push("-q:v".to_owned());
    args.push(image_quality_value(input_size, budget_bytes).to_string());
    args.push(output_str.to_owned());

    Ok(EncodePlan {
        args,
        planned_budget_bytes: budget_bytes,
    })
}

pub fn build_quality_bump_video_plan(
    probe: &MediaProbe,
    target_max_bytes: i64,
    actual_bytes: u64,
    input: &Path,
    output: &Path,
) -> Result<EncodePlan> {
    let mut plan = build_video_plan(
        probe,
        target_max_bytes,
        FIRST_TARGET_RATIO,
        input,
        output,
    )?;
    scale_video_bitrate_in_plan(&mut plan, quality_bump_bitrate_scale(actual_bytes, target_max_bytes));
    Ok(plan)
}

pub fn build_quality_bump_image_plan(
    probe: &MediaProbe,
    input_size: u64,
    target_max_bytes: i64,
    actual_bytes: u64,
    input: &Path,
    output: &Path,
) -> Result<EncodePlan> {
    let mut plan = build_image_plan(
        probe,
        input_size,
        target_max_bytes,
        FIRST_TARGET_RATIO,
        input,
        output,
    )?;
    improve_image_quality_in_plan(&mut plan, quality_bump_bitrate_scale(actual_bytes, target_max_bytes));
    Ok(plan)
}

fn quality_bump_bitrate_scale(actual_bytes: u64, limit: i64) -> f64 {
    if !is_under_quality_floor(actual_bytes, limit) {
        return 1.0;
    }

    let actual_ratio = (actual_bytes as f64) / (limit as f64);
    if actual_ratio <= 0.0 {
        return 1.0;
    }

    (QUALITY_BUMP_TARGET_RATIO / actual_ratio).clamp(1.0, 2.0)
}

fn scale_video_bitrate_in_plan(plan: &mut EncodePlan, scale: f64) {
    if (scale - 1.0).abs() < f64::EPSILON {
        return;
    }

    let video_bps = plan
        .args
        .windows(2)
        .find_map(|pair| pair[0].eq("-b:v").then(|| pair[1].parse::<u64>().ok()))
        .flatten()
        .expect("video encode plan must include -b:v");

    let scaled = ((video_bps as f64) * scale).round() as u64;
    let max_rate = ((scaled as f64) * 1.2).round() as u64;
    let buf_size = scaled * 2;

    for index in 0..plan.args.len() {
        match plan.args[index].as_str() {
            "-b:v" => plan.args[index + 1] = scaled.to_string(),
            "-maxrate" => plan.args[index + 1] = max_rate.to_string(),
            "-bufsize" => plan.args[index + 1] = buf_size.to_string(),
            _ => {}
        }
    }

    plan.planned_budget_bytes = ((plan.planned_budget_bytes as f64) * scale).ceil() as u64;
}

fn improve_image_quality_in_plan(plan: &mut EncodePlan, scale: f64) {
    if (scale - 1.0).abs() < f64::EPSILON {
        return;
    }

    let quality_index = plan
        .args
        .iter()
        .position(|arg| arg == "-q:v")
        .map(|index| index + 1)
        .expect("image encode plan must include -q:v");

    let current = plan.args[quality_index]
        .parse::<u32>()
        .expect("image quality must be numeric");
    let improved = ((current as f64) / scale).round().clamp(2.0, 12.0) as u32;
    plan.args[quality_index] = improved.to_string();
    plan.planned_budget_bytes = ((plan.planned_budget_bytes as f64) * scale).ceil() as u64;
}

fn choose_scale_height(video_bps: u64) -> u32 {
    if video_bps >= BITRATE_THRESHOLD_HD {
        SCALE_HEIGHT_HD
    } else if video_bps >= BITRATE_THRESHOLD_SD {
        SCALE_HEIGHT_SD
    } else {
        SCALE_HEIGHT_LOW
    }
}

fn image_scale_filter(probe: &MediaProbe, input_size: u64, budget_bytes: u64) -> Option<String> {
    if input_size <= budget_bytes {
        return None;
    }

    let scale_factor = ((budget_bytes as f64) / (input_size as f64)).sqrt();
    let target_width = ((probe.width as f64) * scale_factor)
        .round()
        .clamp(1.0, probe.width as f64) as u32;

    if target_width >= probe.width {
        return None;
    }

    Some(format!("scale={target_width}:-2"))
}

fn image_quality_value(input_size: u64, budget_bytes: u64) -> u32 {
    let ratio = (input_size as f64) / (budget_bytes as f64);
    if ratio <= 1.5 {
        3
    } else if ratio <= 3.0 {
        5
    } else if ratio <= 6.0 {
        8
    } else {
        12
    }
}

fn path_to_str(path: &Path) -> Result<&str> {
    path.to_str()
        .with_context(|| format!("path is not valid utf-8: {}", path.display()))
}

#[cfg(test)]
mod tests {
    use super::*;

    fn sample_video_probe() -> MediaProbe {
        MediaProbe {
            duration_secs: 100.0,
            width: 1920,
            height: 1080,
            video_bit_rate: Some(8_000_000),
            audio_bit_rate: Some(128_000),
            format_size: Some(100_000_000),
            is_image: false,
        }
    }

    fn sample_image_probe() -> MediaProbe {
        MediaProbe {
            duration_secs: 0.0,
            width: 4000,
            height: 3000,
            video_bit_rate: None,
            audio_bit_rate: None,
            format_size: Some(4_500_000),
            is_image: true,
        }
    }

    #[test]
    fn target_bytes_applies_ratio_and_overhead() {
        let bytes = target_bytes(1_000_000, 0.95);
        assert_eq!(bytes, 931_000);
    }

    #[test]
    fn threshold_helpers_classify_actual_size() {
        let limit = 1_000_000_i64;
        assert!(is_under_quality_floor(600_000, limit));
        assert!(!is_under_quality_floor(750_000, limit));
        assert!(exceeds_hard_cap(1_000_001, limit));
        assert!(!exceeds_hard_cap(950_000, limit));
        assert!(in_acceptance_band(800_000, limit));
        assert!(!in_acceptance_band(600_000, limit));
        assert!(!in_acceptance_band(1_100_000, limit));
    }

    #[test]
    fn build_video_plan_targets_budget_with_bitrate_mode() {
        let plan = build_video_plan(
            &sample_video_probe(),
            10_000_000,
            FIRST_TARGET_RATIO,
            Path::new("/tmp/input.mp4"),
            Path::new("/tmp/output.mp4"),
        )
        .unwrap();

        assert!(plan.args.contains(&"-b:v".to_owned()));
        assert!(plan.args.iter().any(|arg| arg.starts_with("scale=-2:")));
        assert!(plan.planned_budget_bytes > 0);
        assert!(plan.planned_budget_bytes <= target_bytes(10_000_000, FIRST_TARGET_RATIO));
    }

    #[test]
    fn build_video_plan_uses_lower_scale_for_tight_budget() {
        let probe = MediaProbe {
            duration_secs: 3_600.0,
            width: 1920,
            height: 1080,
            video_bit_rate: None,
            audio_bit_rate: None,
            format_size: None,
            is_image: false,
        };

        let plan = build_video_plan(
            &probe,
            50_000_000,
            SECOND_TARGET_RATIO,
            Path::new("/tmp/input.mp4"),
            Path::new("/tmp/output.mp4"),
        )
        .unwrap();

        let scale = plan
            .args
            .windows(2)
            .find_map(|pair| pair[0].eq("-vf").then(|| pair[1].clone()))
            .unwrap();
        assert!(scale.contains(&SCALE_HEIGHT_LOW.to_string()));
    }

    #[test]
    fn build_image_plan_adds_scale_when_input_is_much_larger_than_budget() {
        let plan = build_image_plan(
            &sample_image_probe(),
            20_000_000,
            1_000_000,
            FIRST_TARGET_RATIO,
            Path::new("/tmp/input.jpg"),
            Path::new("/tmp/output.jpg"),
        )
        .unwrap();

        assert!(plan
            .args
            .iter()
            .any(|arg| arg.starts_with("scale=")));
        assert!(plan.args.contains(&"-q:v".to_owned()));
    }

    #[test]
    fn quality_bump_plan_raises_bitrate_when_output_was_too_small() {
        let limit = 10_000_000_i64;
        let first = build_video_plan(
            &sample_video_probe(),
            limit,
            FIRST_TARGET_RATIO,
            Path::new("/tmp/input.mp4"),
            Path::new("/tmp/output.mp4"),
        )
        .unwrap();
        let bump = build_quality_bump_video_plan(
            &sample_video_probe(),
            limit,
            5_000_000,
            Path::new("/tmp/input.mp4"),
            Path::new("/tmp/output.mp4"),
        )
        .unwrap();

        assert!(bump.planned_budget_bytes > first.planned_budget_bytes);

        let first_video_bps = first
            .args
            .windows(2)
            .find_map(|pair| pair[0].eq("-b:v").then(|| pair[1].parse::<u64>().ok()))
            .flatten()
            .unwrap();
        let bump_video_bps = bump
            .args
            .windows(2)
            .find_map(|pair| pair[0].eq("-b:v").then(|| pair[1].parse::<u64>().ok()))
            .flatten()
            .unwrap();
        assert!(bump_video_bps > first_video_bps);
    }

    #[test]
    fn quality_bump_bitrate_scale_is_neutral_when_already_in_band() {
        assert_eq!(quality_bump_bitrate_scale(800_000, 1_000_000), 1.0);
    }
}

use std::path::Path;

use anyhow::{bail, Context, Result};
use tokio::process::Command;

use crate::encode_plan::{
    self, build_image_plan, build_quality_bump_image_plan, build_quality_bump_video_plan,
    build_video_plan, exceeds_hard_cap, EncodePlan, FIRST_TARGET_RATIO, SECOND_TARGET_RATIO,
};
use crate::job::{MediaKind, MediaUploadedJob};
use crate::probe::{self, DEFAULT_FEASIBILITY_RATIO, MediaProbe};

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum EncodeStage {
    First,
    QualityBump,
    Second,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum EncodeNextStep {
    Done,
    QualityBump,
    SecondAttempt,
    Failed,
}

enum PlanSpec {
    TargetRatio(f64),
    QualityBump { actual_bytes: u64 },
}

pub async fn process_media(job: &MediaUploadedJob, input: &Path, output: &Path) -> Result<u64> {
    job.validate()?;

    let input_size = file_size(input).await?;

    if input_size <= job.target_max_bytes as u64 {
        tokio::fs::copy(input, output).await?;
        return Ok(input_size);
    }

    let media_probe = probe::probe_media(input).await?;
    probe::ensure_feasible(
        &media_probe,
        job.target_max_bytes,
        DEFAULT_FEASIBILITY_RATIO,
    )?;

    let kind = job.media_kind()?;
    compress_with_probe(
        kind,
        &media_probe,
        input_size,
        input,
        output,
        job.target_max_bytes,
    )
    .await
}

async fn compress_with_probe(
    kind: MediaKind,
    media_probe: &MediaProbe,
    input_size: u64,
    input: &Path,
    output: &Path,
    target_max_bytes: i64,
) -> Result<u64> {
    let mut stage = EncodeStage::First;
    let mut previous_size: Option<u64> = None;

    loop {
        let spec = match stage {
            EncodeStage::First => PlanSpec::TargetRatio(FIRST_TARGET_RATIO),
            EncodeStage::QualityBump => PlanSpec::QualityBump {
                actual_bytes: previous_size.expect("quality bump requires prior encode size"),
            },
            EncodeStage::Second => PlanSpec::TargetRatio(SECOND_TARGET_RATIO),
        };

        let plan = build_encode_plan(
            kind,
            spec,
            media_probe,
            input_size,
            target_max_bytes,
            input,
            output,
        )?;
        let size = run_plan(&plan).await?;
        previous_size = Some(size);

        match next_step_after_encode(stage, size, target_max_bytes) {
            EncodeNextStep::Done => return Ok(size),
            EncodeNextStep::QualityBump => stage = EncodeStage::QualityBump,
            EncodeNextStep::SecondAttempt => stage = EncodeStage::Second,
            EncodeNextStep::Failed => {
                if stage == EncodeStage::Second {
                    bail!("media could not be compressed below {target_max_bytes} bytes");
                }
                bail!("unexpected processing state after {stage:?} encode");
            }
        }
    }
}

pub fn next_step_after_encode(stage: EncodeStage, actual: u64, limit: i64) -> EncodeNextStep {
    if exceeds_hard_cap(actual, limit) {
        return match stage {
            EncodeStage::Second => EncodeNextStep::Failed,
            _ => EncodeNextStep::SecondAttempt,
        };
    }

    if stage == EncodeStage::First && encode_plan::is_under_quality_floor(actual, limit) {
        return EncodeNextStep::QualityBump;
    }

    EncodeNextStep::Done
}

fn build_encode_plan(
    kind: MediaKind,
    spec: PlanSpec,
    media_probe: &MediaProbe,
    input_size: u64,
    target_max_bytes: i64,
    input: &Path,
    output: &Path,
) -> Result<EncodePlan> {
    match (kind, spec) {
        (MediaKind::Video, PlanSpec::TargetRatio(target_ratio)) => {
            build_video_plan(media_probe, target_max_bytes, target_ratio, input, output)
        }
        (MediaKind::Image, PlanSpec::TargetRatio(target_ratio)) => build_image_plan(
            media_probe,
            input_size,
            target_max_bytes,
            target_ratio,
            input,
            output,
        ),
        (MediaKind::Video, PlanSpec::QualityBump { actual_bytes }) => {
            build_quality_bump_video_plan(
                media_probe,
                target_max_bytes,
                actual_bytes,
                input,
                output,
            )
        }
        (MediaKind::Image, PlanSpec::QualityBump { actual_bytes }) => build_quality_bump_image_plan(
            media_probe,
            input_size,
            target_max_bytes,
            actual_bytes,
            input,
            output,
        ),
    }
}

async fn run_plan(plan: &EncodePlan) -> Result<u64> {
    run_ffmpeg(&plan.args).await?;

    let output = plan
        .args
        .last()
        .context("encode plan missing output path")?;
    file_size(Path::new(output)).await
}

async fn run_ffmpeg(args: &[String]) -> Result<()> {
    let arg_refs: Vec<&str> = args.iter().map(String::as_str).collect();
    let status = Command::new("ffmpeg")
        .args(&arg_refs)
        .status()
        .await
        .context("spawn ffmpeg")?;

    if !status.success() {
        bail!("ffmpeg exited with status {status}");
    }

    Ok(())
}

async fn file_size(path: &Path) -> Result<u64> {
    Ok(tokio::fs::metadata(path)
        .await
        .with_context(|| format!("stat file {}", path.display()))?
        .len())
}

#[cfg(test)]
mod tests {
    use super::*;

    const LIMIT: i64 = 1_000_000;

    #[test]
    fn first_encode_accepts_output_within_band() {
        assert_eq!(
            next_step_after_encode(EncodeStage::First, 800_000, LIMIT),
            EncodeNextStep::Done
        );
    }

    #[test]
    fn first_encode_requests_quality_bump_when_output_is_too_small() {
        assert_eq!(
            next_step_after_encode(EncodeStage::First, 600_000, LIMIT),
            EncodeNextStep::QualityBump
        );
    }

    #[test]
    fn first_encode_requests_second_attempt_when_output_exceeds_limit() {
        assert_eq!(
            next_step_after_encode(EncodeStage::First, 1_100_000, LIMIT),
            EncodeNextStep::SecondAttempt
        );
    }

    #[test]
    fn quality_bump_accepts_output_under_limit_even_if_still_small() {
        assert_eq!(
            next_step_after_encode(EncodeStage::QualityBump, 650_000, LIMIT),
            EncodeNextStep::Done
        );
    }

    #[test]
    fn quality_bump_requests_second_attempt_when_output_exceeds_limit() {
        assert_eq!(
            next_step_after_encode(EncodeStage::QualityBump, 1_050_000, LIMIT),
            EncodeNextStep::SecondAttempt
        );
    }

    #[test]
    fn second_encode_fails_when_output_still_exceeds_limit() {
        assert_eq!(
            next_step_after_encode(EncodeStage::Second, 1_001_000, LIMIT),
            EncodeNextStep::Failed
        );
    }

    #[test]
    fn second_encode_accepts_output_within_limit() {
        assert_eq!(
            next_step_after_encode(EncodeStage::Second, 900_000, LIMIT),
            EncodeNextStep::Done
        );
    }
}

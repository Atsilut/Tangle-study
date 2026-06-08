use std::path::Path;

use anyhow::{bail, Context, Result};
use tokio::process::Command;

use crate::encode_plan::{
    self, build_image_plan, build_quality_bump_image_plan, build_quality_bump_video_plan,
    build_video_plan, exceeds_hard_cap, EncodePlan, FIRST_TARGET_RATIO, SECOND_TARGET_RATIO,
};
use crate::job::MediaUploadedJob;
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

pub async fn process_media(job: &MediaUploadedJob, input: &Path, output: &Path) -> Result<u64> {
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

    compress_with_probe(
        &job.kind,
        &media_probe,
        input_size,
        input,
        output,
        job.target_max_bytes,
    )
    .await
}

async fn compress_with_probe(
    kind: &str,
    media_probe: &MediaProbe,
    input_size: u64,
    input: &Path,
    output: &Path,
    target_max_bytes: i64,
) -> Result<u64> {
    let first_plan = build_plan(
        kind,
        media_probe,
        input_size,
        target_max_bytes,
        FIRST_TARGET_RATIO,
        input,
        output,
    )?;
    let first_size = run_plan(&first_plan).await?;

    match next_step_after_encode(EncodeStage::First, first_size, target_max_bytes) {
        EncodeNextStep::Done => return Ok(first_size),
        EncodeNextStep::QualityBump => {
            let bump_plan = build_bump_plan(
                kind,
                media_probe,
                input_size,
                target_max_bytes,
                first_size,
                input,
                output,
            )?;
            let bump_size = run_plan(&bump_plan).await?;

            match next_step_after_encode(EncodeStage::QualityBump, bump_size, target_max_bytes) {
                EncodeNextStep::Done => Ok(bump_size),
                EncodeNextStep::SecondAttempt => {
                    run_second_attempt(
                        kind,
                        media_probe,
                        input_size,
                        input,
                        output,
                        target_max_bytes,
                    )
                    .await
                }
                EncodeNextStep::QualityBump | EncodeNextStep::Failed => {
                    bail!("unexpected processing state after quality bump")
                }
            }
        }
        EncodeNextStep::SecondAttempt => {
            run_second_attempt(
                kind,
                media_probe,
                input_size,
                input,
                output,
                target_max_bytes,
            )
            .await
        }
        EncodeNextStep::Failed => bail!("unexpected processing state after first encode"),
    }
}

async fn run_second_attempt(
    kind: &str,
    media_probe: &MediaProbe,
    input_size: u64,
    input: &Path,
    output: &Path,
    target_max_bytes: i64,
) -> Result<u64> {
    let plan = build_plan(
        kind,
        media_probe,
        input_size,
        target_max_bytes,
        SECOND_TARGET_RATIO,
        input,
        output,
    )?;
    let size = run_plan(&plan).await?;

    match next_step_after_encode(EncodeStage::Second, size, target_max_bytes) {
        EncodeNextStep::Done => Ok(size),
        EncodeNextStep::Failed => {
            bail!("media could not be compressed below {target_max_bytes} bytes")
        }
        EncodeNextStep::QualityBump | EncodeNextStep::SecondAttempt => {
            bail!("unexpected processing state after second encode")
        }
    }
}

pub fn next_step_after_encode(stage: EncodeStage, actual: u64, limit: i64) -> EncodeNextStep {
    match stage {
        EncodeStage::First => {
            if exceeds_hard_cap(actual, limit) {
                EncodeNextStep::SecondAttempt
            } else if encode_plan::is_under_quality_floor(actual, limit) {
                EncodeNextStep::QualityBump
            } else {
                EncodeNextStep::Done
            }
        }
        EncodeStage::QualityBump => {
            if exceeds_hard_cap(actual, limit) {
                EncodeNextStep::SecondAttempt
            } else {
                EncodeNextStep::Done
            }
        }
        EncodeStage::Second => {
            if exceeds_hard_cap(actual, limit) {
                EncodeNextStep::Failed
            } else {
                EncodeNextStep::Done
            }
        }
    }
}

fn build_plan(
    kind: &str,
    media_probe: &MediaProbe,
    input_size: u64,
    target_max_bytes: i64,
    target_ratio: f64,
    input: &Path,
    output: &Path,
) -> Result<EncodePlan> {
    match kind.to_ascii_lowercase().as_str() {
        "video" => build_video_plan(media_probe, target_max_bytes, target_ratio, input, output),
        "image" => build_image_plan(
            media_probe,
            input_size,
            target_max_bytes,
            target_ratio,
            input,
            output,
        ),
        other => bail!("unsupported media kind {other}"),
    }
}

fn build_bump_plan(
    kind: &str,
    media_probe: &MediaProbe,
    input_size: u64,
    target_max_bytes: i64,
    actual_bytes: u64,
    input: &Path,
    output: &Path,
) -> Result<EncodePlan> {
    match kind.to_ascii_lowercase().as_str() {
        "video" => build_quality_bump_video_plan(
            media_probe,
            target_max_bytes,
            actual_bytes,
            input,
            output,
        ),
        "image" => build_quality_bump_image_plan(
            media_probe,
            input_size,
            target_max_bytes,
            actual_bytes,
            input,
            output,
        ),
        other => bail!("unsupported media kind {other}"),
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

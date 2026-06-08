use std::path::Path;

use anyhow::{bail, Context, Result};
use tokio::process::Command;

use crate::job::MediaUploadedJob;

pub async fn process_media(job: &MediaUploadedJob, input: &Path, output: &Path) -> Result<u64> {
    let metadata = tokio::fs::metadata(input)
        .await
        .with_context(|| format!("stat input file {}", input.display()))?;
    let input_size = metadata.len();

    if input_size <= job.target_max_bytes as u64 {
        tokio::fs::copy(input, output).await?;
        return Ok(input_size);
    }

    match job.kind.to_ascii_lowercase().as_str() {
        "video" => compress_video(input, output, job.target_max_bytes).await,
        "image" => compress_image(input, output, job.target_max_bytes).await,
        other => bail!("unsupported media kind {other}"),
    }
}

async fn compress_video(input: &Path, output: &Path, target_max_bytes: i64) -> Result<u64> {
    for crf in [28_i32, 32, 36] {
        run_ffmpeg(&[
            "-y",
            "-i",
            input.to_str().context("input path is not valid utf-8")?,
            "-vf",
            "scale=-2:720",
            "-c:v",
            "libx264",
            "-preset",
            "veryfast",
            "-crf",
            &crf.to_string(),
            "-c:a",
            "aac",
            "-b:a",
            "128k",
            "-movflags",
            "+faststart",
            output.to_str().context("output path is not valid utf-8")?,
        ])
        .await?;

        let size = file_size(output).await?;
        if size <= target_max_bytes as u64 {
            return Ok(size);
        }
    }

    bail!("video could not be compressed below {target_max_bytes} bytes");
}

async fn compress_image(input: &Path, output: &Path, target_max_bytes: i64) -> Result<u64> {
    for quality in [85_i32, 70, 55] {
        run_ffmpeg(&[
            "-y",
            "-i",
            input.to_str().context("input path is not valid utf-8")?,
            "-q:v",
            &quality.to_string(),
            output.to_str().context("output path is not valid utf-8")?,
        ])
        .await?;

        let size = file_size(output).await?;
        if size <= target_max_bytes as u64 {
            return Ok(size);
        }
    }

    bail!("image could not be compressed below {target_max_bytes} bytes");
}

async fn run_ffmpeg(args: &[&str]) -> Result<()> {
    let status = Command::new("ffmpeg")
        .args(args)
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
        .with_context(|| format!("stat output file {}", path.display()))?
        .len())
}

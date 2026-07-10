use std::path::Path;

use anyhow::{bail, Context, Result};
use tempfile::tempdir;
use tracing::info;

use crate::api_callback;
use crate::config::MediaConfig;
use crate::job::MediaUploadedJob;
use crate::processing;
use crate::storage::BlobStorage;

pub async fn handle(
    job: &MediaUploadedJob,
    config: &MediaConfig,
    http: &reqwest::Client,
) -> Result<()> {
    if config.azure_storage_connection_string.trim().is_empty() {
        bail!("AZURE_STORAGE_CONNECTION_STRING is not configured");
    }

    let storage = BlobStorage::from_connection_string(
        &config.azure_storage_connection_string,
        &config.media_container_name,
    )?;
    let work_dir = tempdir().context("create temp work directory")?;
    let input_path = work_dir.path().join("input.bin");
    let output_path = work_dir.path().join("processed.bin");

    storage
        .download_to_file(&job.original_object_key, &input_path)
        .await
        .with_context(|| format!("download blob {}", job.original_object_key))?;

    let processed_object_key = build_processed_object_key(job);
    match processing::process_media(job, &input_path, &output_path).await {
        Ok(stored_size_bytes) => {
            storage
                .upload_file(&processed_object_key, &output_path, &job.mime_type)
                .await
                .with_context(|| format!("upload processed blob {processed_object_key}"))?;

            api_callback::report_success(
                http,
                &config.core,
                job.media_asset_id,
                &processed_object_key,
                stored_size_bytes,
            )
            .await?;

            info!(
                media_asset_id = job.media_asset_id,
                processed_object_key = %processed_object_key,
                stored_size_bytes = stored_size_bytes,
                "media.uploaded job processed"
            );
            Ok(())
        }
        Err(err) => {
            let reason = err.to_string();
            api_callback::report_failure(&http, &config.core, job.media_asset_id, &reason).await?;
            info!(
                media_asset_id = job.media_asset_id,
                error = %reason,
                "media.uploaded job failed and callback reported"
            );
            Ok(())
        }
    }
}

fn build_processed_object_key(job: &MediaUploadedJob) -> String {
    let file_name = Path::new(&job.original_object_key)
        .file_name()
        .and_then(|name| name.to_str())
        .unwrap_or("processed.bin");

    format!("processed/{}/{}", job.media_asset_id, file_name)
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::job::MediaUploadedJob;

    fn sample_job(original_object_key: &str) -> MediaUploadedJob {
        MediaUploadedJob {
            media_asset_id: 42,
            intended_context: "Post".to_owned(),
            kind: "Video".to_owned(),
            mime_type: "video/mp4".to_owned(),
            original_object_key: original_object_key.to_owned(),
            original_size_bytes: 100,
            target_max_bytes: 1_000,
        }
    }

    #[test]
    fn build_processed_object_key_preserves_file_name() {
        let key = build_processed_object_key(&sample_job("raw/9/video.mp4"));
        assert_eq!(key, "processed/42/video.mp4");
    }

    #[test]
    fn build_processed_object_key_falls_back_when_path_has_no_file_name() {
        let key = build_processed_object_key(&sample_job(""));
        assert_eq!(key, "processed/42/processed.bin");
    }
}

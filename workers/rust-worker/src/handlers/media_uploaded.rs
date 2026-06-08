use std::path::Path;

use anyhow::{bail, Context, Result};
use tempfile::tempdir;
use tracing::info;

use crate::api_callback;
use crate::config::Config;
use crate::job::MediaUploadedJob;
use crate::processing;
use crate::storage::BlobStorage;

pub async fn handle(job: &MediaUploadedJob, config: &Config) -> Result<()> {
    if config.azure_storage_connection_string.trim().is_empty() {
        bail!("AZURE_STORAGE_CONNECTION_STRING is not configured");
    }

    let storage = BlobStorage::from_connection_string(
        &config.azure_storage_connection_string,
        &config.media_container_name,
    )?;
    let http = reqwest::Client::new();
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
                &http,
                config,
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
            api_callback::report_failure(&http, config, job.media_asset_id, &reason).await?;
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

//! Run with: `cargo test -p worker-media --test media_handler_integration -- --ignored`

use reqwest::Client;
use worker_media::config::MediaConfig;
use worker_media::handler;
use worker_media::job::MediaUploadedJob;

#[tokio::test]
#[ignore = "requires compose stack (media, azurite)"]
async fn handle_image_job_reports_success_to_media() {
    let media_config = MediaConfig::from_env().expect("media config");
    media_config.validate_consumer().expect("validate");

    let job = MediaUploadedJob {
        media_asset_id: 1,
        intended_context: "Post".to_owned(),
        kind: "Image".to_owned(),
        mime_type: "image/jpeg".to_owned(),
        original_object_key: std::env::var("TEST_BLOB_KEY").unwrap_or_else(|_| {
            "raw/1/b2c3137bfd2d4c5db1550bcc60923bb0/sample.jpg".to_owned()
        }),
        original_size_bytes: 249,
        target_max_bytes: 157_286_400,
    };

    let http = Client::builder()
        .timeout(std::time::Duration::from_secs(30))
        .build()
        .expect("http client");

    tokio::time::timeout(
        std::time::Duration::from_secs(30),
        handler::handle(&job, &media_config, &http),
    )
    .await
    .expect("handler timed out")
    .expect("handler failed");
}

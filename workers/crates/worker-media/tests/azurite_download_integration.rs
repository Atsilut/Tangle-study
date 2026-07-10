//! Run with: `cargo test -p worker-media --test azurite_download_integration -- --ignored`

use std::path::Path;

use worker_media::storage::BlobStorage;

#[tokio::test]
#[ignore = "requires Azurite with tangle-media/raw blob"]
async fn download_fixture_blob_from_azurite() {
    let connection_string = std::env::var("AZURE_STORAGE_CONNECTION_STRING").unwrap_or_else(|_| {
        "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://azurite:10000/devstoreaccount1;".to_owned()
    });
    let object_key = std::env::var("TEST_BLOB_KEY").unwrap_or_else(|_| {
        "raw/1/b2c3137bfd2d4c5db1550bcc60923bb0/sample.jpg".to_owned()
    });

    let storage =
        BlobStorage::from_connection_string(&connection_string, "tangle-media").expect("storage client");

    let dir = tempfile::tempdir().expect("tempdir");
    let path = dir.path().join("sample.jpg");

    let result = tokio::time::timeout(
        std::time::Duration::from_secs(15),
        storage.download_to_file(&object_key, &path),
    )
    .await;

    match result {
        Ok(Ok(())) => {
            let size = tokio::fs::metadata(Path::new(&path)).await.expect("metadata").len();
            assert!(size > 0, "downloaded blob should not be empty");
        }
        Ok(Err(err)) => panic!("download failed: {err:#}"),
        Err(_) => panic!("download timed out after 15s (likely azure SDK hang against Azurite)"),
    }
}

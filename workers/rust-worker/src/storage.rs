use std::path::Path;

use anyhow::{Context, Result};
use azure_storage::ConnectionString;
use azure_storage::CloudLocation;
use azure_storage_blobs::prelude::*;
use futures::stream::StreamExt;
use tokio::io::AsyncWriteExt;

pub struct BlobStorage {
    container_client: ContainerClient,
}

impl BlobStorage {
    pub fn from_connection_string(connection_string: &str, container_name: &str) -> Result<Self> {
        let connection = ConnectionString::new(connection_string)
            .context("parse azure storage connection string")?;
        let account = connection
            .account_name
            .context("connection string missing account name")?;
        let credentials = connection
            .storage_credentials()
            .context("build storage credentials from connection string")?;

        let cloud_location = if let Some(blob_endpoint) = connection.blob_endpoint {
            CloudLocation::Custom {
                account: account.to_owned(),
                uri: blob_endpoint.trim_end_matches('/').to_owned(),
            }
        } else {
            CloudLocation::Public {
                account: account.to_owned(),
            }
        };

        Ok(Self {
            container_client: ClientBuilder::with_location(cloud_location, credentials)
                .container_client(container_name),
        })
    }

    pub async fn download_to_file(&self, object_key: &str, path: &Path) -> Result<()> {
        let blob_client = self.container_client.blob_client(object_key);
        let mut stream = blob_client.get().into_stream();
        let mut file = tokio::fs::File::create(path)
            .await
            .with_context(|| format!("create file {}", path.display()))?;

        while let Some(response) = stream.next().await {
            let mut body = response.context("download blob content")?.data;
            while let Some(chunk) = body.next().await {
                let chunk = chunk.context("download blob content")?;
                file.write_all(&chunk)
                    .await
                    .with_context(|| format!("write blob chunk to {}", path.display()))?;
            }
        }

        file.flush().await?;
        Ok(())
    }

    pub async fn upload_file(&self, object_key: &str, path: &Path, content_type: &str) -> Result<()> {
        let data = tokio::fs::read(path).await?;
        self.container_client
            .blob_client(object_key)
            .put_block_blob(data)
            .content_type(content_type.to_owned())
            .await
            .context("upload processed blob")?;
        Ok(())
    }
}

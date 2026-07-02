use std::path::Path;

use anyhow::{Context, Result};
use azure_storage::ConnectionString;
use azure_storage::CloudLocation;
use azure_storage_blobs::prelude::*;
use futures::stream::StreamExt;
use tokio::io::{AsyncReadExt, AsyncWriteExt};

/// Azure block blob max is 4000 MiB; 4 MiB keeps memory bounded while limiting round trips.
const UPLOAD_BLOCK_SIZE: usize = 4 * 1024 * 1024;

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
        let blob_client = self.container_client.blob_client(object_key);
        let mut file = tokio::fs::File::open(path)
            .await
            .with_context(|| format!("open file {}", path.display()))?;

        let mut block_list = BlockList::default();
        let mut buffer = vec![0u8; UPLOAD_BLOCK_SIZE];
        let mut block_index = 0u32;

        loop {
            let bytes_read = file
                .read(&mut buffer)
                .await
                .with_context(|| format!("read file chunk from {}", path.display()))?;
            if bytes_read == 0 {
                break;
            }

            let block_id = format!("block{block_index:06}");
            blob_client
                .put_block(block_id.clone(), buffer[..bytes_read].to_vec())
                .await
                .context("upload blob block")?;
            block_list
                .blocks
                .push(BlobBlockType::new_uncommitted(block_id));
            block_index += 1;
        }

        if block_list.blocks.is_empty() {
            blob_client
                .put_block_blob(Vec::<u8>::new())
                .content_type(content_type.to_owned())
                .await
                .context("upload processed blob")?;
            return Ok(());
        }

        blob_client
            .put_block_list(block_list)
            .content_type(content_type.to_owned())
            .await
            .context("commit uploaded blob blocks")?;
        Ok(())
    }
}

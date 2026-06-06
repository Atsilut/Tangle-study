pub mod chat_message_created;

use anyhow::Result;

use crate::job::ChatMessageCreatedJob;

/// Process a decoded job payload.
pub async fn dispatch_chat_message_created(job: &ChatMessageCreatedJob) -> Result<()> {
    chat_message_created::handle(job).await
}

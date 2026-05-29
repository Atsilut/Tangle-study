pub mod chat_message_created;

use anyhow::Result;

use crate::job::ChatMessageCreatedJob;

/// Process a decoded job payload. Implemented in later milestones.
#[allow(dead_code)]
pub async fn dispatch_chat_message_created(job: &ChatMessageCreatedJob) -> Result<()> {
    chat_message_created::handle(job).await
}

use anyhow::Result;
use tracing::info;

use worker_core::job::ChatMessageCreatedJob;

/// Placeholder handler until async work (e.g. media processing) is defined.
pub async fn handle(job: &ChatMessageCreatedJob) -> Result<()> {
    info!(
        message_id = job.message_id,
        chat_room_id = job.chat_room_id,
        sender_user_id = job.sender_user_id,
        "chat.message.created job received (handler stub)"
    );
    Ok(())
}

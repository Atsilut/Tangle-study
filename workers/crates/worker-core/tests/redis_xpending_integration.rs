//! Run with: `cargo test -p worker-core --test redis_xpending_integration -- --ignored`
//! Requires `REDIS_URL` (defaults to redis://127.0.0.1:6379).

use redis::aio::ConnectionManager;
use redis::{AsyncCommands, Client};

#[tokio::test]
#[ignore = "requires a running Redis with pending media.uploaded messages"]
async fn xpending_count_returns_pending_ids() -> redis::RedisResult<()> {
    let redis_url = std::env::var("REDIS_URL").unwrap_or_else(|_| "redis://127.0.0.1:6379".to_owned());
    let client = Client::open(redis_url)?;
    let mut conn = ConnectionManager::new(client).await?;

    let pending: redis::streams::StreamPendingCountReply = conn
        .xpending_count(
            "tangle:queue:media.uploaded",
            "tangle-study-workers",
            "-",
            "+",
            10,
        )
        .await?;

    eprintln!("pending ids: {}", pending.ids.len());
    for item in &pending.ids {
        eprintln!(
            "  id={} consumer={} idle_ms={} times_delivered={}",
            item.id, item.consumer, item.last_delivered_ms, item.times_delivered
        );
    }

    assert!(
        !pending.ids.is_empty(),
        "expected at least one pending message on tangle:queue:media.uploaded"
    );

    Ok(())
}

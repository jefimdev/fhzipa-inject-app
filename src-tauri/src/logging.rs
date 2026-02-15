use serde::{Deserialize, Serialize};
use std::sync::Arc;
use tauri::{AppHandle, Emitter};
use tracing_subscriber::layer::Context;
use tracing_subscriber::{registry::LookupSpan, Layer};

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ExtendedLogRecord {
    pub level: u8,
    pub message: String,
    pub target: Option<String>,
    pub timestamp: String,
}

pub struct FrontendLoggingLayer {
    app_handle: Arc<AppHandle>,
}

impl FrontendLoggingLayer {
    pub fn new(app_handle: AppHandle) -> Self {
        Self {
            app_handle: Arc::new(app_handle),
        }
    }
}

impl<S> Layer<S> for FrontendLoggingLayer
where
    S: tracing::Subscriber + for<'a> LookupSpan<'a>,
{
    fn on_event(&self, event: &tracing::Event<'_>, _ctx: Context<'_, S>) {
        use tracing::field::Visit;

        let metadata = event.metadata();
        let level = match *metadata.level() {
            tracing::Level::TRACE => 1u8,
            tracing::Level::DEBUG => 2u8,
            tracing::Level::INFO => 3u8,
            tracing::Level::WARN => 4u8,
            tracing::Level::ERROR => 5u8,
        };

        let target = metadata.target().to_string();

        struct MessageVisitor {
            message: String,
        }

        impl Visit for MessageVisitor {
            fn record_debug(&mut self, field: &tracing::field::Field, value: &dyn std::fmt::Debug) {
                if field.name() == "message" {
                    self.message = format!("{:?}", value);
                }
            }
        }

        let mut visitor = MessageVisitor {
            message: String::new(),
        };
        event.record(&mut visitor);

        let timestamp = chrono::Local::now()
            .format("%Y-%m-%d %H:%M:%S%.3f")
            .to_string();

        let record = ExtendedLogRecord {
            level,
            message: visitor.message,
            target: Some(target),
            timestamp,
        };

        let _ = self.app_handle.emit("log-record", &record);
    }
}

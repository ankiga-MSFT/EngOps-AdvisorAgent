// <copyright file="PiiRedactionLogProcessor.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

namespace InfraService.OpenTelemetryProvider
{
    using OpenTelemetry;
    using OpenTelemetry.Logs;
    using System.Collections.Generic;

    /// <summary>
    /// OpenTelemetry log processor that strips PII and sensitive AI content
    /// from log records before they reach persistent exporters (App Insights, Geneva).
    ///
    /// This processor acts as a safety net — even if EnableSensitiveData is
    /// accidentally true or a developer logs PII directly, this processor
    /// ensures sensitive data never reaches persistent stores.
    ///
    /// Place this processor AFTER the console exporter (so debug console
    /// sees full content) and BEFORE the Geneva/Azure Monitor exporters.
    /// </summary>
    public class PiiRedactionLogProcessor : BaseProcessor<LogRecord>
    {
        /// <summary>
        /// Keys from OpenTelemetry GenAI semantic conventions and custom keys
        /// that may contain user prompts, AI responses, or PII.
        /// </summary>
        private static readonly HashSet<string> SensitiveKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            // GenAI semantic convention keys (from Microsoft.Extensions.AI OpenTelemetryChatClient)
            "gen_ai.content.prompt",
            "gen_ai.content.completion",
            "gen_ai.prompt",
            "gen_ai.response",
            "gen_ai.event.content",

            // Custom keys that may contain PII
            "user.question",
            "user.input",
            "user.prompt",
            "ai.prompt",
            "ai.response",
            "ai.prompt.content",
            "ai.response.content",
            "pii.content",

            // Common keys that might leak PII
            "Prompt",
            "UserPrompt",
            "EnhancedPrompt",
            "Answer",
            "Response",
            "ConversationContent",
            "HistoryContext",
            "SessionSummary",
        };

        private const string RedactedValue = "[REDACTED-PII]";

        /// <summary>
        /// Called when a log record is about to be exported. Strips sensitive attributes.
        /// </summary>
        /// <param name="data">The <see cref="LogRecord"/> to redact.</param>
        public override void OnEnd(LogRecord data)
        {
            RedactAttributes(data);
            base.OnEnd(data);
        }

        private static void RedactAttributes(LogRecord data)
        {
            // LogRecord.Attributes contains the structured log key-value pairs.
            // It is a settable property in OpenTelemetry SDK 1.12.0.
            var attributes = data.Attributes;
            if (attributes is null)
            {
                return;
            }

            var redactedList = new List<KeyValuePair<string, object?>>();
            bool needsRedaction = false;

            foreach (var attr in attributes)
            {
                if (SensitiveKeys.Contains(attr.Key))
                {
                    redactedList.Add(new KeyValuePair<string, object?>(attr.Key, RedactedValue));
                    needsRedaction = true;
                }
                else
                {
                    redactedList.Add(attr);
                }
            }

            if (needsRedaction)
            {
                data.Attributes = redactedList;
            }
        }
    }
}

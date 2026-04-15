using Microsoft.Azure.Functions.Worker.Http;
using OpenTelemetry.Context;

namespace InfraService.OpenTelemetryProvider
{
    public static class OpenTelemetryContext
    {
        private readonly static string AFDCorrelationId = "AFDCorrelationId";

        private readonly static string AFDReferenceHeader = "x-azure-ref";

        private readonly static RuntimeContextSlot<string> AFDCorrelationContextSlot;
        static OpenTelemetryContext()
        {
            AFDCorrelationContextSlot = RuntimeContext.RegisterSlot<string>(AFDCorrelationId);
        }

        public static string? SetAFDCorrelationIdFromRequest(HttpRequestData httpRequest)
        {
            if (httpRequest != null)
            {
                var correlationIds = httpRequest.Headers
                    .FirstOrDefault(h => h.Key.Equals(AFDReferenceHeader, StringComparison.OrdinalIgnoreCase)).Value;

                if (correlationIds != null)
                {
                    var correlationId = correlationIds.FirstOrDefault();
                    if (!string.IsNullOrEmpty(correlationId))
                    {
                        // Set the correlation ID in our telemetry or logging context
                        SetAFDCorrelationId(correlationId);
                        return correlationId;
                    }
                }
            }

            return default;
        }

        internal static void SetAFDCorrelationId(string correlationId)
        {
            if (!string.IsNullOrEmpty(correlationId))
            {
                AFDCorrelationContextSlot.Set(correlationId);
            }
        }
    }
}

using Newtonsoft.Json.Linq;

namespace InfraService.Interfaces
{
    public interface IAzureMetricsService
    {
        void LogProcessingTime(IEnumerable<JObject> events, MetricContext context);
        void LogProcessingTime(JObject eventData, MetricContext context);
    }
}
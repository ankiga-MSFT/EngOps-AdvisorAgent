using InfraService.Interfaces;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Newtonsoft.Json.Linq;
namespace InfraService
{
#pragma warning disable

    public class AzureMetricsService : IAzureMetricsService
    {
        private readonly TelemetryClient telemetryClient;

        public AzureMetricsService(string connectionString)
        {
            telemetryClient = new TelemetryClient(new TelemetryConfiguration
            {
                ConnectionString = connectionString
            });
        }

        public void LogProcessingTime(JObject eventData, MetricContext context)
        {
            if (eventData == null) throw new ArgumentNullException(nameof(eventData));

            if (eventData.TryGetValue("enqueueTime", out JToken enqueueTimeToken) &&
                DateTime.TryParse(enqueueTimeToken.ToString(), out DateTime enqueueTime))
            {
                DateTime currentTime = DateTime.UtcNow;
                double processingTime = (currentTime - enqueueTime).TotalSeconds;

                var metric = new MetricTelemetry("ProcessingTime", processingTime);
                metric.Properties.Add("ServiceName", context.ServiceName);
                metric.Properties.Add("FunctionName", context.FunctionName);
                metric.Properties.Add("CallerClassName", context.CallerClassName);
                telemetryClient.TrackMetric(metric);
            }
        }

        public void LogProcessingTime(IEnumerable<JObject> events, MetricContext context)
        {
            if (events == null) throw new ArgumentNullException(nameof(events));

            foreach (var eventData in events)
            {
                LogProcessingTime(eventData, context);
            }
        }
    }

    public class MetricContext
    {
        public string ServiceName { get; set; }
        public string FunctionName { get; set; }
        public string CallerClassName { get; set; }
    }


}

/*
 --------------------USAGE----------------------
 var context = new MetricContext
        {
            ServiceName = "YourServiceName",
            FunctionName = nameof(Run),//Run is the FunctionName
            CallerClassName = nameof(ProcessEvent) //ProcessEvent is the CallerClassName
        };

 metricsService.LogProcessingTime(eventList, context);

 */

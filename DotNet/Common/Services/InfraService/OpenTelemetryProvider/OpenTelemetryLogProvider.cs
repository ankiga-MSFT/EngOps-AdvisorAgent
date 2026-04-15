// <copyright file="OpenTelemetryLogProvider.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

namespace InfraService.OpenTelemetryProvider
{
    using InfraService.Utilities;
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;

    /// <summary>
    /// Defines the <see cref="OpenTelemetryLogProvider" />.
    /// </summary>
    public class OpenTelemetryLogProvider
    {
        /// <summary>
        /// Defines the subscribedSourceName.
        /// </summary>
        private readonly string subscribedSourceName;

        /// <summary>
        /// Defines the subscribedSourceVersion.
        /// </summary>
        private readonly string subscribedSourceVersion;

        /// <summary>
        /// Gets the CustomerResourceId.
        /// </summary>
        private readonly string customerResourceId;

        /// <summary>
        /// Gets the LocationId.
        /// </summary>
        private readonly string locationId;

        /// <summary>
        /// Defines the metricsName.
        /// </summary>
        private readonly Meter metricsName;

        /// <summary>
        /// Defines the activitySource.
        /// </summary>
        private readonly ActivitySource activitySource;

        /// <summary>
        /// Defines the metricsCounterDictionary.
        /// </summary>
        private readonly ConcurrentDictionary<string, Counter<long>> metricsCounterDictionary = new();

        /// <summary>
        /// Defines the metricsCounterDictionary.
        /// </summary>
        private readonly ConcurrentDictionary<string, Histogram<double>> latencyHistogramDictionary = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenTelemetryLogProvider"/> class.
        /// </summary>
        /// <param name="subscribedSourceName">The subscribedSourceName<see cref="string"/>.</param>
        /// <param name="subscribedSourceVersion">The subscribedSourceVersion<see cref="string"/>.</param>
        public OpenTelemetryLogProvider(string subscribedSourceName = "SupportDataPlatform", string subscribedSourceVersion = "1.0", string customerResourceId = "", string locationId = "")
        {
            this.subscribedSourceName = subscribedSourceName;
            this.subscribedSourceVersion = subscribedSourceVersion;
            this.metricsName = new Meter(this.subscribedSourceName, this.subscribedSourceVersion);
            this.activitySource = new ActivitySource(this.subscribedSourceName, this.subscribedSourceVersion);

            this.customerResourceId = Requires.IsNotNull(customerResourceId, nameof(customerResourceId));
            this.locationId = Requires.IsNotNull(locationId, nameof(locationId));
        }

        /// <summary>
        /// Gets the open telemetry MetricsMeter provider to create metrics.
        /// </summary>
        public Meter MetricsMeter
        {
            get
            {
                return this.metricsName;
            }
        }

        /// <summary>
        /// Gets the open telemetry provider for the trace activity.
        /// </summary>
        public ActivitySource TraceActivitySource
        {
            get
            {
                return this.activitySource;
            }
        }

        /// <summary>
        /// The CreateCustomerResourceId.
        /// </summary>
        /// <param name="product">The product<see cref="string"/>.</param>
        /// <param name="serviceName">The serviceName<see cref="string"/>.</param>
        /// <returns>The <see cref="string"/>.</returns>
        public static string CreateCustomerResourceId(string product, string serviceName) => product + "_" + serviceName;

        /// <summary>
        /// The CreateCustomerResourceId.
        /// </summary>
        /// <param name="serviceTreeId">The serviceTreeId<see cref="string"/>.</param>
        /// <returns>The <see cref="string"/>.</returns>
        public static string CreateCustomerResourceId(string serviceTreeId) => "ServiceTreeId://" + serviceTreeId;

        /// <summary>
        /// The CreateLocationId.
        /// </summary>
        /// <param name="cloud">The cloud<see cref="string"/>.</param>
        /// <param name="region">The region<see cref="string"/>.</param>
        /// <param name="stamp">The stamp<see cref="string?"/>.</param>
        /// <returns>The <see cref="string"/>.</returns>
        public static string CreateLocationId(string cloud, string region, string? stamp = null)
        {
            var id = cloud + "_" + region;
            if (!string.IsNullOrWhiteSpace(stamp))
            {
                id += "_" + stamp;
            }

            return id;
        }

        /// <summary>
        /// The Create the Metrics meter Counter of type long.
        /// </summary>
        /// <param name="metricName">The metricName<see cref="string"/>.</param>
        /// <param name="unit">The unit<see cref="string"/>.</param>
        /// <param name="description">The description<see cref="string"/>.</param>
        /// <returns>The Counter{long}/>.</returns>
        public Counter<long> CreateMetricsMeterCounter(string metricName, string? unit = null, string? description = null)
        {
            return this.metricsCounterDictionary.GetOrAdd(metricName, this.MetricsMeter.CreateCounter<long>(metricName, unit, description));
        }

        /// <summary>
        /// The TrackCounterMetric.
        /// </summary>
        /// <param name="operationName">The operationName<see cref="string"/>.</param>
        /// <param name="count">The count<see cref="int"/>.</param>
        /// <param name="tags">The tags KeyValuePair{string, object?}[].</param>
        public void TrackCounterMetric(string operationName, int count, params KeyValuePair<string, object?>[] tags)
        {
            var counterMetric = this.metricsCounterDictionary.GetOrAdd(operationName, this.MetricsMeter.CreateCounter<long>(operationName, unit: "count", description: "Meter counter metric to track the hits/counts."));
            TagList tagList = this.GetTagList(operationName);

            foreach (var tag in tags)
            {
                tagList.Add(tag);
            }

            counterMetric.Add(count, tagList);
        }

        /// <summary>
        /// The RecordLatency.
        /// </summary>
        /// <param name="operationName">The operationName<see cref="string"/>.</param>
        /// <param name="elapsedTime">The elapsedTime<see cref="double"/>.</param>
        /// <param name="tags">The tags<see cref="KeyValuePair{string, object?}[]"/>.</param>
        public void RecordLatencySLI(string operationName, double elapsedTime, params KeyValuePair<string, object?>[] tags)
        {
            // Create a special latency histogram with custom buckets
            var latencyHistogram = this.latencyHistogramDictionary.GetOrAdd(operationName, _ =>
            {
                // Create histogram with custom bucket advice for Geneva percentiles
                var advice = new InstrumentAdvice<double>
                {
                    HistogramBucketBoundaries = Extensions.AddOpenTelemetryExtensions.GenerateHistogramBucket().OrderBy(boundary => boundary).ToList()
                };

               return this.MetricsMeter.CreateHistogram<double>(
                    operationName,
                    unit: "ms",
                    description: $"Latency histogram for {operationName}. Uses custom buckets  for accurate Geneva percentiles.",
                    advice: advice);
            });

            TagList tagList = this.GetTagList(operationName);

            foreach (var tag in tags)
            {
                tagList.Add(tag);
            }

            latencyHistogram.Record(elapsedTime, tagList);
        }

        /// <summary>
        /// The GetTagList.
        /// </summary>
        /// <param name="operationName">The operationName<see cref="string"/>.</param>
        /// <returns>The <see cref="TagList"/>.</returns>
        private TagList GetTagList(string operationName)
        {
            return new TagList
            {
                { "CustomerResourceId", this.customerResourceId },
                { "LocationId", this.locationId },
                { "OperationName", operationName },
            };
        }
    }
}

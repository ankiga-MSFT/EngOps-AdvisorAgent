// <copyright file="MetricsProvider.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

namespace InfraService.OpenTelemetryProvider
{
    using System.Collections.Generic;
    using InfraService.Utilities;

    /// <summary>
    /// Defines the <see cref="MetricsProvider" />.
    /// </summary>
    public class MetricsProvider : IMetricsProvider
    {
        /// <summary>
        /// Defines the openTelemetryLogProvider.
        /// </summary>
        private readonly OpenTelemetryLogProvider openTelemetryLogProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenTelemetryLogger"/> class.
        /// </summary>
        /// <param name="openTelemetryLogProvider">The openTelemetryLogProvider<see cref="OpenTelemetryLogProvider"/>.</param>
        public MetricsProvider(OpenTelemetryLogProvider openTelemetryLogProvider)
        {
            this.openTelemetryLogProvider = Requires.IsNotNull(openTelemetryLogProvider, nameof(openTelemetryLogProvider));
        }

        public void TrackCounterMetric(string operationName, int count, params KeyValuePair<string, object?>[] tags)
        {
            this.openTelemetryLogProvider.TrackCounterMetric(operationName, count, tags);
        }

        public void TrackAvailabilityMetric(string operationName, int count, Exception? exception, params KeyValuePair<string, object?>[] tags)
        {
            if (exception is null)
                TrackCounterMetric(operationName, count, tags);
            else
                TrackFailureCounterMetric(operationName, count, exception, tags);
        }

        /// <summary>
        /// The RecordLatency.
        /// </summary>
        /// <param name="operationName">The operationName<see cref="string"/>.</param>
        /// <param name="elapsedTime">The elapsedTime<see cref="long"/>.</param>
        /// <param name="tags">The tags<see cref="KeyValuePair{string, object?}[]"/>.</param>
        public void RecordLatencySLI(string operationName, double elapsedTime, params KeyValuePair<string, object?>[] tags)
        {
            this.openTelemetryLogProvider.RecordLatencySLI(operationName, elapsedTime, tags);
        }

        /// <summary>
        /// The TrackSuccessCounterMetric.
        /// </summary>
        /// <param name="operationName">The operationName<see cref="string"/>.</param>
        /// <param name="count">The count<see cref="int"/>.</param>
        /// <param name="tags">The tags KeyValuePair{string, object?}[].</param>
        public void TrackSuccessCounterMetric(string operationName, int count, params KeyValuePair<string, object?>[] tags)
        {
            var tagsList = tags.ToList();
            tagsList.Add(new KeyValuePair<string, object?>("Status", "Success"));
            this.TrackCounterMetric(operationName, count, tagsList.ToArray());
        }

        /// <summary>
        /// The TrackFailureCounterMetric.
        /// </summary>
        /// <param name="operationName">The operationName<see cref="string"/>.</param>
        /// <param name="count">The count<see cref="int"/>.</param>
        /// <param name="exception">The exception<see cref="Exception"/>.</param>
        /// <param name="tags">The tags KeyValuePair{string, object?}[].</param>
        public void TrackFailureCounterMetric(string operationName, int count, Exception? exception = null, params KeyValuePair<string, object?>[] tags)
        {
            var tagsList = tags.ToList();

            if(exception != null)
            {
                if (exception is AggregateException aggregateException)
                {
                    // Select the first inner exception that is not an AggregateException
                    exception = aggregateException.InnerExceptions.FirstOrDefault(innerEx => innerEx is not AggregateException) ?? exception;
                }

                tagsList.Add(new KeyValuePair<string, object?>("ExceptionType", exception.GetType().ToString()));
            }

            tagsList.Add(new KeyValuePair<string, object?>("Status", "Failure"));
            this.TrackCounterMetric(operationName, count, tagsList.ToArray());
        }

        /// <summary>
        /// Start the LatencyMeasure Operation, latency measures in units of seconds.
        /// </summary>
        /// <param name="operationName">The operationName<see cref="string"/>.</param>
        /// <param name="tags">The tags<see cref="KeyValuePair{string, object?}[]"/>.</param>
        /// <returns>The <see cref="OpenTelemetryProvider.LatencyMeasureOperation"/>.</returns>
        public LatencyMeasureOperation LatencyMeasureOperation(string operationName, params KeyValuePair<string, object?>[] tags) => new (this, operationName, tags);
    }
}

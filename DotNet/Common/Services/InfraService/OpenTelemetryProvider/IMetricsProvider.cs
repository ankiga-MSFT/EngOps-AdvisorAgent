namespace InfraService.OpenTelemetryProvider
{
    public interface IMetricsProvider
    {
        /// <summary>
        /// The TrackCounterMetric.
        /// </summary>
        /// <param name="operationName">The operationName<see cref="string"/>.</param>
        /// <param name="count">The count<see cref="int"/>.</param>
        /// <param name="tags">The tags KeyValuePair{string, object?}[].</param>
        public void TrackCounterMetric(string operationName, int count, params KeyValuePair<string, object?>[] tags);

        /// <summary>
        /// The TrackAvailabilityMetric.
        /// </summary>
        /// <param name="operationName">The operationName<see cref="string"/>.</param>
        /// <param name="count">The count<see cref="int"/>.</param>
        /// <param name="exception">The exception<see cref="Exception"/>.</param>
        /// <param name="tags">The tags KeyValuePair{string, object?}[].</param>
        public void TrackAvailabilityMetric(string operationName, int count, Exception? exception, params KeyValuePair<string, object?>[] tags);

        /// <summary>
        /// The RecordLatency.
        /// </summary>
        /// <param name="operationName">The operationName<see cref="string"/>.</param>
        /// <param name="elapsedTime">The elapsedTime<see cref="long"/>.</param>
        /// <param name="tags">The tags<see cref="KeyValuePair{string, object?}[]"/>.</param>
        public void RecordLatencySLI(string operationName, double elapsedTime, params KeyValuePair<string, object?>[] tags);

        /// <summary>
        /// The TrackSuccessCounterMetric.
        /// </summary>
        /// <param name="operationName">The operationName<see cref="string"/>.</param>
        /// <param name="count">The count<see cref="int"/>.</param>
        /// <param name="tags">The tags KeyValuePair{string, object?}[].</param>
        public void TrackSuccessCounterMetric(string operationName, int count, params KeyValuePair<string, object?>[] tags);

        /// <summary>
        /// The TrackFailureCounterMetric.
        /// </summary>
        /// <param name="operationName">The operationName<see cref="string"/>.</param>
        /// <param name="count">The count<see cref="int"/>.</param>
        /// <param name="exception">The exception<see cref="Exception"/>.</param>
        /// <param name="tags">The tags KeyValuePair{string, object?}[].</param>
        public void TrackFailureCounterMetric(string operationName, int count, Exception? exception, params KeyValuePair<string, object?>[] tags);

        /// <summary>
        /// Start the LatencyMeasure Operation.
        /// </summary>
        /// <param name="operationName">The operationName<see cref="string"/>.</param>
        /// <param name="tags">The tags<see cref="KeyValuePair{string, object?}[]"/>.</param>
        /// <returns>The <see cref="OpenTelemetryProvider.LatencyMeasureOperation"/>.</returns>
        public LatencyMeasureOperation LatencyMeasureOperation(string operationName, params KeyValuePair<string, object?>[] tags);
    }
}

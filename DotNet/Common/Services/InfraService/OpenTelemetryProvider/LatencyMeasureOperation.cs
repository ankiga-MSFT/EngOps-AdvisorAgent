// <copyright file="LatencyMeasureOperation.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

namespace InfraService.OpenTelemetryProvider
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Net;

    /// <summary>
    /// Defines the <see cref="LatencyMeasureOperation" />.
    /// </summary>
    public class LatencyMeasureOperation : IDisposable
    {
        /// <summary>
        /// Defines the genevaOpenTelemetryLogProvider.
        /// </summary>
        private readonly IMetricsProvider metricsProvider;

        /// <summary>
        /// Defines the operationName.
        /// </summary>
        private readonly string operationName;

        /// <summary>
        /// Defines the tags.
        /// </summary>
        private readonly KeyValuePair<string, object?>[] tags;

        /// <summary>
        /// Defines the stopWatch.
        /// </summary>
        private readonly Stopwatch stopWatch;

        /// <summary>
        /// Defines the disposedValue.
        /// </summary>
        private bool disposedValue;

        /// <summary>
        /// Defines the activityStatusCode.
        /// </summary>
        private ActivityStatusCode activityStatusCode = ActivityStatusCode.Unset;

        /// <summary>
        /// Defines the httpStatusCode.
        /// </summary>
        private int httpStatusCode;

        /// <summary>
        /// Initializes a new instance of the <see cref="LatencyMeasureOperation"/> class.
        /// </summary>
        /// <param name="metricsProvider">The genevaOpenTelemetryLogProvider<see cref="OpenTelemetryLogger"/>.</param>
        /// <param name="operationName">The operationName<see cref="string"/>.</param>
        /// <param name="tags">The tags KeyValuePair{string, object?}[]/>.</param>
        public LatencyMeasureOperation(IMetricsProvider metricsProvider, string operationName, params KeyValuePair<string, object?>[] tags)
        {
            this.metricsProvider = metricsProvider;
            this.operationName = operationName;

            if (tags is null)
            {
                this.tags = new List<KeyValuePair<string, object?>>().ToArray();
            }
            else
            {
                this.tags = tags;
            }

            this.stopWatch = new Stopwatch();
            this.stopWatch.Start();
        }

        /// <summary>
        /// Gets or sets a value indicating whether DoEmitMetrics.
        /// </summary>
        internal bool DoEmitMetrics { get; set; } = true;

        /// <summary>
        /// The SetState.
        /// </summary>
        /// <param name="activityStatusCode">The activityStatusCode<see cref="ActivityStatusCode"/>.</param>
        public void SetState(ActivityStatusCode activityStatusCode) => this.activityStatusCode = activityStatusCode;

        /// <summary>
        /// The SetState.
        /// </summary>
        /// <param name="httpStatusCode">The httpStatusCode<see cref="HttpStatusCode"/>.</param>
        public void SetState(HttpStatusCode httpStatusCode) => this.httpStatusCode = (int)httpStatusCode;

        /// <summary>
        /// The SetHttpStatusCode.
        /// </summary>
        /// <param name="httpStatusCode">The httpStatusCode<see cref="int"/>.</param>
        public void SetHttpStatusCode(int httpStatusCode) => this.httpStatusCode = httpStatusCode;

        /// <summary>
        /// The Dispose.
        /// </summary>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            this.Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// The Dispose.
        /// </summary>
        /// <param name="disposing">The disposing<see cref="bool"/>.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                if (disposing && this.DoEmitMetrics)
                {
                    this.stopWatch.Stop();
                    long elapsedTime = this.stopWatch.ElapsedMilliseconds;
                    var tags = this.tags?.ToList() ?? new List<KeyValuePair<string, object?>>();
                    tags.Add(new KeyValuePair<string, object?>("Status", this.activityStatusCode.ToString()));
                    if (this.httpStatusCode > 0)
                    {
                        tags.Add(new KeyValuePair<string, object?>("HttpStatusCode", (int)this.httpStatusCode));
                    }

                    this.metricsProvider.RecordLatencySLI(this.operationName, elapsedTime, tags.ToArray());
                }

                this.disposedValue = true;
            }
        }
    }
}

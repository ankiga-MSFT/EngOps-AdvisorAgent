// <copyright file="GenevaInstrumentation.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

namespace InfraService.OpenTelemetryProvider.Models
{
#pragma warning disable CS8618
    /// <summary>
    /// Defines the <see cref="GenevaInstrumentation" />.
    /// </summary>
    public class GenevaInstrumentation
    {
        /// <summary>
        /// Gets or sets the AccountName.
        /// </summary>
        public string AccountName { get; set; }

        /// <summary>
        /// Gets or sets the Namespace.
        /// </summary>
        public string Namespace { get; set; }

        /// <summary>
        /// Gets or sets the EtwSessionConnectionName.
        /// </summary>
        public string EtwSessionConnectionName { get; set; }

        /// <summary>
        /// Gets or sets the Region.
        /// </summary>
        public string Region { get; set; }

        /// <summary>
        /// Gets or sets the Tenant.
        /// </summary>
        public string Tenant { get; set; }

        /// <summary>
        /// Gets or sets the Cloud.
        /// </summary>
        public string Cloud { get; set; }

        /// <summary>
        /// Gets or sets the CounterMeterMetricName.
        /// </summary>
        public string CounterMeterMetricName { get; set; } = "CounterMeter";

        /// <summary>
        /// Gets or sets the LatencySliMetricName.
        /// </summary>
        public string LatencySliMetricName { get; set; } = "LatencySLI";

        /// <summary>
        /// Gets or sets the CustomFields.
        /// </summary>
        public List<string> CustomFields { get; set; } = new List<string>();
    }
}

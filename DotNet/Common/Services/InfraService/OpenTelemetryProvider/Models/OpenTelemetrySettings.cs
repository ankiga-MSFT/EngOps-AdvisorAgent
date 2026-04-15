// <copyright file="OpenTelemetrySettings.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

namespace InfraService.OpenTelemetryProvider.Models
{
    /// <summary>
    /// Defines the <see cref="OpenTelemetrySettings" />.
    /// </summary>
    #pragma warning disable CS8618
    public class OpenTelemetrySettings
    {
        /// <summary>
        /// Gets OpenTelemetrySettingsKeyName.
        /// </summary>
        public const string OpenTelemetrySettingsKeyName = nameof(OpenTelemetrySettings);

        /// <summary>
        /// Gets or sets the ServiceName.
        /// </summary>
        public string ServiceName { get; set; }

        ///// <summary>
        ///// Gets or sets the ServiceNameSpace.
        ///// </summary>
        public string ServiceNameSpace { get; set; }

        ///// <summary>
        ///// Gets or sets the SourceName.
        ///// </summary>
        public string SourceName { get; set; }

        /// <summary>
        /// Gets or sets the GenevaInstrumentation.
        /// </summary>
        public GenevaInstrumentation GenevaInstrumentation { get; set; }
    }
}

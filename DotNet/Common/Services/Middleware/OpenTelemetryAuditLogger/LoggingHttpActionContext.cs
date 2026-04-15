// <copyright file="LoggingHttpActionContext.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

namespace Middleware.OpenTelemetryAuditLogger
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The LoggingHttpActionContext class.
    /// </summary>
    public class LoggingHttpActionContext
    {
        /// <summary>
        /// Gets or sets the method.
        /// </summary>
        /// <value>
        /// The method.
        /// </value>
        public string? Method { get; set; }

        /// <summary>
        /// Gets or sets the request URI.
        /// </summary>
        /// <value>
        /// The request URI.
        /// </value>
        public Uri? RequestUri { get; set; }

        /// <summary>
        /// Gets or sets the headers.
        /// </summary>
        /// <value>
        /// The headers.
        /// </value>
        public IEnumerable<KeyValuePair<string, IEnumerable<string>>>? Headers { get; set; }
    }
}
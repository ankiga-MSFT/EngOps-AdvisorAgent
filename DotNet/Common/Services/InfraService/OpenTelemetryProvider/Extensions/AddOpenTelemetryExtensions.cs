// <copyright file="AddOpenTelemetryExtensions.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
namespace InfraService.OpenTelemetryProvider.Extensions
{
    using Azure.Monitor.OpenTelemetry.Exporter;
    using InfraService.OpenTelemetryProvider.Models;
    using InfraService.Utilities;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using OpenTelemetry;
    using OpenTelemetry.Exporter.Geneva;
    using OpenTelemetry.Logs;
    using OpenTelemetry.Metrics;
    using OpenTelemetry.Resources;
    using OpenTelemetry.Trace;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Defines the <see cref="AddOpenTelemetryExtensions" />.
    /// </summary>
    public static class AddOpenTelemetryExtensions
    {
        /// <summary>
        /// Defines the ProductName.
        /// </summary>
        private const string ProductName = "SupportDataPlatform";

        public static List<double> GenerateHistogramBucket()
        {
            var hundredMilliSeccondGapTill10Sec = new HashSet<double>() { 50, 100, 200, 400, 800, 1000, 2000, 3000, 4000, 5000, 6000, 7000, 8000, 9000, 10000 };
            var fiveSecGapTill1Minute = new HashSet<double>() { 15000, 20000, 25000, 30000, 35000, 40000, 45000, 50000, 60000 };
            var fiveMinuteGapTill3Hours = new HashSet<double>() { 90000, 120000, 150000, 180000, 210000, 240000, 270000, 300000, 330000, 360000, 390000, 420000, 450000, 480000, 510000, 540000, 570000, 600000, 630000, 660000, 690000, 720000, 750000, 780000, 810000, 840000, 870000, 900000, 930000, 960000, 1200000, 1500000, 1800000, 2100000, 2400000, 2700000, 3000000, 3300000, 3600000, 3900000, 4200000, 4500000, 4800000, 5100000, 5400000, 5700000, 6000000, 6300000, 6600000, 6900000, 7200000, 7500000, 7800000, 8100000, 8400000, 8700000, 9000000, 9300000, 9600000, 9900000, 10200000, 10500000, 10800000 };
            var fifteenMinuteGapTill12Hours = new HashSet<double>() { 11700000, 12600000, 13500000, 14400000, 15300000, 16200000, 17100000, 18000000, 18900000, 19800000, 20700000, 21600000, 22500000, 23400000, 24300000, 25200000, 26100000, 27000000, 27900000, 28800000, 29700000, 30600000, 31500000, 32400000, 33300000, 34200000, 35100000, 36000000, 36900000, 37800000, 38700000, 39600000, 40500000, 41400000, 42300000, 43200000 };
            var oneDayGapTill31Days = new HashSet<double>() { 172800000, 259200000, 345600000, 432000000, 518400000, 604800000, 691200000, 777600000, 864000000, 950400000, 1036800000, 1123200000, 1209600000, 1296000000, 1382400000, 1468800000, 1555200000, 1641600000, 1728000000, 1814400000, 1900800000, 1987200000, 2073600000, 2160000000, 2246400000, 2332800000, 2419200000, 2505600000, 2592000000, 2678400000 };
            var finalSet = new[] { hundredMilliSeccondGapTill10Sec, fiveSecGapTill1Minute, fiveMinuteGapTill3Hours, fifteenMinuteGapTill12Hours, oneDayGapTill31Days }.Aggregate((a, b) => new HashSet<double>(a.Union(b)));
            return finalSet.ToList();
        }

        /// <summary>
        /// The ConfigureLoggingTelemetry.
        /// </summary>
        /// <param name="services">The services<see cref="IServiceCollection"/>.</param>
        /// <param name="openTelemetrySettings">The openTelemetrySettings<see cref="OpenTelemetrySettings"/>.</param>
        /// <param name="assemblyVersion">The assemblyVersion<see cref="string"/>.</param>
        /// <param name="addConsoleExporter">The addConsoleExporter<see cref="bool"/>.</param>
        /// <returns>The <see cref="IServiceCollection"/>.</returns>
        public static IServiceCollection ConfigureLoggingTelemetry(this IServiceCollection services, OpenTelemetrySettings openTelemetrySettings, string assemblyVersion = "unknown", bool addConsoleExporter = false)
        {
            string serviceInstanceName = Environment.MachineName;

            // Listen to this events.
            var subscribedSourcesForTelemetry = new string[] { "*" };

            // Setting role attributes for this service.
            var resourceAttributes = new Dictionary<string, object>
                    {
                        { "service.name", openTelemetrySettings.ServiceName },
                        { "service.namespace", openTelemetrySettings.ServiceNameSpace },
                        { "geneva.metrics.account", openTelemetrySettings.GenevaInstrumentation.AccountName },
                        { "geneva.metrics.namespace", openTelemetrySettings.GenevaInstrumentation.Namespace },
                    };

            // Setting prepopulatedFields for this service.
            var prepopulatedFields = new Dictionary<string, object>
            {
                ["cloud.role"] = OpenTelemetryLogProvider.CreateLocationId(openTelemetrySettings.GenevaInstrumentation.Cloud, openTelemetrySettings.GenevaInstrumentation.Region),
                ["cloud.roleInstance"] = serviceInstanceName,
                ["cloud.region"] = openTelemetrySettings.GenevaInstrumentation.Region,
                ["cloud.tenant"] = openTelemetrySettings.GenevaInstrumentation.Tenant,
                ["cloud.type"] = openTelemetrySettings.GenevaInstrumentation.Cloud,
                ["service.SourceName"] = openTelemetrySettings.SourceName,
                ["AppName"] = Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME") ?? "NA",
                ["AppSlotName"] = Environment.GetEnvironmentVariable("WEBSITE_SLOT_NAME") ?? "NA",
                //["BuildVersion"] = Environment.GetEnvironmentVariable("BuildVersion") ?? assemblyVersion,
                ["ServiceName"] = openTelemetrySettings.ServiceName,
            };

            // By default, all the Microsoft Common Schema Part C fields will be stored as individual table columns.
            // CustomFields can be used to decide which fields gets stored as dedicated columns. When provided, any field that belongs to the specified list will be stored as
            // individual table column. Other fields will be converted to a JSON representation and stored in a column named env_properties.
            var customFields = new string[]
            {
                "OperationName",
                "FunctionName",
                "ClassName",
                "CaseNumber",
                "CustomerResourceId",
                "LocationId",
                "HttpStatusCode",
                "Status",
                "IncidentId",
                "StepName",
                "SkillName",
                "ResourceName",
                "IsSuccess",
                "Scope",
                "ExceptionType"
            };
            if (openTelemetrySettings.GenevaInstrumentation.CustomFields != null && openTelemetrySettings.GenevaInstrumentation.CustomFields.Count > 0)
            {
                // Union of the exported custom fields in the table.
                customFields = customFields.Union(openTelemetrySettings.GenevaInstrumentation.CustomFields).ToArray();
            }

            // Open Telemetry resource builder.
            var resourceBuilder = CreateOpenTelemetryResourceBuilder(
                        serviceName: openTelemetrySettings.ServiceName,
                        serviceVersion: assemblyVersion,
                        serviceInstanceId: serviceInstanceName,
                        resourceAttributes: resourceAttributes);

            // Open Telemetry Meter provider.
            AddOpenTelemetryMetricsProviderExtension(
                services,
                genevaAccountName: openTelemetrySettings.GenevaInstrumentation.AccountName,
                genevaAccountNameSpace: openTelemetrySettings.GenevaInstrumentation.Namespace,
                resourceBuilder: resourceBuilder,
                prepopulatedMetricDimensions: prepopulatedFields,
                meterNames: subscribedSourcesForTelemetry,
                addConsoleExporter: addConsoleExporter);

            // Open Telemetry logger provider.
            AddOpenTelemetryLoggerFactoryExtension(
                services,
                genevaETWSessionConnectionName: openTelemetrySettings.GenevaInstrumentation.EtwSessionConnectionName,
                resourceBuilder: resourceBuilder,
                prepopulatedFields: prepopulatedFields,
                customFields: customFields,
                addConsoleExporter: addConsoleExporter);

            // Open Telemetry trace provider.
            AddOpenTelemetryTraceProviderExtension(
                services,
                genevaETWSessionConnectionName: openTelemetrySettings.GenevaInstrumentation.EtwSessionConnectionName,
                resourceBuilder: resourceBuilder,
                prepopulatedFields: prepopulatedFields,
                sourcesSubscribedforTrace: subscribedSourcesForTelemetry,
                customFields: customFields,
                addConsoleExporter: addConsoleExporter);

            services.AddSingleton((sp) =>
            {
                var customerResourceId = OpenTelemetryLogProvider.CreateCustomerResourceId(ProductName, openTelemetrySettings.ServiceName);
                var locationId = OpenTelemetryLogProvider.CreateLocationId(openTelemetrySettings.GenevaInstrumentation.Cloud, openTelemetrySettings.GenevaInstrumentation.Region);
                return new OpenTelemetryLogProvider(openTelemetrySettings.SourceName, "1.0", customerResourceId : customerResourceId, locationId : locationId);
            });
            services.AddSingleton<IMetricsProvider>((sp) =>
            {
                var openTelemetryLogProvider = sp.GetRequiredService<OpenTelemetryLogProvider>();
                
                return new MetricsProvider(openTelemetryLogProvider);
            });

            return services;
        }

        /// <summary>
        /// The AddOpenTelemetryMetricsProviderExtension.
        /// </summary>
        /// <param name="serviceCollection">The serviceCollection<see cref="IServiceCollection"/>.</param>
        /// <param name="genevaAccountName">The genevaAccountName<see cref="string"/>.</param>
        /// <param name="genevaAccountNameSpace">The genevaAccountNameSpace<see cref="string"/>.</param>
        /// <param name="resourceBuilder">The resourceBuilder<see cref="ResourceBuilder"/>.</param>
        /// <param name="prepopulatedMetricDimensions">The prepopulatedMetricDimensions<see cref="Dictionary{Tkey, TValue}"/>.</param>
        /// <param name="addConsoleExporter">The addConsoleExporter<see cref="bool"/>.</param>
        /// <param name="meterNames">The meterNames<see cref="string"/>.</param>
        /// <returns>The <see cref="IServiceCollection"/>.</returns>
        public static IServiceCollection AddOpenTelemetryMetricsProviderExtension(this IServiceCollection serviceCollection, string genevaAccountName, string genevaAccountNameSpace, ResourceBuilder resourceBuilder, Dictionary<string, object>? prepopulatedMetricDimensions = null, bool addConsoleExporter = false, params string[] meterNames)
        {
            Requires.IsNotNull(genevaAccountName, nameof(genevaAccountName));
            Requires.IsNotNull(genevaAccountNameSpace, nameof(genevaAccountNameSpace));
            Requires.IsNotNull(resourceBuilder, nameof(resourceBuilder));
            Requires.IsNotNull(meterNames, nameof(meterNames));

            // Open Telemetry METRICS provider
            var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddAzureMonitorMetricExporter()
            .SetResourceBuilder(resourceBuilder)
            .AddMeter(meterNames)

            // Add the Geneva exporter
            .AddGenevaMetricExporter(options =>
            {
                // On Windows
                options.ConnectionString = $"Account={genevaAccountName};Namespace={genevaAccountNameSpace}";

                // On Linux
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    options.ConnectionString = $"Endpoint=unix:/var/etw/mdm_ifx.socket;Account={genevaAccountName};Namespace={genevaAccountNameSpace}";
                }

                if (prepopulatedMetricDimensions != null)
                {
                    options.PrepopulatedMetricDimensions = prepopulatedMetricDimensions;
                }
            })
            .AddHttpClientInstrumentation();

            if (addConsoleExporter)
            {
                // Add console exporter.
                meterProvider.AddConsoleExporter((exporterOptions, metricReaderOptions) =>

                // The ConsoleMetricExporter defaults to a manual collect cycle.
                // This configuration causes metrics to be exported to stdout on a 10s interval.
                metricReaderOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 10000);
            }

            meterProvider.Build();
            serviceCollection.AddSingleton(meterProvider);
            return serviceCollection;
        }

        /// <summary>
        /// The AddOpenTelemetryLoggerFactoryExtension.
        /// </summary>
        /// <param name="serviceCollection">The serviceCollection<see cref="IServiceCollection"/>.</param>
        /// <param name="genevaETWSessionConnectionName">The genevaETWSessionConnectionName<see cref="string"/>.</param>
        /// <param name="resourceBuilder">The resourceBuilder<see cref="ResourceBuilder"/>.</param>
        /// <param name="prepopulatedFields">The prepopulatedFields<see cref="Dictionary{TKey, TValue}"/>.</param>
        /// <param name="tableNameMappings">The tableNameMappings<see cref="Dictionary{Tkey, TValue}"/>.</param>
        /// <param name="customFields">The customFields<see cref="string"/>.</param>
        /// <param name="addConsoleExporter">The addConsoleExporter<see cref="bool"/>.</param>
        /// <returns>The <see cref="IServiceCollection"/>.</returns>
        public static IServiceCollection AddOpenTelemetryLoggerFactoryExtension(this IServiceCollection serviceCollection, string genevaETWSessionConnectionName, ResourceBuilder resourceBuilder, Dictionary<string, object>? prepopulatedFields = null, Dictionary<string, string>? tableNameMappings = null, string[]? customFields = null, bool addConsoleExporter = false)
        {
            Requires.IsNotNull(genevaETWSessionConnectionName, nameof(genevaETWSessionConnectionName));
            Requires.IsNotNull(resourceBuilder, nameof(resourceBuilder));

            // Create logger factory
            serviceCollection.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddOpenTelemetry(loggerOptions =>
                {
                    loggerOptions.IncludeScopes = true;
                    loggerOptions.ParseStateValues = true;
                    loggerOptions.IncludeFormattedMessage = true;
                    loggerOptions.SetResourceBuilder(resourceBuilder);

                    // Console exporter runs BEFORE PII redaction so debug console sees full content
                    if (addConsoleExporter || string.Equals(
                        Environment.GetEnvironmentVariable("CXOAI_DEBUG_LOGGING"), "true", StringComparison.OrdinalIgnoreCase))
                    {
                        loggerOptions.AddConsoleExporter();
                    }

                    // PII redaction processor - strips sensitive data before persistent exporters
                    loggerOptions.AddProcessor(new PiiRedactionLogProcessor());

                    // Azure Monitor exporter (sees redacted data)
                    loggerOptions.AddAzureMonitorLogExporter();

                    // Add the GenevaLogExporter and configure it (sees redacted data)
                    loggerOptions.AddGenevaLogExporter(exporterOptions =>
                    {
                        // On Windows
                        exporterOptions.ConnectionString = $"EtwSession={genevaETWSessionConnectionName};PrivatePreviewEnableAFDCorrelationIdEnrichment=true";
                        //exporterOptions.ConnectionString = $"EtwSession={genevaETWSessionConnectionName}";

                        // On Linux
                        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                        {
                            exporterOptions.ConnectionString = "Endpoint=unix:/var/run/mdsd/default_fluent.socket";
                        }

                        if (tableNameMappings != null)
                        {
                            exporterOptions.TableNameMappings = tableNameMappings;
                        }

                        if (prepopulatedFields != null)
                        {
                            exporterOptions.PrepopulatedFields = prepopulatedFields;
                        }

                        if (customFields != null)
                        {
                            exporterOptions.CustomFields = customFields;
                        }

                        exporterOptions.ExceptionStackExportMode = ExceptionStackExportMode.ExportAsString;
                    });
                });
            });

            return serviceCollection;
        }

        /// <summary>
        /// The AddOpenTelemetryTraceProviderExtension.
        /// </summary>
        /// <param name="serviceCollection">The serviceCollection<see cref="IServiceCollection"/>.</param>
        /// <param name="genevaETWSessionConnectionName">The genevaETWSessionConnectionName<see cref="string"/>.</param>
        /// <param name="resourceBuilder">The resourceBuilder<see cref="ResourceBuilder"/>.</param>
        /// <param name="prepopulatedFields">The prepopulatedFields<see cref="Dictionary{Tkey, TValue}"/>.</param>
        /// <param name="tableNameMappings">The tableNameMappings<see cref="Dictionary{Tkey, TValue}"/>.</param>
        /// <param name="customFields">The customFields<see cref="string"/>.</param>
        /// <param name="addConsoleExporter">The addConsoleExporter<see cref="bool"/>.</param>
        /// <param name="sourcesSubscribedforTrace">The sourcesSubscribedforTrace<see cref="string"/>.</param>
        /// <returns>The <see cref="IServiceCollection"/>.</returns>
        public static IServiceCollection AddOpenTelemetryTraceProviderExtension(this IServiceCollection serviceCollection, string genevaETWSessionConnectionName, ResourceBuilder resourceBuilder, Dictionary<string, object>? prepopulatedFields = null, Dictionary<string, string>? tableNameMappings = null, string[]? customFields = null, bool addConsoleExporter = false, params string[] sourcesSubscribedforTrace)
        {
            Requires.IsNotNull(genevaETWSessionConnectionName, nameof(genevaETWSessionConnectionName));
            Requires.IsNotNull(resourceBuilder, nameof(resourceBuilder));
            Requires.IsNotNull(sourcesSubscribedforTrace, nameof(sourcesSubscribedforTrace));

            // Setup Open telemetry Traces provider.
            var tracerProvider = Sdk.CreateTracerProviderBuilder()
                        .SetResourceBuilder(resourceBuilder)
                        .SetSampler(new AlwaysOnSampler())
                        .AddSource(sourcesSubscribedforTrace)
                        .AddGenevaTraceExporter(options =>
                        {
                            // On Windows
                            options.ConnectionString = $"EtwSession={genevaETWSessionConnectionName}";

                            // On Linux
                            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                            {
                                options.ConnectionString = "Endpoint=unix:/var/run/mdsd/default_fluent.socket";
                            }

                            if (tableNameMappings != null)
                            {
                                options.TableNameMappings = tableNameMappings;
                            }

                            if (prepopulatedFields != null)
                            {
                                options.PrepopulatedFields = prepopulatedFields;
                            }

                            if (customFields != null)
                            {
                                options.CustomFields = customFields;
                            }
                        })
                        .AddHttpClientInstrumentation();

            if (addConsoleExporter)
            {
                // Add console exporter.
                tracerProvider.AddConsoleExporter();
            }

            tracerProvider.Build();

            serviceCollection.AddSingleton(tracerProvider);
            return serviceCollection;
        }

        /// <summary>
        /// The CreateOpenTelemetryResourceBuilder.
        /// </summary>
        /// <param name="serviceName">The serviceName<see cref="string"/>.</param>
        /// <param name="serviceVersion">The serviceVersion<see cref="string"/>.</param>
        /// <param name="serviceInstanceId">The serviceInstanceId<see cref="string"/>.</param>
        /// <param name="resourceAttributes">The resourceAttributes<see cref="Dictionary{Tkey, TValue}"/>.</param>
        /// <returns>The <see cref="ResourceBuilder"/>.</returns>
        public static ResourceBuilder CreateOpenTelemetryResourceBuilder(string serviceName, string? serviceVersion = null, string? serviceInstanceId = null, Dictionary<string, object>? resourceAttributes = null)
        {
            Requires.IsNotNull(serviceName, nameof(serviceName));
            var serviceResourceBuilder = ResourceBuilder.CreateDefault().AddService(serviceName, serviceVersion: serviceVersion, serviceInstanceId: serviceInstanceId);

            if (resourceAttributes != null && resourceAttributes.Count > 0)
            {
                serviceResourceBuilder.AddAttributes(resourceAttributes);
            }

            return serviceResourceBuilder;
        }

        /// <summary>
        /// GetInfoVersion(T).
        /// </summary>
        /// <typeparam name="T">Generic Type.</typeparam>
        /// <returns>a string version.</returns>
        public static string GetInfoVersion<T>()
        {
            var assembly = typeof(T).Assembly;
            var attribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            return attribute != null ? attribute.InformationalVersion : "unknown";
        }
    }
}

// <copyright file="OpenTelemetryAuditLogger.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

namespace Middleware.OpenTelemetryAuditLogger
{
    using System;
    using Microsoft.Extensions.Logging;
    using OpenTelemetry.Audit.Geneva;
    using InfraService.Utilities;
    using InfraService.OpenTelemetryProvider.Extensions;

    /// <summary>
    /// The OpenTelemetryAuditLogger.
    /// </summary>
    public class OpenTelemetryAuditLogger
    {
        /// <summary>
        /// The audit factory logger.
        /// </summary>
        private static AuditLoggerFactory? auditFactory;

        /// <summary>
        /// The data plane logger.
        /// </summary>
        private static ILogger? dataPlaneLogger;

        /// <summary>
        /// The init method.
        /// </summary>
        /// <param name="serviceTreeId">The service tree id.</param>
        public static void Init(string serviceTreeId)
        {
            Action<AuditOptions> auditOptions = options =>
            {
                options.Destination = AuditLogDestination.ETW;
                options.ServiceId = new Guid(serviceTreeId);
                options.HeartbeatInterval = new TimeSpan(0, 30, 0);
            };

            auditFactory = AuditLoggerFactory.Create(auditOptions);
            dataPlaneLogger = auditFactory.CreateDataPlaneLogger();
        }

        /// <summary>
        /// Log Api response.
        /// </summary>
        /// <param name="operation">The operation name</param>
        /// <param name="operationType">The operationType.</param>
        /// <param name="operationalAccessLevel">The operationalAccessLevel.</param>
        /// <param name="isSuccessful">Flag denoting success of the operation.</param>
        /// <param name="clientIpAddress">The clientIpAddress.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="responseString">The responseString.</param>
        /// <param name="userIdentity">The userIdentity.</param>
        public static void LogApiResponse(string operation, OperationType operationType, string operationalAccessLevel, bool isSuccessful, string clientIpAddress, ILogger<OpenTelemetryAuditMiddleware> logger, string responseString, string userIdentity, System.Net.HttpStatusCode? httpStatusCode = null)
        {
            Requires.IsNotNull(logger, nameof(logger));
            Requires.IsNotNullOrWhitespace(operation, nameof(operation));
            logger.AppLogInformation($"OpenTelemetryAuditLogger - Calling Log api response for operation : {operation}.");

            try
            {
                AuditRecord auditRecord = new AuditRecord();
                auditRecord.OperationName = operation;
                auditRecord.AddOperationCategory(OperationCategory.CustomerFacing); // Please refer https://1dsdocs.azurewebsites.net/schema/PartB/logs/Audit.html#operationcategory-enumeration
                auditRecord.OperationResult = isSuccessful ? OperationResult.Success : OperationResult.Failure;
                if (auditRecord.OperationResult == OperationResult.Failure)
                {
                    auditRecord.OperationResultDescription = "Failure Audit Log.";
                    
                    // Add HTTP status code to custom data for failure metrics
                    if (httpStatusCode.HasValue)
                    {
                        auditRecord.AddCustomData("HttpStatusCode", httpStatusCode.Value.ToString());
                    }
                }

                auditRecord.AddCallerIdentity(OpenTelemetry.Audit.Geneva.CallerIdentityType.Other, @"DOMAIN\" + userIdentity, "It could be any MS employee alias or any service application id.");
                auditRecord.AddTargetResource("response", responseString);
                auditRecord.OperationAccessLevel = operationalAccessLevel;
                auditRecord.OperationType = operationType;
                auditRecord.CallerIpAddress = clientIpAddress;
                auditRecord.AddCallerAccessLevels(new[] { "Read", "ReadWrite" });
                auditRecord.CallerAgent = operation;
                try
                {
                    string localAddress = Environment.GetEnvironmentVariable("LOCAL_ADDR") ?? string.Empty;
                    if (!string.IsNullOrEmpty(localAddress))
                    {
                        auditRecord.AddCustomData("LocalAddress", localAddress);
                    }

                    string websiteInfraIp = Environment.GetEnvironmentVariable("WEBSITE_INFRASTRUCTURE_IP") ?? string.Empty;
                    if (!string.IsNullOrEmpty(websiteInfraIp))
                    {
                        auditRecord.AddCustomData("WebsiteInfraIp", websiteInfraIp);
                    }
                }
                catch (Exception ex)
                {
                    logger.AppLogError(ex, "Error occured while getting the LocalAddress/WebsiteInfraIp from environment variables.");
                }

                if (dataPlaneLogger != null) {
                    dataPlaneLogger.LogAudit(auditRecord);
                    logger.AppLogInformation($"OpenTelemetryAuditLogger - Successfully Logged audit record for operation : {operation}.");
                } else {
                    logger.AppLogError("DataPlaneLogger is not initialized. Ensure Init method is called before logging.");
                }
            }
            catch (Exception ex)
            {
                if (logger != null)
                {
                    logger.AppLogError(ex, $"Failed to create audit record: {ex.Message}");
                }
            }
        }
    }
}
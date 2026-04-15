// <copyright file="OpenTelemetryAuditMiddleware.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

namespace Middleware.OpenTelemetryAuditLogger
{
    using InfraService.OpenTelemetryProvider.Extensions;
    using InfraService.Utilities;
    using Microsoft.Azure.Functions.Worker;
    using Microsoft.Azure.Functions.Worker.Http;
    using Microsoft.Azure.Functions.Worker.Middleware;
    using Microsoft.Extensions.Logging;
    using Middleware.Extension;
    using Newtonsoft.Json;
    using OpenTelemetry.Audit.Geneva;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// The OpenTelemetryAuditMiddleware class.
    /// </summary>
    public class OpenTelemetryAuditMiddleware : IFunctionsWorkerMiddleware
    {
        /// <summary>
        /// Authorization header.
        /// </summary>
        public const string AuthorizationHeader = "Authorization";

        /// <summary>
        /// Cookies header.
        /// </summary>
        public const string CookiesHeader = "Cookies";

        /// <summary>
        /// Cookie header.
        /// </summary>
        public const string CookieHeader = "Cookie";

        /// <summary>
        /// X-Azure-Ref header.
        /// A unique reference string that identifies a request served by Front Door
        /// </summary>
        public const string XAzureRefHeader = "X-Azure-Ref";

        /// <summary>
        /// X-ARR-SSL header.
        /// provides information about the TLS server certificate that was used to secure
        /// the TCP connection between the client (i.e. browser) and the ARR frontendoor
        /// </summary>
        public const string XARRSSLHeader = "X-ARR-SSL";

        /// <summary>
        /// X-Forwarded-For header.
        /// de-facto standard header for identifying the originating IP address of a client 
        /// connecting to a web server through a proxy server.
        /// </summary>
        public const string XForwardedFor = "x-forwarded-for";

        /// <summary>
        /// X-Azure-ClientIP header.
        /// de-facto standard header for identifying the originating IP address of a client 
        /// connecting to a web server through a proxy server.
        /// </summary>
        public const string XAzureClientIp = "x-azure-clientip";

        public const string XClientIP = "CLIENT-IP";

        /// <summary>
        /// Traceparent header.
        /// The traceparent HTTP header field identifies the incoming request in a tracing system
        /// </summary>
        public const string TraceparentHeader = "traceparent";

        /// <summary>
        /// Access-Token header.
        /// </summary>
        public const string AccessTokenHeader = "Access-Token";

        /// <summary>
        /// Defines the CallerAppId.
        /// </summary>
        public const string CallerAppId = "CallerAppId";

        /// <summary>
        /// Defines the name.
        /// </summary>
        public const string Name = "name";

        /// <summary>
        /// Defines the websiteInfraIp.
        /// </summary>
        public const string WebsiteInfraIp = "websiteInfraIp";

        /// <summary>
        /// Defines the localAddress.
        /// </summary>
        public const string LocalAddress = "localAddress";

        /// <summary>
        /// Defines the FunctionMiddlewareName.
        /// </summary>
        private const string FunctionMiddlewareName = nameof(OpenTelemetryAuditMiddleware);

        /// <summary>
        /// The max number of characters permitted in Geneva logs is 32767, we're limiting response string to 10000 as Open Telemetry Audit library adds more content and hence we don't get entire 32767 to ourselves.
        /// </summary>
        private const int MaxLogsSize = 10000;

        /// <summary>
        /// Defines the logger.
        /// </summary>
        private readonly ILogger logger;


        /// <summary>
        /// Initializes a new instance of the <see cref="OpenTelemetryAuditMiddleware"/> class.
        /// </summary>
        /// <param name="loggerFactory">The loggerFactory<see cref="ILoggerFactory"/>.</param>
        /// <param name="tokenValidationConfiguration"></param>
        /// 
        public OpenTelemetryAuditMiddleware(ILogger<OpenTelemetryAuditMiddleware> openTelemetryLogger)
        {
            Requires.IsNotNull(openTelemetryLogger, nameof(openTelemetryLogger));
            this.logger = openTelemetryLogger;
        }

        /// <summary>
        /// The Invoke.
        /// </summary>
        /// <param name="context">The context<see cref="FunctionContext"/>.</param>
        /// <param name="next">The next<see cref="FunctionExecutionDelegate"/>.</param>
        /// <returns>The <see cref="Task"/>.</returns>
        public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
        {
            Requires.IsNotNull(context, nameof(context));
            Requires.IsNotNull(next, nameof(next));
            var funcName = context.FunctionDefinition.Name;
            this.logger.AppLogInformation($"Invoked Function: {funcName}, Executing Middleware pipeline: {FunctionMiddlewareName}.");

            if (context.IsHttpTrigger())
            {
                await this.OnActionExecuting(context);
                await next(context);
                await this.OnActionExecuted(context);    
            }
            else
            {
                await next(context);
            }
            
        }

        private async Task OnActionExecuting(FunctionContext context)
        {
            this.logger.AppLogInformation($"On OnActionExecuting {context.FunctionDefinition?.EntryPoint}");
            await this.LogFunctionAtBeginning(context);
        }

        private async Task OnActionExecuted(FunctionContext context)
        {
            this.logger.AppLogInformation($"On OnActionExecuted {context.FunctionDefinition?.EntryPoint}");
            await this.LogFunctionAtEnd(context);
        }

        /// <summary>
        /// Logs the function at end.
        /// </summary>
        /// <param name="context">The action executed context.</param>
        private async Task LogFunctionAtEnd(FunctionContext context)
        {
            var functionEntryPointFull = context.FunctionDefinition?.EntryPoint;
            var functionEntryPoint = functionEntryPointFull?.Substring(0, functionEntryPointFull.LastIndexOf('.')) ?? string.Empty;
            var callerMethodName = context.FunctionDefinition?.Name;
            var httpRequest = await context.GetHttpRequestDataAsync();
            string returnValue, message, operationAccessLevel = string.Empty;
            string identity = string.Empty;
            OperationType operationType = OperationType.Read;
            var ipAddress = "127.0.0.1"; // keeping this IP address as default otherwise in case when IP address is not detected, the LogAudit method throws validation exception for IP address and Audit log is not created.

            try
            {
                ipAddress = this.GetCallerIpAddress(httpRequest);
                this.FindOperationTypeAndAccessLevel(httpRequest.Method, out operationType, out operationAccessLevel);
                returnValue = this.GetApiResponseData(context);

                if (returnValue.Length < MaxLogsSize)
                {
                    message = $"Complete: Full result calling method {callerMethodName} was: {returnValue}. FunctionEntryPoint: {functionEntryPoint}, CallerMethodName: {callerMethodName}";
                }
                else
                {
                    returnValue = returnValue.Substring(0, MaxLogsSize);
                    message = $"Complete: Partial result calling method {callerMethodName} was: {returnValue}. FunctionEntryPoint: {functionEntryPoint}, CallerMethodName: {callerMethodName}";
                }

                identity = this.GetCallerIdentity(context);
                this.logger.AppLogInformation(message);
                this.logger.AppLogInformation($"ClientIp: {ipAddress}, operationType: {operationType}, operationAccessLevel: {operationAccessLevel}, identity: {identity}");

                OpenTelemetryAuditLogger.LogApiResponse(
                operation: callerMethodName,
                operationType: operationType,
                operationalAccessLevel: operationAccessLevel,
                isSuccessful: true,
                clientIpAddress: ipAddress,
                logger: (ILogger<OpenTelemetryAuditMiddleware>) this.logger,
                responseString: returnValue,
                userIdentity: identity);
            }
            catch (Exception exception)
            {
                this.logger.AppLogError(exception, $"Exception Thrown: Calling method {callerMethodName} when serializing response");
                // Get HTTP status code if available from the response
                var httpStatusCode = context.GetHttpResponseData()?.StatusCode;

                OpenTelemetryAuditLogger.LogApiResponse(
                    callerMethodName,
                    operationType,
                    operationAccessLevel,
                    false,
                    ipAddress,
                    logger: (ILogger<OpenTelemetryAuditMiddleware>)this.logger,
                    responseString: exception.ToString(),
                    identity,
                    httpStatusCode);
            }
        }

        /// <summary>
        /// Gets the caller identity.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns>The caller identity.</returns>
        private string GetCallerIdentity(FunctionContext context)
        {
            if (context.Items.ContainsKey(Name) && context.Items[Name] is not null)
            {
                return context.Items[Name] as string ?? string.Empty;
            }
            else if (context.Items.ContainsKey(CallerAppId) && context.Items[CallerAppId] is not null)
            {
                return context.Items[CallerAppId] as string ?? string.Empty;
            }

            return "CallerIdentityNotFound";
        }

        /// <summary>
        /// Gets the API response data.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns>The API response.</returns>
        private string GetApiResponseData(FunctionContext context)
        {
            string returnValue = string.Empty;
            var responseData = context.GetHttpResponseData();
            if (responseData?.Body != null && responseData.Body.CanRead)
            {
                responseData.Body.Seek(0, SeekOrigin.Begin);
                using (StreamReader reader = new StreamReader(responseData.Body, Encoding.UTF8, leaveOpen: true))
                {
                    returnValue = reader.ReadToEnd();
                }
                return returnValue;
            }
            else
            {
                this.logger.AppLogWarning("HttpResponseData is not readable.");
                return "HttpResponseData is not readable.";
            }
        }

        /// <summary>
        /// Gets the ip address of the caller.
        /// </summary>
        /// <param name="httpRequest">The httpRequest data.</param>
        /// <returns>The caller ip address.</returns>
        private string GetCallerIpAddress(HttpRequestData httpRequest)
        {
            // Reference https://stackoverflow.com/questions/72740004/how-to-get-client-ip-address-in-dotnet-isolated-azure-functions
            var headerDict = httpRequest?.Headers?.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase) ?? new Dictionary<string, IEnumerable<string>>();
            var ipAddress = "127.0.0.1"; // keeping this IP address as default otherwise in case when IP address is not detected, the LogAudit method throws validation exception for IP address and Audit log is not created.
            if (headerDict.ContainsKey(XForwardedFor))
            {
                var headerValues = headerDict[XForwardedFor];
                var ipn = headerValues?.FirstOrDefault()?.Split(new char[] { ',' })?.FirstOrDefault()?.Split(new char[] { ':' })?.FirstOrDefault();
                if (IPAddress.TryParse(ipn, out IPAddress ipAdd))
                {
                    ipAddress = ipAdd.ToString();
                    this.logger.AppLogInformation($"IpAddress: {ipAddress} retrieved from {XForwardedFor} header.");
                }
            }
            else if (headerDict.ContainsKey(XAzureClientIp))
            {
                var headerValues = headerDict[XAzureClientIp];
                var ipn = headerValues?.FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(ipn) && IPAddress.TryParse(ipn, out IPAddress ipAdd))
                {
                    ipAddress = ipAdd.ToString();
                    this.logger.AppLogInformation($"IpAddress: {ipAddress} retrieved from {XAzureClientIp} header.");
                }
            }
            else if (headerDict.ContainsKey(XClientIP)) {
                var headerValues = headerDict[XClientIP];
                var ipn = headerValues?.FirstOrDefault()?.Split(new char[] { ':' })?.FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(ipn) && IPAddress.TryParse(ipn, out IPAddress ipAdd))
                {
                    ipAddress = ipAdd.ToString();
                    this.logger.AppLogInformation($"IpAddress: {ipAddress} retrieved from {XClientIP} header.");
                }
            } 
            return ipAddress;
        }

        /// <summary>
        /// Logs the function at beginning.
        /// </summary>
        private async Task LogFunctionAtBeginning(FunctionContext context)
        {
            try
            {
                var callerMethodName = context.FunctionDefinition?.Name;
                var httpRequest = await context.GetHttpRequestDataAsync();
                List<string> excludedHeaders = new List<string> { AuthorizationHeader, CookiesHeader, CookieHeader, XAzureRefHeader, XARRSSLHeader, TraceparentHeader, AccessTokenHeader };

                var headers = httpRequest.Headers
                    .Where(header => !excludedHeaders.Contains(header.Key))
                    .ToDictionary(header => header.Key, header => header.Value);

                LoggingHttpActionContext obj = new LoggingHttpActionContext
                {
                    Headers = headers,
                    Method = httpRequest.Method,
                    RequestUri = httpRequest.Url,
                };

                var bindingData = context?.BindingContext?.BindingData != null 
                    ? new Dictionary<string, object>(context.BindingContext.BindingData) 
                    : new Dictionary<string, object>();
                if (bindingData.Any())
                {
                    bindingData.Remove("Headers");
                }

                this.logger.AppLogInformation(
                    $"Begin: Calling method {callerMethodName} with serialized headers: {JsonConvert.SerializeObject(obj, Formatting.Indented)} and Json Request: {JsonConvert.SerializeObject(bindingData, Formatting.Indented)}");
            }
            catch (Exception ex)
            {
                this.logger.AppLogError(ex, $"Failure occured at {nameof(this.LogFunctionAtBeginning)} in {nameof(OpenTelemetryAuditMiddleware)}.");
            }

        }

        /// <summary>
        /// The FindOperationTypeAndAccessLevel.
        /// </summary>
        /// <param name="httpMethodType">The httpMethodType.</param>
        /// <param name="operationType">The operationType.</param>
        /// <param name="accessLevel">The accessLevel.</param>
        private void FindOperationTypeAndAccessLevel(string httpMethodType, out OperationType operationType, out string accessLevel)
        {
            // Refer to this link to understand more about access levels https://1dsdocs.azurewebsites.net/schema/PartB/logs/Audit.html#operationaccesslevel
            switch (httpMethodType)
            {
                case "PUT":
                    operationType = OperationType.Create;
                    accessLevel = "ReadWrite";
                    break;

                case "POST":
                case "PATCH":
                    operationType = OperationType.Update;
                    accessLevel = "Read"; // The minimum access level (role) required for the operation to be performed.
                    break;

                case "GET":
                    operationType = OperationType.Read;
                    accessLevel = "Read";
                    break;

                case "DELETE":
                    operationType = OperationType.Delete;
                    accessLevel = "ReadWrite";
                    break;

                default:
                    operationType = OperationType.Read;
                    accessLevel = "Read";
                    break;
            }
        }
    }
}

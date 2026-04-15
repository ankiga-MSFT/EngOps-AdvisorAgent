// <copyright file="AuthenticationMiddleware.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using InfraService.OpenTelemetryProvider;
using InfraService.OpenTelemetryProvider.Extensions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.ServiceEssentials;
using Microsoft.IdentityModel.S2S.Configuration;
using Middleware.Auth.Configuration;
using Middleware.Auth.Model;
using Middleware.Extension;
using Newtonsoft.Json;
using System.Security.Claims;
using System.Text;

namespace Middleware.Auth
{
    /// <summary>
    /// Defines the <see cref="Auth" />.
    /// </summary>
    public class AuthenticationMiddleware : IFunctionsWorkerMiddleware
    {

        /// <summary>
        /// Gets or sets Logger object.
        /// </summary>
        private readonly ILogger<AuthenticationMiddleware> logger;

        /// <summary>
        /// Gets or sets TokenValidationConfiguration object.
        /// </summary>
        private ITokenValidationConfiguration TokenConfiguration { get; set; }

        private MiseHost<MiseHttpContext> MistHost { get; set; } = null!;

        /// <summary>
        /// Authentication Middleware Constructor.
        /// </summary>
        /// <param name="openTelemetryLogger"></param>
        /// <param name="tokenValidationConfiguration"></param>
        /// 
        public AuthenticationMiddleware(
            ILogger<AuthenticationMiddleware> openTelemetryLogger,
            ITokenValidationConfiguration tokenValidationConfiguration)
        {
            this.logger = openTelemetryLogger;
            this.TokenConfiguration = tokenValidationConfiguration;
            this.MistHost = this.MiseHostInit();
        }
        private MiseHost<MiseHttpContext> MiseHostInit()
        {
            var s2sAuthenticationManager = S2SAuthenticationManagerFactory.Default.BuildS2SAuthenticationManager(this.TokenConfiguration.AadAuthenticationOptions);

            var miseHost = MiseBuilder.Create(new ApplicationInformationContainer(this.TokenConfiguration.AadAuthenticationOptions.ClientId))
              .WithDefaultAuthentication(s2sAuthenticationManager)
              .ConfigureDefaultModuleCollection(builder =>
              {
                  builder.AddTrV2Module();
              })
              .WithLogger(this.logger)
            .Build();

            return miseHost;
        }

        public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
        {
            var healthCheckFunctionNames = this.TokenConfiguration.HealthCheckFunctionNames?.Split(',').Select(name => name.Trim()).ToArray();
            var httpRequest = await context.GetHttpRequestDataAsync().ConfigureAwait(false);
            var afdcorid = OpenTelemetryContext.SetAFDCorrelationIdFromRequest(httpRequest!);
            this.logger.AppLogInformation($"SetAFDCorrelationIdFromRequest invoked. AFDCorrelationId: {afdcorid}");

            if (healthCheckFunctionNames == null || !context.IsHealthCheckHttpTrigger(healthCheckFunctionNames))
            {
                ApiResponse<string> response = new ApiResponse<string>();
                if (context.IsHttpTrigger())
                {
                    var requestHeaders = httpRequest!.Headers.ToDictionary();

                    if (requestHeaders.TryGetValue("Authorization", out var authorizationHeader))
                    {
                        string authorizationHeaderContent = authorizationHeader.FirstOrDefault()!;

                        // Obtain http request data from your stack
                        var httpRequestData = new Microsoft.Identity.ServiceEssentials.HttpRequestData();
                        httpRequestData.Headers.Add("Authorization", authorizationHeaderContent);

                        var miseContext = new MiseHttpContext(httpRequestData);
                        var miseResult = await this.MistHost.HandleAsync(miseContext, default).ConfigureAwait(false);

                        /*** 3. examine results (for each request) ***/
                        if (miseResult.Succeeded)
                        {
                            // need to have some logic on this.
                            // return success http response
                            ClaimsIdentity appIdentity = miseResult.AuthenticationTicket.ActorIdentity ?? miseResult.AuthenticationTicket.SubjectIdentity;
                            this.logger.AppLogInformation("Authentication succeeded");
                            if (miseResult.AuthenticationTicket.SubjectIdentity != null)
                            {
                                var subjectIdentity = miseResult.AuthenticationTicket.SubjectIdentity;
                                // Do not want to log PI information incase someone manually trying to authenticate. This is not valid for S2S autehtnication purpose.
                                var claimsDictionary = subjectIdentity.Claims
                                    .Where(claim => claim.Type != "preferred_username" && claim.Type != "name")
                                    .ToDictionary(claim => claim.Type, claim => claim.Value);

                                var claimsJson = JsonConvert.SerializeObject(claimsDictionary, Formatting.Indented);
                                this.logger.AppLogInformation("Claims: {ClaimsJson}", claimsJson);

                                var claimsPrincipal = new ClaimsPrincipal(miseResult.AuthenticationTicket.SubjectIdentity);
                                this.AddS2SAppIdToContext(context, claimsPrincipal);
                            }
                        }
                        else
                        {
                            // return unauthorized http response
                            this.logger.LogError("Authentication failed");
                            this.logger.AppLogInformation($"Request validation failed.");

                            /*** 3.2 examine failure, and/or http response produced by a module that failed to handle the request ***/
                            this.logger.AppLogInformation($"Exception: {miseResult.Failure}");

                            if(miseResult.Failure?.InnerException != null)
                            {
                                this.logger.AppLogError(miseResult.Failure.InnerException, $"Mise Failure InnerException: {miseResult.Failure.InnerException.Message}");
                            }
                            var moduleCreatedFailureResponse = miseResult.MiseContext.ModuleFailureResponse;
                            if (moduleCreatedFailureResponse != null)
                            {
                                this.logger.AppLogInformation($"HTTP status code: {moduleCreatedFailureResponse.StatusCode}");

                                foreach (var header in moduleCreatedFailureResponse.Headers)
                                {
                                    this.logger.AppLogInformation($"Header - key:{header.Key} value:{string.Join(',', header.Value)}");
                                }

                                if (moduleCreatedFailureResponse.Body != null)
                                {
                                    this.logger.AppLogInformation($"HTTP Body: {Encoding.UTF8.GetString(moduleCreatedFailureResponse.Body, 0, moduleCreatedFailureResponse.Body.Length)}");
                                }
                            }
                            var httpResponse = httpRequest.CreateResponse(System.Net.HttpStatusCode.Unauthorized);
                            response.Message = "Unauthorized: Invalid Token.";
                            response.HttpStatusCode = System.Net.HttpStatusCode.Unauthorized;
                            await httpResponse.WriteAsJsonAsync(response).ConfigureAwait(false);
                            return; // Stop further processing

                        }

                    }
                    else
                    {
                        this.logger.LogError("Authorization header not found");
                        var httpResponse = httpRequest.CreateResponse(System.Net.HttpStatusCode.Unauthorized);
                        response.Message = "Unauthorized: Invalid Token.";
                        response.HttpStatusCode = System.Net.HttpStatusCode.Unauthorized;
                        await httpResponse.WriteAsJsonAsync(response).ConfigureAwait(false);
                        return; // Stop further processing  
                    }

                }
            }
            else
            {
                this.logger.AppLogInformation("The function is in exclusion list {healthCheckFunctionNames} :: skipping the Authentication", string.Join(", ", healthCheckFunctionNames));
            }
            await next(context).ConfigureAwait(false);
        }

        /// <summary>
        /// Adds S2S caller appid to the functions context.
        /// </summary>
        /// <param name="context">context.</param>
        /// <param name="principal">principal.</param>
        private void AddS2SAppIdToContext(FunctionContext context, ClaimsPrincipal principal)
        {
            // Add Claims AppId to the functions context if its S2S call, for S2S AllowedCallers check.
            string? oid = principal.FindFirst(x => x.Type == Constants.ObjectClaimTypeV2 || x.Type == Constants.ObjectClaimType)?.Value; // ObjectId
            string? sub = principal.FindFirst(x => x.Type == Constants.SubClaimTypeV2 || x.Type == ClaimTypes.NameIdentifier)?.Value; // Subject (whom the token refers to)
            string name = principal.FindFirst(Constants.Name)?.Value ?? string.Empty; // User name, if its a user delegated token.

            // To determine if a token is an App token, we use a combination of checking that the claims:
            // "oid" and "sub" both exist and are the same, as well as checking that the token does not contain a name claim to validate its not user delegated token.
            // Condition matches --> It's S2S token.
            if (oid?.Equals(sub, StringComparison.OrdinalIgnoreCase) == true && string.IsNullOrWhiteSpace(name))
            {
                if (principal.HasClaim(claim => claim.Type == Constants.Version))
                {
                    var version = principal.FindFirst(Constants.Version)?.Value ?? string.Empty;
                    string appId = string.Empty;

                    if (string.IsNullOrWhiteSpace(version) || version == "1.0")
                    {
                        appId = principal.FindFirst(Constants.Appid)?.Value ?? string.Empty;
                    }
                    else if (version == "2.0")
                    {
                        appId = principal.FindFirst(Constants.Appazp)?.Value ?? string.Empty;
                    }

                    context.Items.Add(Constants.CallerAppId, appId);
                }
            }
            else
            {
                // This is user delegated token, fetching user information.
                this.AddUserIdentityClaimsToContext(context, principal);
            }
        }

        /// <summary>
        /// The AddUserIdentityClaimsToContext.
        /// </summary>
        /// <param name="context">The context<see cref="FunctionContext"/>.</param>
        /// <param name="principal">The principal<see cref="ClaimsPrincipal"/>.</param>
        private void AddUserIdentityClaimsToContext(FunctionContext context, ClaimsPrincipal principal)
        {
            var name = principal.FindFirst(Constants.Name)?.Value ?? string.Empty;
            context.Items.Add(Constants.Name, name);
        }
    }
}

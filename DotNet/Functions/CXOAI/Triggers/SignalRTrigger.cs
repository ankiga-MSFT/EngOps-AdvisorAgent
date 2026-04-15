using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace CXOAI.Functions.Triggers;

/// <summary>
/// HTTP triggers for SignalR client connection negotiation.
/// </summary>
public class SignalRTrigger
{
    private readonly ServiceHubContext? _hubContext;
    private readonly ILogger<SignalRTrigger> _logger;

    public SignalRTrigger(ILogger<SignalRTrigger> logger, ServiceHubContext? hubContext = null)
    {
        _logger = logger;
        _hubContext = hubContext;
    }

    /// <summary>
    /// POST /api/negotiate?sessionId={sessionId}
    /// Returns SignalR connection info for the client. Adds the client to the session group.
    /// </summary>
    [Function("Negotiate")]
    public async Task<HttpResponseData> Negotiate(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "negotiate")] HttpRequestData req)
    {
        if (_hubContext is null)
        {
            var err = req.CreateResponse(HttpStatusCode.ServiceUnavailable);
            await err.WriteStringAsync("SignalR is not configured.");
            return err;
        }

        var sessionId = req.Query["sessionId"];
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
            await badReq.WriteStringAsync("Query parameter 'sessionId' is required.");
            return badReq;
        }

        var negotiateResponse = await _hubContext.NegotiateAsync(new NegotiationOptions
        {
            UserId = sessionId
        });

        // Add the client to the session group so status updates target only this session
        await _hubContext.UserGroups.AddToGroupAsync(sessionId, sessionId);

        _logger.LogInformation("SignalR negotiated for session '{SessionId}'", sessionId);

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(JsonSerializer.Serialize(new
        {
            url = negotiateResponse.Url,
            accessToken = negotiateResponse.AccessToken
        }));

        return response;
    }
}

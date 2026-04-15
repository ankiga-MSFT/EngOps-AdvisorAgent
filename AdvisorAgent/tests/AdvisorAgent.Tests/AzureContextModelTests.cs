using AdvisorAgent.Core.Models;
using Xunit;

namespace AdvisorAgent.Tests;

public class AzureContextModelTests
{
    [Fact]
    public void HasScope_ReturnsFalse_WhenEmpty()
    {
        var ctx = new AzureContext();
        Assert.False(ctx.HasScope);
    }

    [Fact]
    public void HasScope_ReturnsTrue_WhenSubscriptionSet()
    {
        var ctx = new AzureContext { SubscriptionId = "abc-123" };
        Assert.True(ctx.HasScope);
    }

    [Fact]
    public void ToContextSummary_ReturnsNone_WhenEmpty()
    {
        var ctx = new AzureContext();
        var summary = ctx.ToContextSummary();
        Assert.Contains("No Azure scope", summary);
    }

    [Fact]
    public void ToContextSummary_IncludesAllFields()
    {
        var ctx = new AzureContext
        {
            SubscriptionId = "sub-1",
            ResourceGroup = "rg-prod",
            ResourceName = "my-vm",
            Region = "eastus"
        };

        var summary = ctx.ToContextSummary();
        Assert.Contains("sub-1", summary);
        Assert.Contains("rg-prod", summary);
        Assert.Contains("my-vm", summary);
        Assert.Contains("eastus", summary);
    }

    [Fact]
    public void AdvisorAgentResponse_SuccessFactory()
    {
        var resp = AdvisorAgentResponse.Success("All good");
        Assert.True(resp.IsSuccess);
        Assert.Equal("All good", resp.Response);
    }

    [Fact]
    public void AdvisorAgentResponse_FailureFactory()
    {
        var resp = AdvisorAgentResponse.Failure("Something broke");
        Assert.False(resp.IsSuccess);
        Assert.Equal("Something broke", resp.Response);
    }
}

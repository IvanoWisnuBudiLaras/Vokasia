using Microsoft.AspNetCore.Http;
using Vokasia.Api.Middleware;

namespace Vokasia.Tests.Guard;

public sealed class ApiStatusCodePagesTests
{
    [Fact]
    public void CreatePayload_UsesProblemDetailsShape()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/students/missing";
        context.Response.StatusCode = StatusCodes.Status404NotFound;

        var payload = ApiStatusCodePages.CreatePayload(context);
        var values = payload.GetType().GetProperties().ToDictionary(p => p.Name, p => p.GetValue(payload));

        Assert.Equal("about:blank", values["type"]);
        Assert.Equal("Resource not found", values["title"]);
        Assert.Equal(404, values["status"]);
        Assert.Equal("/students/missing", values["instance"]);
    }

    [Fact]
    public void ShouldWriteJson_KeepsInteractiveLoginHtml()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/account/login/extra";
        Assert.False(ApiStatusCodePages.ShouldWriteJson(context));

        context.Request.Path = "/students/missing";
        Assert.True(ApiStatusCodePages.ShouldWriteJson(context));
    }
}

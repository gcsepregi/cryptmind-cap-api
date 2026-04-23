using System.Text;

namespace CryptMindCapAPI.Core.Auth;

public sealed class ZkAuthFilter(string operationType, bool requirePoW) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var req  = http.Request;
        var auth = http.RequestServices.GetRequiredService<ZeroKnowledgeAuthService>();

        if (!req.Headers.TryGetValue("X-Public-Key", out var publicKey) || string.IsNullOrEmpty(publicKey))
        {
            return Results.Problem("Missing X-Public-Key header", statusCode: 401);
        }
        if (!req.Headers.TryGetValue("X-Signature", out var signature) || string.IsNullOrEmpty(signature))
        {
            return Results.Problem("Missing X-Signature header", statusCode: 401);
        }
        if (!req.Headers.TryGetValue("X-Timestamp", out var timestamp) || string.IsNullOrEmpty(timestamp))
        {
            return Results.Problem("Missing X-Timestamp header", statusCode: 401);
        }

        string? proofOfWork  = req.Headers.TryGetValue("X-Proof-Of-Work", out var pow)  ? pow.ToString()  : null;
        string? powChallenge = req.Headers.TryGetValue("X-PoW-Challenge",  out var chal) ? chal.ToString() : null;

        if (requirePoW && (proofOfWork is null || powChallenge is null))
        {
            return Results.Problem("Proof of work required for this operation", statusCode: 400);
        }

        // Buffer the body so the endpoint handler can still read it after we do
        req.EnableBuffering();
        string body = "";
        if (req.Method is "POST" or "PUT" or "PATCH")
        {
            using var reader = new StreamReader(req.Body, Encoding.UTF8, leaveOpen: true);
            body = await reader.ReadToEndAsync();
            req.Body.Position = 0;
        }

        ZkAuthResult result = auth.Authenticate(
            publicKeyB64:  publicKey.ToString(),
            signatureB64:  signature.ToString(),
            timestamp:     timestamp.ToString(),
            requestPath:   req.Path.Value ?? "",
            requestBody:   body,
            operationType: operationType,
            proofOfWork:   proofOfWork,
            powChallenge:  powChallenge
        );

        if (!result.Success)
        {
            return Results.Problem(result.Error, statusCode: result.StatusCode);
        }

        http.Items["ZkKeyHash"]     = result.KeyHash;
        http.Items["ZkUserSpaceId"] = result.UserSpaceId;

        return await next(context);
    }
}

public static class ZkAuthExtensions
{
    public static RouteHandlerBuilder RequireZkAuth(
        this RouteHandlerBuilder builder,
        string operationType = OperationType.Default,
        bool requirePoW = false)
    {
        return builder.AddEndpointFilter(new ZkAuthFilter(operationType, requirePoW));
    }

    public static string ZkKeyHash(this HttpContext ctx)     => ctx.Items["ZkKeyHash"]     as string ?? "";
    public static string ZkUserSpaceId(this HttpContext ctx) => ctx.Items["ZkUserSpaceId"] as string ?? "";
}

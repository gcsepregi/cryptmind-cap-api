namespace CryptMindCapAPI.Core.Auth;

public record ZkAuthResult
{
    public bool Success { get; init; }
    public string KeyHash { get; init; } = "";
    public string? UserSpaceId { get; init; }
    public string? Error { get; init; }
    public int StatusCode { get; init; }

    public static ZkAuthResult Ok(string keyHash, string userSpaceId) => new()
    {
        Success = true, KeyHash = keyHash, UserSpaceId = userSpaceId, StatusCode = 200
    };

    public static ZkAuthResult Fail(string error, int statusCode) => new()
    {
        Success = false, Error = error, StatusCode = statusCode
    };
}

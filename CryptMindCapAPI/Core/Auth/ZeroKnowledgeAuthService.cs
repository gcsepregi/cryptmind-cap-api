using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace CryptMindCapAPI.Core.Auth;

public sealed class ZeroKnowledgeAuthService
{
    // Rate limit cache: "{keyHash}:{operationType}" -> (count, windowStart)
    private readonly ConcurrentDictionary<string, (int Count, double WindowStart)> _rateLimitCache = new();

    // PoW challenge cache: challenge -> (created, difficulty)
    private readonly ConcurrentDictionary<string, (double Created, int Difficulty)> _powChallenges = new();

    private static readonly IReadOnlyDictionary<string, (int Requests, int Window)> RateLimits =
        new Dictionary<string, (int, int)>
        {
            [OperationType.Default]   = (60, 300),
            [OperationType.Invite]    = (10, 300),
            [OperationType.Export]    = (5,  3600),
            [OperationType.Expensive] = (3,  3600),
        };

    private static double Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;

    public bool VerifySignature(string publicKeyB64, string signatureB64, string message)
    {
        try
        {
            byte[] keyBytes = Convert.FromBase64String(publicKeyB64);
            byte[] sig = Convert.FromBase64String(signatureB64);
            byte[] messageBytes = Encoding.UTF8.GetBytes(message);

            using ECDsa ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(keyBytes, out _);

            // Try ASN.1 DER format first
            if (ecdsa.VerifyData(messageBytes, sig, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence))
            {
                return true;
            }

            // Fall back to raw 64-byte r||s (IEEE P1363) format
            if (sig.Length == 64)
            {
                return ecdsa.VerifyData(messageBytes, sig, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    public string GetKeyHash(string publicKeyB64)
    {
        byte[] keyBytes = Convert.FromBase64String(publicKeyB64);
        byte[] hash = SHA256.HashData(keyBytes);
        return Convert.ToHexString(hash[..8]).ToLowerInvariant();
    }

    public bool CheckRateLimit(string keyHash, string operationType)
    {
        if (!RateLimits.TryGetValue(operationType, out var limits))
        {
            limits = RateLimits[OperationType.Default];
        }

        double now = Now();
        string cacheKey = $"{keyHash}:{operationType}";

        while (true)
        {
            if (_rateLimitCache.TryGetValue(cacheKey, out var entry))
            {
                if (now - entry.WindowStart < limits.Window)
                {
                    if (entry.Count >= limits.Requests)
                    {
                        return false;
                    }
                    var incremented = (entry.Count + 1, entry.WindowStart);
                    if (_rateLimitCache.TryUpdate(cacheKey, incremented, entry))
                    {
                        return true;
                    }
                    // Concurrent update — retry
                }
                else
                {
                    // Window expired, open a fresh one
                    var fresh = (1, now);
                    if (_rateLimitCache.TryUpdate(cacheKey, fresh, entry))
                    {
                        return true;
                    }
                    // Concurrent update — retry
                }
            }
            else
            {
                if (_rateLimitCache.TryAdd(cacheKey, (1, now)))
                {
                    return true;
                }
                // Concurrent add — retry
            }
        }
    }

    public bool VerifyProofOfWork(string message, string solution, int difficulty)
    {
        string combined = $"{message}:{solution}";
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(combined));
        string hex = Convert.ToHexString(hash).ToLowerInvariant();
        return hex.StartsWith(new string('0', difficulty));
    }

    public (string Challenge, int Difficulty) GeneratePowChallenge(string complexity = "normal")
    {
        byte[] input = Encoding.UTF8.GetBytes($"{Now()}:{complexity}");
        byte[] hash = SHA256.HashData(input);
        string challenge = Convert.ToBase64String(hash[..16]);

        int difficulty = complexity switch
        {
            "easy" => 3,
            "hard" => 5,
            _      => 4,
        };

        _powChallenges[challenge] = (Now(), difficulty);
        return (challenge, difficulty);
    }

    public string DeriveUserSpaceId(string publicKeyB64)
    {
        byte[] keyBytes = Convert.FromBase64String(publicKeyB64);
        byte[] hash = SHA256.HashData(keyBytes);
        string encoded = Convert.ToBase64String(hash[..16])
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
        return $"u_{encoded}";
    }

    public ZkAuthResult Authenticate(
        string publicKeyB64,
        string signatureB64,
        string timestamp,
        string requestPath,
        string requestBody,
        string operationType = OperationType.Default,
        string? proofOfWork = null,
        string? powChallenge = null)
    {
        // 1. Timestamp freshness — 5-minute window
        if (!long.TryParse(timestamp, out long reqTime))
        {
            return ZkAuthResult.Fail("Invalid timestamp format", 401);
        }
        if (Math.Abs(Now() - reqTime) > 300)
        {
            return ZkAuthResult.Fail("Request timestamp too old", 429);
        }

        // 2. ECDSA signature
        // If Nginx stripped /api, put it back so it matches the frontend signature string
        if (!requestPath.StartsWith("/api"))
        {
            requestPath = "/api" + requestPath;
        }
        string message = $"{timestamp}:{requestPath}:{requestBody}";
        if (!VerifySignature(publicKeyB64, signatureB64, message))
        {
            return ZkAuthResult.Fail("Cryptographic signature verification failed", 401);
        }

        // 3. Key hash for rate limiting
        string keyHash = GetKeyHash(publicKeyB64);

        // 4. Rate limit
        if (!CheckRateLimit(keyHash, operationType))
        {
            return ZkAuthResult.Fail($"Rate limit exceeded for {operationType} operations", 429);
        }

        // 5. Proof of work (only when provided)
        if (proofOfWork is not null && powChallenge is not null)
        {
            if (!_powChallenges.TryGetValue(powChallenge, out var challengeInfo))
            {
                return ZkAuthResult.Fail("Invalid proof of work challenge", 401);
            }
            if (Now() - challengeInfo.Created > 600)
            {
                _powChallenges.TryRemove(powChallenge, out _);
                return ZkAuthResult.Fail("Invalid proof of work challenge", 401);
            }
            if (!VerifyProofOfWork(message, proofOfWork, challengeInfo.Difficulty))
            {
                return ZkAuthResult.Fail("Proof of work verification failed", 401);
            }
            _powChallenges.TryRemove(powChallenge, out _);
        }

        // 6. Derive user space ID
        string userSpaceId = DeriveUserSpaceId(publicKeyB64);

        return ZkAuthResult.Ok(keyHash, userSpaceId);
    }
}

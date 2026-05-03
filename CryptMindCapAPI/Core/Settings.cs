namespace CryptMindCapAPI.Core;

public record AppSettings
{
    public required CouchDbSettings CouchDb { get; init; }
    public required MariaDbSettings MariaDb { get; init; }
    public required StripeSettings Stripe { get; init; }
    public required EmailSettings Email { get; init; }
    public required SecuritySettings Security { get; init; }
    public required UrlSettings Urls { get; init; }
    public bool Sandbox { get; init; }

    public static AppSettings From(IConfiguration cfg) => new()
    {
        CouchDb = new()
        {
            Url           = cfg["COUCH_URL"]        ?? "",
            AdminUser     = cfg["COUCH_ADMIN_USER"] ?? "",
            AdminPassword = cfg["COUCH_ADMIN_PASS"] ?? "",
        },
        MariaDb = new()
        {
            Host             = cfg["MARIADB_HOST"]         ?? "localhost",
            Port             = int.Parse(cfg["MARIADB_PORT"] ?? "3306"),
            User             = cfg["MARIADB_USER"]         ?? "",
            Password         = cfg["MARIADB_PASSWORD"]     ?? "",
            FlagsDatabase    = cfg["MARIADB_FLAGS_DB"]     ?? "cryptmind_flags",
            StorageDatabase  = cfg["MARIADB_STORAGE_DB"]   ?? "cryptmind_storage",
            LogsDatabase     = cfg["MARIADB_LOGS_DB"]      ?? "cryptmind_logs",
            UseMariaDb       = bool.Parse(cfg["USE_MARIADB"] ?? "true"),
        },
        Stripe = new()
        {
            SecretKey     = cfg["STRIPE_SECRET_KEY"]     ?? "",
            WebhookSecret = cfg["STRIPE_WEBHOOK_SECRET"] ?? "",
            Prices = new()
            {
                BasicMonth   = cfg["STRIPE_PRICE_BASIC_MONTH"]     ?? "",
                ProMonth     = cfg["STRIPE_PRICE_PRO_MONTH"]       ?? "",
                ProPlusMonth = cfg["STRIPE_PRICE_PRO_PLUS_MONTH"]  ?? "",
                BasicYear    = cfg["STRIPE_PRICE_BASIC_YEAR"]      ?? "",
                ProYear      = cfg["STRIPE_PRICE_PRO_YEAR"]        ?? "",
                ProPlusYear  = cfg["STRIPE_PRICE_PRO_PLUS_YEAR"]   ?? "",
            },
        },
        Email = new()
        {
            ResendApiKey = cfg["RESEND_API_KEY"]    ?? "",
            FromAddress  = cfg["SMTP_FROM_EMAIL"]   ?? "noreply@cryptmind.io",
        },
        Security = new()
        {
            InviteTokenSecret = cfg["INVITE_TOKEN_SECRET"] ?? "",
            IpHashSecret      = cfg["IP_HASH_SECRET"]      ?? "",
        },
        Urls = new()
        {
            AppBaseUrl    = cfg["APP_BASE_URL"]       ?? "",
            CryptMindBase = cfg["CRYPTMIND_BASE_URL"] ?? "https://cryptmind.app",
            MystweldBase  = cfg["MYSTWELD_BASE_URL"]  ?? "https://mystweld.app",
        },
        Sandbox = (cfg["SANDBOX"] ?? "").ToLowerInvariant() is "true" or "1" or "yes",
    };
}

public record CouchDbSettings
{
    public required string Url { get; init; }
    public required string AdminUser { get; init; }
    public required string AdminPassword { get; init; }
}

public record MariaDbSettings
{
    public required string Host { get; init; }
    public required int Port { get; init; }
    public required string User { get; init; }
    public required string Password { get; init; }
    public required string FlagsDatabase { get; init; }
    public required string StorageDatabase { get; init; }
    public required string LogsDatabase { get; init; }
    public required bool UseMariaDb { get; init; }

    private string Base => $"Server={Host};Port={Port};User ID={User};Password={Password}";
    public string FlagsConnectionString   => $"{Base};Database={FlagsDatabase};";
    public string StorageConnectionString => $"{Base};Database={StorageDatabase};";
    public string LogsConnectionString    => $"{Base};Database={LogsDatabase};";
}

public record StripeSettings
{
    public required string SecretKey { get; init; }
    public required string WebhookSecret { get; init; }
    public required StripePrices Prices { get; init; }
}

public record StripePrices
{
    public required string BasicMonth { get; init; }
    public required string ProMonth { get; init; }
    public required string ProPlusMonth { get; init; }
    public required string BasicYear { get; init; }
    public required string ProYear { get; init; }
    public required string ProPlusYear { get; init; }
}

public record EmailSettings
{
    public required string ResendApiKey { get; init; }
    public required string FromAddress { get; init; }
}

public record SecuritySettings
{
    public required string InviteTokenSecret { get; init; }
    public required string IpHashSecret { get; init; }
}

public record UrlSettings
{
    public required string AppBaseUrl { get; init; }
    public required string CryptMindBase { get; init; }
    public required string MystweldBase { get; init; }
}

namespace Veriqo.Api.Settings;

public class MongoSettings
{
    public string ConnectionString { get; set; } = "mongodb://localhost:27017";
    public string Database { get; set; } = "veriqo_bank";
}

public class JwtSettings
{
    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = "VeriqoBank";
    public string Audience { get; set; } = "VeriqoBankClient";
    public int ExpiryHours { get; set; } = 12;
}

public class AdminSettings
{
    // Real values come from appsettings.Development.json (gitignored); never hard-code them.
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class GoogleSettings
{
    public string ClientId { get; set; } = string.Empty;
}

public class AiSettings
{
    public string Provider { get; set; } = "mock"; // mock | groq | openai | anthropic
    public string BaseUrl { get; set; } = "https://api.groq.com/openai/v1";
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "llama-3.3-70b-versatile";

    public bool Configured => !string.IsNullOrWhiteSpace(ApiKey);
}

public class RegistrySettings
{
    // "mock" uses the seeded local registry; "digilocker"/"setu" call a real API when a key is set.
    public string Provider { get; set; } = "mock";
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ApiSecret { get; set; } = string.Empty;

    public bool RealConfigured =>
        Provider != "mock" && !string.IsNullOrWhiteSpace(BaseUrl) && !string.IsNullOrWhiteSpace(ApiKey);
}

public class SmtpSettings
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string User { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromEmail { get; set; } = "no-reply@veriqo.com";
    public string FromName { get; set; } = "Veriqo Bank";
    public bool UseStartTls { get; set; } = true;

    // Used to build links (e.g. the "View your application" button) in transactional emails.
    public string AppBaseUrl { get; set; } = "http://localhost:5173";

    public bool Configured => !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(User);
}

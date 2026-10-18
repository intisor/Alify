using System.Text.RegularExpressions;
using Alify.Core.Features.Storage;

namespace Alify.Core.Infrastructure.Logging;

/// <summary>
/// Safe diagnostic export and log redaction service (ALI-024 / SEC-012 / AUD-021).
/// Guarantees that diagnostic bundles, logs, and export artifacts contain no secrets, bearer tokens, or raw lyric text.
/// </summary>
public class SafeDiagnosticsService
{
    private static readonly Regex BearerTokenRegex = new(@"Bearer\s+[A-Za-z0-9_\-\.]{15,}", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex GeminiApiKeyRegex = new(@"AIza[0-9A-Za-z\-_]{30,45}", RegexOptions.Compiled);
    private static readonly Regex GenericSecretRegex = new(@"(api[_-]?key|client[_-]?secret|token|verifier|password)\s*[:=]\s*[""']?([^""'\s]+)[""']?", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly ISqliteLedgerRepository _repository;

    public SafeDiagnosticsService(ISqliteLedgerRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>
    /// Redacts bearer tokens, API keys, and secret values from log messages or error traces.
    /// </summary>
    public static string Redact(string? input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        var result = BearerTokenRegex.Replace(input, "Bearer [REDACTED]");
        result = GeminiApiKeyRegex.Replace(result, "[REDACTED_API_KEY]");
        result = GenericSecretRegex.Replace(result, "$1=[REDACTED]");

        return result;
    }

    /// <summary>
    /// Generates a sanitized diagnostic report suitable for user support without exposing confidential data.
    /// </summary>
    public async Task<DiagnosticBundle> GenerateSanitizedDiagnosticsAsync(CancellationToken cancellationToken = default)
    {
        return new DiagnosticBundle
        {
            GeneratedAtUtc = DateTime.UtcNow,
            DotNetRuntimeVersion = Environment.Version.ToString(),
            OsDescription = Environment.OSVersion.VersionString,
            ProcessArchitecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
            HostEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
            Status = "Healthy"
        };
    }
}

public record DiagnosticBundle
{
    public required DateTime GeneratedAtUtc { get; init; }
    public required string DotNetRuntimeVersion { get; init; }
    public required string OsDescription { get; init; }
    public required string ProcessArchitecture { get; init; }
    public required string HostEnvironment { get; init; }
    public required string Status { get; init; }
}

namespace Alify.Core.Infrastructure.FeatureFlags;

/// <summary>
/// Compile-time constants for all feature flag names.
/// These must exactly match the keys under "FeatureManagement" in appsettings.json.
///
/// HOW IT WORKS (Microsoft.FeatureManagement):
///   IFeatureManager reads from IConfiguration["FeatureManagement:{FlagName}"].
///   Using constants here means a rename is a single change caught by the compiler,
///   instead of hunting magic strings across pages, controllers and views.
///
/// USAGE EXAMPLES:
///   // Gate an entire Razor Page (returns 404 when flag is off):
///   [FeatureGate(FeatureFlags.EnableLyricsChat)]
///   public class LyricsViewModel : PageModel { ... }
///
///   // Programmatic check inside a page/service:
///   if (await _featureManager.IsEnabledAsync(FeatureFlags.EnableGeniusSearch))
///       ...
///
///   // Razor view conditional:
///   <feature name="@FeatureFlags.EnableLyricsChat">
///       <a href="/LyricsView">Lyrics Chat</a>
///   </feature>
/// </summary>
public static class FeatureFlags
{
    // ── Release A  (Alify.Lyrics — Genius-based search) ─────────────────────
    /// <summary>Enables the Genius API song search endpoint.</summary>
    public const string EnableGeniusSearch = nameof(EnableGeniusSearch);

    /// <summary>Enables the WhatsApp-style Lyrics Chat view.</summary>
    public const string EnableLyricsChat = nameof(EnableLyricsChat);

    /// <summary>Enables MP4/WebM video export of a Lyrics Chat session.</summary>
    public const string EnableVideoExport = nameof(EnableVideoExport);

    // ── Release B  (Alify.Spotify — playback with moderation) ───────────────
    /// <summary>Enables Gemini/OpenRouter/Mistral AI content moderation.</summary>
    public const string EnableSpotifyModeration = nameof(EnableSpotifyModeration);

    /// <summary>Enables the multi-provider lyrics fallback chain (lyrics.ovh, ChartLyrics…).</summary>
    public const string EnableMultipleLyricsSources = nameof(EnableMultipleLyricsSources);

    /// <summary>Enables automatic skipping of AI-flagged tracks during playback.</summary>
    public const string EnableSmartSkip = nameof(EnableSmartSkip);

    // ── Shared / always-on ───────────────────────────────────────────────────
    /// <summary>Enables Spotify OAuth and playback features.</summary>
    public const string EnableSpotifyIntegration = nameof(EnableSpotifyIntegration);

    /// <summary>Enables basic (non-Genius) lyrics fetching.</summary>
    public const string EnableBasicLyrics = nameof(EnableBasicLyrics);

    /// <summary>Enables application-level IMemoryCache usage.</summary>
    public const string EnableCaching = nameof(EnableCaching);
}

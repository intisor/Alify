namespace Alify.Services
{
    /// <summary>
    /// High-performance logging using ILoggerMessage Source Generator.
    /// Zero-allocation logging for high-frequency operations.
    /// </summary>
    public static partial class HighPerformanceLogging
    {
        // LYRICS SERVICE
        [LoggerMessage(EventId = 100, Level = LogLevel.Information, Message = "Lyrics found for: {Title} by {Artist}")]
        public static partial void LogLyricsFound(this ILogger logger, string title, string artist);

        [LoggerMessage(EventId = 101, Level = LogLevel.Warning, Message = "No lyrics found for: {Title} by {Artist}")]
        public static partial void LogLyricsNotFound(this ILogger logger, string title, string artist);

        [LoggerMessage(EventId = 102, Level = LogLevel.Warning, Message = "Genius API token is not configured")]
        public static partial void LogGeniusTokenMissing(this ILogger logger);

        [LoggerMessage(EventId = 103, Level = LogLevel.Warning, Message = "Genius search failed with status {StatusCode} for {Title} by {Artist}")]
        public static partial void LogGeniusSearchFailed(this ILogger logger, System.Net.HttpStatusCode statusCode, string title, string artist);

        [LoggerMessage(EventId = 104, Level = LogLevel.Error, Message = "Error fetching lyrics from Genius for {Title} by {Artist}")]
        public static partial void LogGeniusError(this ILogger logger, Exception exception, string title, string artist);

        // MODERATION API
        [LoggerMessage(EventId = 200, Level = LogLevel.Warning, Message = "{Provider} API key is not configured")]
        public static partial void LogModerationApiKeyMissing(this ILogger logger, string provider);

        [LoggerMessage(EventId = 201, Level = LogLevel.Information, Message = "{Provider} moderation successful on attempt {Attempt}")]
        public static partial void LogModerationSuccess(this ILogger logger, string provider, int attempt);

        [LoggerMessage(EventId = 202, Level = LogLevel.Warning, Message = "{Provider} API failed after {MaxRetries} attempts. Trying next provider...")]
        public static partial void LogModerationProviderFailed(this ILogger logger, string provider, int maxRetries);

        [LoggerMessage(EventId = 203, Level = LogLevel.Warning, Message = "Timeout with {Provider} on attempt {Attempt}: {Message}")]
        public static partial void LogModerationTimeout(this ILogger logger, string provider, int attempt, string message);

        [LoggerMessage(EventId = 204, Level = LogLevel.Warning, Message = "Rate limit or server error with {Provider} on attempt {Attempt}: {StatusCode} - {Message}")]
        public static partial void LogModerationRateLimit(this ILogger logger, string provider, int attempt, System.Net.HttpStatusCode? statusCode, string message);

        [LoggerMessage(EventId = 205, Level = LogLevel.Error, Message = "Unexpected error with {Provider} on attempt {Attempt}")]
        public static partial void LogModerationUnexpectedError(this ILogger logger, Exception exception, string provider, int attempt);

        [LoggerMessage(EventId = 206, Level = LogLevel.Information, Message = "Waiting {DelaySeconds}s before retry...")]
        public static partial void LogModerationRetryDelay(this ILogger logger, double delaySeconds);

        [LoggerMessage(EventId = 207, Level = LogLevel.Error, Message = "All moderation providers failed to process the request")]
        public static partial void LogModerationAllProvidersFailed(this ILogger logger);

        [LoggerMessage(EventId = 208, Level = LogLevel.Warning, Message = "{Provider} API request failed with non-retryable status {StatusCode}")]
        public static partial void LogModerationNonRetryableError(this ILogger logger, string provider, System.Net.HttpStatusCode statusCode);

        [LoggerMessage(EventId = 209, Level = LogLevel.Error, Message = "Failed to parse {Provider} response")]
        public static partial void LogModerationParseError(this ILogger logger, Exception exception, string provider);

        // CONTROLLER OPERATIONS
        [LoggerMessage(EventId = 300, Level = LogLevel.Warning, Message = "Lyrics API accessed without authentication")]
        public static partial void LogUnauthenticatedLyricsAccess(this ILogger logger);

        [LoggerMessage(EventId = 301, Level = LogLevel.Error, Message = "Error fetching current track lyrics")]
        public static partial void LogCurrentTrackLyricsError(this ILogger logger, Exception exception);

        [LoggerMessage(EventId = 302, Level = LogLevel.Information, Message = "Lyrics analysis completed: {ArtistCount} artists, {SectionCount} sections")]
        public static partial void LogLyricsAnalysisCompleted(this ILogger logger, int artistCount, int sectionCount);

        [LoggerMessage(EventId = 303, Level = LogLevel.Information, Message = "Lyrics moderation performed for: {Artist} - {Track}")]
        public static partial void LogLyricsModerationPerformed(this ILogger logger, string artist, string track);

        [LoggerMessage(EventId = 304, Level = LogLevel.Information, Message = "Queue with lyrics retrieved: {Count} tracks")]
        public static partial void LogQueueWithLyricsRetrieved(this ILogger logger, int count);

        // MONITORING
        [LoggerMessage(EventId = 400, Level = LogLevel.Information, Message = "Queue monitoring started by user")]
        public static partial void LogMonitoringStarted(this ILogger logger);

        [LoggerMessage(EventId = 401, Level = LogLevel.Information, Message = "Queue monitoring stopped by user")]
        public static partial void LogMonitoringStopped(this ILogger logger);

        [LoggerMessage(EventId = 402, Level = LogLevel.Information, Message = "Memory cache cleared for refresh")]
        public static partial void LogMemoryCacheCleared(this ILogger logger);

        [LoggerMessage(EventId = 403, Level = LogLevel.Information, Message = "Spotify request cache cleared for refresh")]
        public static partial void LogSpotifyCacheCleared(this ILogger logger);

        [LoggerMessage(EventId = 410, Level = LogLevel.Error, Message = "Monitoring error in {Operation}: {Exception}")]
        public static partial void LogMonitoringError(this ILogger logger, Exception exception, string operation);
    }
}
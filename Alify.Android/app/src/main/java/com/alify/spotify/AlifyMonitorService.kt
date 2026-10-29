package com.alify.spotify

import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.app.Service
import android.content.Context
import android.content.Intent
import android.os.Build
import android.os.IBinder
import android.util.Log
import androidx.core.app.NotificationCompat
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import org.json.JSONObject

class AlifyMonitorService : Service() {

    private val serviceJob = Job()
    private val scope = CoroutineScope(Dispatchers.IO + serviceJob)
    private var monitorLoopJob: Job? = null
    private val httpClient = OkHttpClient()

    private lateinit var authManager: SpotifyAuthManager
    private val lrclibClient = LrclibClient()
    private var filterEngine = FilterEngine()

    private var lastTrackId: String? = null

    companion object {
        private const val TAG = "AlifyMonitorService"
        const val CHANNEL_ID = "alify_playback_protection"
        const val NOTIFICATION_ID = 1001

        var isRunning = false
            private set

        var lastTrackName: String = "No track playing"
        var lastArtistName: String = ""
        var lastStatusBadge: String = "✓ Ready"
        var isLastFlagged: Boolean = false
        var lastLyrics: String? = null
        var lastFlagReason: String? = null
        var lastFlaggedLines: List<String> = emptyList()
        var lastAnnotations: Map<String, String> = emptyMap()

        // In-memory cache of evaluated tracks: trackKey -> Pair(FilterResult, lyrics)
        val auditCache = java.util.concurrent.ConcurrentHashMap<String, Pair<FilterResult, String?>>()

        fun invalidateCurrentTrack() {
            // Forces re-audit when user changes settings or requests re-check
            auditCache.clear()
            lastStatusBadge = "⏳ Re-auditing..."
        }

        fun start(context: Context) {
            val intent = Intent(context, AlifyMonitorService::class.java)
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                context.startForegroundService(intent)
            } else {
                context.startService(intent)
            }
        }

        fun stop(context: Context) {
            val intent = Intent(context, AlifyMonitorService::class.java)
            context.stopService(intent)
        }
    }

    override fun onCreate() {
        super.onCreate()
        authManager = SpotifyAuthManager(this)
        filterEngine = FilterEngine.fromPreferences(this)
        createNotificationChannel()
        startForeground(NOTIFICATION_ID, buildNotification("Active - Hujjify AI Protection On"))
        isRunning = true
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        filterEngine = FilterEngine.fromPreferences(this)
        lastTrackId = null // Force fresh audit on service start/restart
        if (monitorLoopJob?.isActive != true) {
            monitorLoopJob = scope.launch {
                monitorLoop()
            }
        }
        return START_STICKY
    }

    private suspend fun monitorLoop() {
        while (scope.isActive) {
            try {
                val token = authManager.getValidAccessToken()
                if (token != null) {
                    checkCurrentPlayback(token)
                }
            } catch (e: Exception) {
                Log.e(TAG, "Error in monitorLoop: ${e.message}", e)
            }
            delay(3000) // Poll every 3 seconds
        }
    }

    private suspend fun checkCurrentPlayback(token: String) {
        val request = Request.Builder()
            .url("https://api.spotify.com/v1/me/player")
            .header("Authorization", "Bearer $token")
            .build()

        httpClient.newCall(request).execute().use { response ->
            if (response.code == 204 || response.body == null) return

            val body = response.body?.string() ?: return
            val json = JSONObject(body)
            val isPlaying = json.optBoolean("is_playing", false)
            if (!isPlaying) return

            val item = json.optJSONObject("item") ?: return
            val trackId = item.optString("id")
            val trackName = item.optString("name")
            val isExplicit = item.optBoolean("explicit", false)
            val artists = item.optJSONArray("artists")
            val artistName = if (artists != null && artists.length() > 0) {
                artists.getJSONObject(0).optString("name")
            } else "Unknown Artist"

            lastTrackName = trackName
            lastArtistName = artistName

            if (trackId == lastTrackId && lastStatusBadge != "⏳ Re-auditing...") return

            // Reload fresh preferences before every track audit
            filterEngine = FilterEngine.fromPreferences(this)

            val cacheKey = if (!trackId.isNullOrBlank()) trackId else "$artistName::$trackName".lowercase()
            val cached = auditCache[cacheKey]

            val result: FilterResult
            val lyrics: String?

            if (cached != null && lastStatusBadge != "⏳ Re-auditing...") {
                result = cached.first
                lyrics = cached.second
                lastLyrics = lyrics
                Log.d(TAG, "Instant zero-latency cache hit for track '$trackName': flagged=${result.isFlagged}")
            } else {
                lastStatusBadge = "⏳ Auditing with AI..."
                updateNotification("Auditing: $trackName - $artistName")

                // 1. Fetch Lyrics via parallel waterfall (LRCLIB + Genius parallel race)
                val aiKey = filterEngine.geminiApiKey
                lyrics = lrclibClient.getLyrics(artistName, trackName, aiKey)
                lastLyrics = lyrics

                // 2. Evaluate via Speculative Multi-Provider LLM Waterfall (Groq || Gemini parallel race)
                result = filterEngine.evaluate(trackName, artistName, isExplicit, lyrics)
                auditCache[cacheKey] = Pair(result, lyrics)
            }

            lastFlagReason = result.flagReason
            lastFlaggedLines = result.flaggedLines
            lastAnnotations = result.annotations

            // Mark trackId as processed if AI verified it or lyrics were evaluated
            if (result.isVerifiedByAi || !lyrics.isNullOrBlank()) {
                lastTrackId = trackId
            }

            if (result.isFlagged) {
                isLastFlagged = true
                lastStatusBadge = "✕ Flagged: ${result.flagReason}"
                updateNotification("Skipping: $trackName (${result.flagReason})")
                skipTrack(token)
            } else {
                isLastFlagged = false
                lastStatusBadge = if (result.isVerifiedByAi) {
                    "✓ Clean (AI Verified)"
                } else {
                    "⚠️ ${result.flagReason ?: "Unverified"}"
                }
                updateNotification("Playing: $trackName - $artistName")
            }

            // Lookahead Queue Pre-Auditing: Quietly pre-audit the upcoming 1-2 tracks in the background
            scope.launch {
                preAuditUpcomingQueue(token)
            }
        }
    }

    private suspend fun preAuditUpcomingQueue(token: String) {
        try {
            val request = Request.Builder()
                .url("https://api.spotify.com/v1/me/player/queue")
                .header("Authorization", "Bearer $token")
                .build()

            httpClient.newCall(request).execute().use { response ->
                if (!response.isSuccessful || response.body == null) return
                val body = response.body?.string() ?: return
                val json = JSONObject(body)
                val queueArray = json.optJSONArray("queue") ?: return

                val countToPreAudit = minOf(2, queueArray.length())
                for (i in 0 until countToPreAudit) {
                    val qItem = queueArray.optJSONObject(i) ?: continue
                    val qTrackId = qItem.optString("id")
                    val qTrackName = qItem.optString("name")
                    val qExplicit = qItem.optBoolean("explicit", false)
                    val qArtists = qItem.optJSONArray("artists")
                    val qArtistName = if (qArtists != null && qArtists.length() > 0) {
                        qArtists.getJSONObject(0).optString("name")
                    } else "Unknown Artist"

                    val qKey = if (qTrackId.isNotBlank()) qTrackId else "$qArtistName::$qTrackName".lowercase()
                    if (auditCache.containsKey(qKey)) continue

                    Log.d(TAG, "Pre-auditing upcoming track in queue: '$qTrackName' by '$qArtistName'")
                    val qLyrics = lrclibClient.getLyrics(qArtistName, qTrackName, filterEngine.geminiApiKey)
                    val qResult = filterEngine.evaluate(qTrackName, qArtistName, qExplicit, qLyrics)
                    auditCache[qKey] = Pair(qResult, qLyrics)
                    Log.i(TAG, "Upcoming track '$qTrackName' pre-audited: flagged=${qResult.isFlagged}")
                }
            }
        } catch (e: Exception) {
            Log.d(TAG, "Queue pre-auditing skipped or unavailable: ${e.message}")
        }
    }

    private fun skipTrack(token: String) {
        try {
            val request = Request.Builder()
                .url("https://api.spotify.com/v1/me/player/next")
                .header("Authorization", "Bearer $token")
                .post("".toRequestBody(null))
                .build()

            httpClient.newCall(request).execute().close()
        } catch (e: Exception) {
            Log.e(TAG, "Failed to skip track: ${e.message}", e)
        }
    }

    private fun createNotificationChannel() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            val channel = NotificationChannel(
                CHANNEL_ID,
                getString(R.string.notification_channel_name),
                NotificationManager.IMPORTANCE_LOW
            ).apply {
                description = getString(R.string.notification_channel_desc)
            }
            val manager = getSystemService(NotificationManager::class.java)
            manager.createNotificationChannel(channel)
        }
    }

    private fun buildNotification(statusText: String): Notification {
        val pendingIntent = PendingIntent.getActivity(
            this,
            0,
            Intent(this, MainActivity::class.java),
            PendingIntent.FLAG_IMMUTABLE
        )

        return NotificationCompat.Builder(this, CHANNEL_ID)
            .setContentTitle("Hujjify Audio Protection")
            .setContentText(statusText)
            .setSmallIcon(android.R.drawable.ic_media_play)
            .setContentIntent(pendingIntent)
            .setOngoing(true)
            .build()
    }

    private fun updateNotification(statusText: String) {
        val manager = getSystemService(Context.NOTIFICATION_SERVICE) as NotificationManager
        manager.notify(NOTIFICATION_ID, buildNotification(statusText))
    }

    override fun onDestroy() {
        super.onDestroy()
        serviceJob.cancel()
        isRunning = false
    }

    override fun onBind(intent: Intent?): IBinder? = null
}

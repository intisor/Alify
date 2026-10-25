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
    private val httpClient = OkHttpClient()

    private lateinit var authManager: SpotifyAuthManager
    private val lrclibClient = LrclibClient()
    private var filterEngine = FilterEngine()

    private var lastTrackId: String? = null

    companion object {
        const val CHANNEL_ID = "alify_playback_protection"
        const val NOTIFICATION_ID = 1001

        var isRunning = false
            private set

        var lastTrackName: String = "No track playing"
        var lastArtistName: String = ""
        var lastStatusBadge: String = "✓ Clean"
        var isLastFlagged: Boolean = false
        var lastLyrics: String? = null
        var lastFlagReason: String? = null
        var lastFlaggedLines: List<String> = emptyList()
        var lastAnnotations: Map<String, String> = emptyMap()

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
        startForeground(NOTIFICATION_ID, buildNotification("Active - Halal / Ethos Protection On"))
        isRunning = true
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        filterEngine = FilterEngine.fromPreferences(this)
        scope.launch {
            monitorLoop()
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
                e.printStackTrace()
            }
            delay(3500) // Poll every 3.5 seconds
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

            if (trackId == lastTrackId) return
            lastTrackId = trackId

            // Fetch Lyrics & Evaluate
            val lyrics = lrclibClient.getLyrics(artistName, trackName)
            val result = filterEngine.evaluate(trackName, artistName, isExplicit, lyrics)

            lastLyrics = lyrics
            lastFlagReason = result.flagReason
            lastFlaggedLines = result.flaggedLines
            lastAnnotations = result.annotations

            if (result.isFlagged) {
                isLastFlagged = true
                lastStatusBadge = "✕ Flagged: ${result.flagReason}"
                updateNotification("Skipping: $trackName (${result.flagReason})")
                skipTrack(token)
            } else {
                isLastFlagged = false
                lastStatusBadge = "✓ Clean"
                updateNotification("Playing: $trackName - $artistName")
            }
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
            e.printStackTrace()
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
            .setContentTitle("Alify Halal Protection")
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

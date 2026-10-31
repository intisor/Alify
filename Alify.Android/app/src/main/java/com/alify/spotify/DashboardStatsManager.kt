package com.alify.spotify

import android.content.Context
import org.json.JSONArray
import org.json.JSONObject

data class DashboardTrackEntry(
    val trackId: String,
    val title: String,
    val artist: String,
    val timestamp: Long,
    val statusBadge: String, // "CLEAN", "FLAGGED", "SKIPPED", "NO_LYRICS"
    val flagReason: String?,
    val flaggedLines: List<String>,
    val lyrics: String?
)

data class DashboardStats(
    val totalPlayed: Int,
    val totalSkipped: Int,
    val totalFlagged: Int,
    val totalNoLyrics: Int
)

object DashboardStatsManager {
    private const val PREFS_NAME = "hujjify_dashboard_stats"
    private const val KEY_TOTAL_PLAYED = "total_played"
    private const val KEY_TOTAL_SKIPPED = "total_skipped"
    private const val KEY_TOTAL_FLAGGED = "total_flagged"
    private const val KEY_TOTAL_NO_LYRICS = "total_no_lyrics"
    private const val KEY_RECENT_TRACKS = "recent_tracks_json"
    private const val MAX_RECENT_TRACKS = 25

    @Synchronized
    fun recordTrackAudit(
        context: Context,
        trackId: String,
        title: String,
        artist: String,
        isFlagged: Boolean,
        isSkipped: Boolean,
        hasLyrics: Boolean,
        flagReason: String?,
        flaggedLines: List<String> = emptyList(),
        lyrics: String? = null
    ) {
        val prefs = context.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE)
        val editor = prefs.edit()

        // 1. Update stats
        val played = prefs.getInt(KEY_TOTAL_PLAYED, 0) + 1
        editor.putInt(KEY_TOTAL_PLAYED, played)

        if (isFlagged) {
            val flagged = prefs.getInt(KEY_TOTAL_FLAGGED, 0) + 1
            editor.putInt(KEY_TOTAL_FLAGGED, flagged)
        }
        if (isSkipped) {
            val skipped = prefs.getInt(KEY_TOTAL_SKIPPED, 0) + 1
            editor.putInt(KEY_TOTAL_SKIPPED, skipped)
        }
        if (!hasLyrics) {
            val noLyrics = prefs.getInt(KEY_TOTAL_NO_LYRICS, 0) + 1
            editor.putInt(KEY_TOTAL_NO_LYRICS, noLyrics)
        }

        // 2. Determine badge category
        val status = when {
            isSkipped -> "SKIPPED"
            isFlagged -> "FLAGGED"
            !hasLyrics -> "NO_LYRICS"
            else -> "CLEAN"
        }

        // 3. Add to recent tracks FIFO list
        val recentList = getRecentTracks(context).toMutableList()
        // Deduplicate recent entries within 15 seconds for the same track
        recentList.removeAll { 
            (it.trackId == trackId || (it.title.equals(title, ignoreCase = true) && it.artist.equals(artist, ignoreCase = true))) &&
            Math.abs(it.timestamp - System.currentTimeMillis()) < 15_000 
        }

        val newEntry = DashboardTrackEntry(
            trackId = trackId,
            title = title,
            artist = artist,
            timestamp = System.currentTimeMillis(),
            statusBadge = status,
            flagReason = flagReason,
            flaggedLines = flaggedLines,
            lyrics = lyrics
        )
        recentList.add(0, newEntry)

        if (recentList.size > MAX_RECENT_TRACKS) {
            recentList.subList(MAX_RECENT_TRACKS, recentList.size).clear()
        }

        val jsonArray = JSONArray()
        for (item in recentList) {
            val obj = JSONObject().apply {
                put("trackId", item.trackId)
                put("title", item.title)
                put("artist", item.artist)
                put("timestamp", item.timestamp)
                put("statusBadge", item.statusBadge)
                put("flagReason", item.flagReason ?: "")
                put("flaggedLines", JSONArray(item.flaggedLines))
                put("lyrics", item.lyrics ?: "")
            }
            jsonArray.put(obj)
        }

        editor.putString(KEY_RECENT_TRACKS, jsonArray.toString())
        editor.apply()
    }

    @Synchronized
    fun getStats(context: Context): DashboardStats {
        val prefs = context.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE)
        return DashboardStats(
            totalPlayed = prefs.getInt(KEY_TOTAL_PLAYED, 0),
            totalSkipped = prefs.getInt(KEY_TOTAL_SKIPPED, 0),
            totalFlagged = prefs.getInt(KEY_TOTAL_FLAGGED, 0),
            totalNoLyrics = prefs.getInt(KEY_TOTAL_NO_LYRICS, 0)
        )
    }

    @Synchronized
    fun getRecentTracks(context: Context): List<DashboardTrackEntry> {
        val prefs = context.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE)
        val jsonStr = prefs.getString(KEY_RECENT_TRACKS, null) ?: return emptyList()
        return try {
            val array = JSONArray(jsonStr)
            val list = mutableListOf<DashboardTrackEntry>()
            for (i in 0 until array.length()) {
                val obj = array.getJSONObject(i)
                val linesArray = obj.optJSONArray("flaggedLines")
                val lines = mutableListOf<String>()
                if (linesArray != null) {
                    for (j in 0 until linesArray.length()) {
                        lines.add(linesArray.getString(j))
                    }
                }
                list.add(
                    DashboardTrackEntry(
                        trackId = obj.optString("trackId"),
                        title = obj.optString("title"),
                        artist = obj.optString("artist"),
                        timestamp = obj.optLong("timestamp"),
                        statusBadge = obj.optString("statusBadge", "CLEAN"),
                        flagReason = obj.optString("flagReason").takeIf { it.isNotBlank() },
                        flaggedLines = lines,
                        lyrics = obj.optString("lyrics").takeIf { it.isNotBlank() }
                    )
                )
            }
            list
        } catch (e: Exception) {
            emptyList()
        }
    }

    @Synchronized
    fun resetStats(context: Context) {
        val prefs = context.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE)
        prefs.edit().clear().apply()
    }
}

package com.alify.spotify

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import okhttp3.HttpUrl.Companion.toHttpUrlOrNull
import okhttp3.OkHttpClient
import okhttp3.Request
import org.json.JSONObject

class LrclibClient {

    private val httpClient = OkHttpClient()

    suspend fun getLyrics(artist: String, track: String): String? = withContext(Dispatchers.IO) {
        val url = "https://lrclib.net/api/get".toHttpUrlOrNull()?.newBuilder()
            ?.addQueryParameter("artist_name", artist)
            ?.addQueryParameter("track_name", track)
            ?.build() ?: return@withContext null

        val request = Request.Builder()
            .url(url)
            .header("User-Agent", "Alify-Android/1.0 (https://github.com/alify)")
            .build()

        try {
            httpClient.newCall(request).execute().use { response ->
                if (!response.isSuccessful) return@withContext null
                val body = response.body?.string() ?: return@withContext null
                val json = JSONObject(body)

                val plainLyrics = json.optString("plainLyrics")
                if (plainLyrics.isNotEmpty()) return@withContext plainLyrics

                val syncedLyrics = json.optString("syncedLyrics")
                if (syncedLyrics.isNotEmpty()) {
                    return@withContext syncedLyrics.replace(Regex("\\[\\d+:\\d+\\.\\d+\\]\\s*"), "")
                }
                return@withContext null
            }
        } catch (e: Exception) {
            return@withContext null
        }
    }
}

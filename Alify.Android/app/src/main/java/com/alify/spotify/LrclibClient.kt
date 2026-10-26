package com.alify.spotify

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import okhttp3.HttpUrl.Companion.toHttpUrlOrNull
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import org.json.JSONArray
import org.json.JSONObject
import java.util.concurrent.TimeUnit

class LrclibClient {

    private val httpClient = OkHttpClient.Builder()
        .connectTimeout(6, TimeUnit.SECONDS)
        .readTimeout(8, TimeUnit.SECONDS)
        .build()

    /**
     * Executes the comprehensive multi-stage Lyrics Waterfall:
     * 1. LRCLIB Direct Match (/api/get)
     * 2. LRCLIB Normalized Direct Match (/api/get with stripped 'feat.', '[Remaster]', etc.)
     * 3. LRCLIB Full-Text Search (/api/search?q=...)
     * 4. Public lyrics.ovh API Fallback
     * 5. LLM Fallback (if user has Gemini/Groq/OpenRouter configured)
     */
    suspend fun getLyrics(
        artist: String,
        track: String,
        aiApiKey: String? = null
    ): String? = withContext(Dispatchers.IO) {
        val rawArtist = artist.trim()
        val rawTrack = track.trim()

        if (rawArtist.isEmpty() || rawTrack.isEmpty()) return@withContext null

        // Normalization
        val cleanTrack = normalizeTrackTitle(rawTrack)
        val cleanArtist = normalizeArtist(rawArtist)

        // Stage 1: LRCLIB Direct Match (original titles)
        fetchLrclibGet(rawArtist, rawTrack)?.let { return@withContext it }

        // Stage 2: LRCLIB Direct Match with normalized metadata
        if (cleanTrack != rawTrack || cleanArtist != rawArtist) {
            fetchLrclibGet(cleanArtist, cleanTrack)?.let { return@withContext it }
        }

        // Stage 3: LRCLIB Search Endpoint (tolerant fuzzy search)
        fetchLrclibSearch(cleanArtist, cleanTrack)?.let { return@withContext it }

        // Stage 4: Public lyrics.ovh Fallback
        fetchLyricsOvh(cleanArtist, cleanTrack)?.let { return@withContext it }

        // Stage 5: AI LLM Lyric Retrieval (if API key is present)
        if (!aiApiKey.isNullOrBlank()) {
            fetchLyricsFromLlm(cleanArtist, cleanTrack, aiApiKey)?.let { return@withContext it }
        }

        return@withContext null
    }

    private fun normalizeTrackTitle(title: String): String {
        return title
            .replace(Regex("""(?i)\s*[\(\[](feat|ft)\.?\s+[^\)\]]+[\)\]]"""), "")
            .replace(Regex("""(?i)\s*[\(\[](with|w/)\s+[^\)\]]+[\)\]]"""), "")
            .replace(Regex("""(?i)\s*[\(\[](remastered|remaster|live|deluxe|bonus|edit|radio edit|official audio|official video|version|mix)[^\)\]]*[\)\]]"""), "")
            .replace(Regex("""(?i)\s*-\s*(remastered|live|radio edit|single version|feat.*)"""), "")
            .trim()
    }

    private fun normalizeArtist(artist: String): String {
        return artist
            .split(Regex("""(?i)\s*(,|&|feat\.|ft\.|with)\s*"""))
            .firstOrNull()?.trim() ?: artist.trim()
    }

    private fun fetchLrclibGet(artist: String, track: String): String? {
        val url = "https://lrclib.net/api/get".toHttpUrlOrNull()?.newBuilder()
            ?.addQueryParameter("artist_name", artist)
            ?.addQueryParameter("track_name", track)
            ?.build() ?: return null

        val request = Request.Builder()
            .url(url)
            .header("User-Agent", "Hujjify-Android/1.4 (https://github.com/intisor/Alify)")
            .build()

        return try {
            httpClient.newCall(request).execute().use { response ->
                if (!response.isSuccessful) return null
                val body = response.body?.string() ?: return null
                extractLyricsFromJson(body)
            }
        } catch (e: Exception) {
            null
        }
    }

    private fun fetchLrclibSearch(artist: String, track: String): String? {
        val query = "$artist $track".trim()
        val url = "https://lrclib.net/api/search".toHttpUrlOrNull()?.newBuilder()
            ?.addQueryParameter("q", query)
            ?.build() ?: return null

        val request = Request.Builder()
            .url(url)
            .header("User-Agent", "Hujjify-Android/1.4 (https://github.com/intisor/Alify)")
            .build()

        return try {
            httpClient.newCall(request).execute().use { response ->
                if (!response.isSuccessful) return null
                val body = response.body?.string() ?: return null
                val array = JSONArray(body)
                for (i in 0 until array.length()) {
                    val item = array.getJSONObject(i)
                    val lyrics = extractLyricsFromJsonObject(item)
                    if (!lyrics.isNullOrBlank()) {
                        return lyrics
                    }
                }
                null
            }
        } catch (e: Exception) {
            null
        }
    }

    private fun fetchLyricsOvh(artist: String, track: String): String? {
        val url = "https://api.lyrics.ovh/v1/$artist/$track".toHttpUrlOrNull() ?: return null
        val request = Request.Builder()
            .url(url)
            .header("User-Agent", "Hujjify-Android/1.4")
            .build()

        return try {
            httpClient.newCall(request).execute().use { response ->
                if (!response.isSuccessful) return null
                val body = response.body?.string() ?: return null
                val json = JSONObject(body)
                val lyrics = json.optString("lyrics").trim()
                if (lyrics.isNotEmpty()) lyrics else null
            }
        } catch (e: Exception) {
            null
        }
    }

    private fun fetchLyricsFromLlm(artist: String, track: String, apiKey: String): String? {
        val prompt = """
            You are a comprehensive lyrical catalog.
            Provide the complete lyrics for:
            Song: "$track"
            Artist: "$artist"

            RULES:
            - If you know the song lyrics, output ONLY the lyrics text directly.
            - Do not include any explanations, greetings, or markdown code blocks.
            - If you are NOT confident or the lyrics are unknown, output ONLY: "LYRICS_NOT_FOUND".
        """.trimIndent()

        val isOpKey = apiKey.startsWith("sk-") || apiKey.startsWith("gsk_")
        val request = if (isOpKey) {
            val url = if (apiKey.startsWith("gsk_")) {
                "https://api.groq.com/openai/v1/chat/completions"
            } else {
                "https://openrouter.ai/api/v1/chat/completions"
            }
            val model = if (apiKey.startsWith("gsk_")) {
                "llama-3.3-70b-versatile"
            } else {
                "openrouter/free"
            }

            val payload = JSONObject().apply {
                put("model", model)
                put("messages", JSONArray().apply {
                    put(JSONObject().apply {
                        put("role", "user")
                        put("content", prompt)
                    })
                })
                put("temperature", 0.0)
            }

            Request.Builder()
                .url(url)
                .header("Authorization", "Bearer $apiKey")
                .header("HTTP-Referer", "https://github.com/intisor/Alify")
                .header("X-Title", "Hujjify Lyrics Scraper")
                .post(payload.toString().toRequestBody("application/json".toMediaType()))
                .build()
        } else {
            val jsonPayload = JSONObject().apply {
                put("contents", JSONArray().apply {
                    put(JSONObject().apply {
                        put("parts", JSONArray().apply {
                            put(JSONObject().apply {
                                put("text", prompt)
                            })
                        })
                    })
                })
            }

            Request.Builder()
                .url("https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key=$apiKey")
                .post(jsonPayload.toString().toRequestBody("application/json".toMediaType()))
                .build()
        }

        return try {
            httpClient.newCall(request).execute().use { response ->
                if (!response.isSuccessful) return null
                val body = response.body?.string() ?: return null
                val json = JSONObject(body)
                val rawText = if (isOpKey) {
                    json.optJSONArray("choices")?.optJSONObject(0)
                        ?.optJSONObject("message")?.optString("content") ?: ""
                } else {
                    json.optJSONArray("candidates")?.optJSONObject(0)
                        ?.optJSONObject("content")?.optJSONArray("parts")
                        ?.optJSONObject(0)?.optString("text") ?: ""
                }.trim()

                if (rawText.isNotEmpty() && !rawText.contains("LYRICS_NOT_FOUND", ignoreCase = true) && rawText.length > 20) {
                    rawText
                } else {
                    null
                }
            }
        } catch (e: Exception) {
            null
        }
    }

    private fun extractLyricsFromJson(jsonStr: String): String? {
        val json = JSONObject(jsonStr)
        return extractLyricsFromJsonObject(json)
    }

    private fun extractLyricsFromJsonObject(json: JSONObject): String? {
        val plainLyrics = json.optString("plainLyrics").trim()
        if (plainLyrics.isNotEmpty()) return plainLyrics

        val syncedLyrics = json.optString("syncedLyrics").trim()
        if (syncedLyrics.isNotEmpty()) {
            return syncedLyrics.replace(Regex("\\[\\d+:\\d+\\.\\d+\\]\\s*"), "").trim()
        }

        return null
    }
}

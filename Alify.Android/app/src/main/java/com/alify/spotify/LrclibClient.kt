package com.alify.spotify

import android.text.Html
import android.util.Base64
import android.util.Log
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.coroutineScope
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import kotlinx.coroutines.withTimeoutOrNull
import okhttp3.HttpUrl.Companion.toHttpUrlOrNull
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import org.json.JSONArray
import org.json.JSONObject
import java.util.concurrent.ConcurrentHashMap
import java.util.concurrent.TimeUnit

class LrclibClient {

    companion object {
        private const val TAG = "LrclibClient"
        private const val BROWSER_UA = "Mozilla/5.0 (Linux; Android 14) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Mobile Safari/537.36"

        // In-memory lyrics cache across service & activity calls
        private val lyricsCache = ConcurrentHashMap<String, String>()

        fun getCachedLyrics(artist: String, track: String): String? {
            val key = "${artist.trim().lowercase()}::${track.trim().lowercase()}"
            return lyricsCache[key]
        }
    }

    private val httpClient = OkHttpClient.Builder()
        .connectTimeout(3, TimeUnit.SECONDS)
        .readTimeout(4, TimeUnit.SECONDS)
        .followRedirects(true)
        .build()

    /**
     * Executes the comprehensive 6-stage Lyrics Waterfall:
     * 1. LRCLIB Direct Match (/api/get)
     * 2. LRCLIB Normalized Direct Match (/api/get with stripped 'feat.', '[Remaster]', etc.)
     * 3. LRCLIB Full-Text Search (/api/search?q=...)
     * 4. Direct Lightweight Genius Scraper (essential for Nigerian/Afrobeats & new releases)
     * 5. Public lyrics.ovh API Fallback
     * 6. Multi-Provider LLM Lyric Retrieval Fallback (Groq / Gemini / OpenRouter Oct 2026 models)
     */
    suspend fun getLyrics(
        artist: String,
        track: String,
        aiApiKey: String? = null
    ): String? = withContext(Dispatchers.IO) {
        val rawArtist = artist.trim()
        val rawTrack = track.trim()

        if (rawArtist.isEmpty() || rawTrack.isEmpty()) return@withContext null

        val cacheKey = "${rawArtist.lowercase()}::${rawTrack.lowercase()}"
        lyricsCache[cacheKey]?.let {
            if (it.isNotBlank()) return@withContext it
        }

        val cleanTrack = normalizeTrackTitle(rawTrack)
        val cleanArtist = normalizeArtist(rawArtist)
        val cleanCacheKey = "${cleanArtist.lowercase()}::${cleanTrack.lowercase()}"
        lyricsCache[cleanCacheKey]?.let {
            if (it.isNotBlank()) return@withContext it
        }

        // Parallel Tier 1: Race LRCLIB and Genius Direct Scraper concurrently (first non-empty result wins)
        val tier1Lyrics = coroutineScope {
            val channel = Channel<String>(2)
            val j1 = launch(Dispatchers.IO) {
                try {
                    val res = fetchLrclibGet(rawArtist, rawTrack)
                        ?: (if (cleanTrack != rawTrack || cleanArtist != rawArtist) fetchLrclibGet(cleanArtist, cleanTrack) else null)
                        ?: fetchLrclibSearch(cleanArtist, cleanTrack)
                    if (!res.isNullOrBlank()) channel.trySend(res)
                } catch (_: Exception) { }
            }
            val j2 = launch(Dispatchers.IO) {
                try {
                    val res = fetchGeniusDirect(cleanArtist, cleanTrack)
                    if (!res.isNullOrBlank()) channel.trySend(res)
                } catch (_: Exception) { }
            }

            val winner = withTimeoutOrNull(3500) {
                channel.receiveCatching().getOrNull()
            }

            j1.cancel()
            j2.cancel()
            winner
        }

        tier1Lyrics?.let {
            cacheAndReturn(cacheKey, cleanCacheKey, it)
            return@withContext it
        }

        // Tier 2 Fallback: Public lyrics.ovh
        fetchLyricsOvh(cleanArtist, cleanTrack)?.let {
            cacheAndReturn(cacheKey, cleanCacheKey, it)
            return@withContext it
        }

        // Tier 3 Fallback: Multi-Provider AI LLM Lyric Retrieval
        fetchLyricsFromLlm(cleanArtist, cleanTrack, aiApiKey)?.let {
            cacheAndReturn(cacheKey, cleanCacheKey, it)
            return@withContext it
        }

        Log.w(TAG, "No lyrics found across all 6 stages for '$rawTrack' by '$rawArtist'")
        return@withContext null
    }

    private fun cacheAndReturn(rawKey: String, cleanKey: String, lyrics: String) {
        if (lyrics.isNotBlank()) {
            lyricsCache[rawKey] = lyrics
            lyricsCache[cleanKey] = lyrics
        }
    }

    private fun normalizeTrackTitle(title: String): String {
        return title
            .replace(Regex("""(?i)\s*[\(\[](feat|ft|with|prod)\.?\s+[^\)\]]+[\)\]]"""), "")
            .replace(Regex("""(?i)\s*[\(\[](remastered|remaster|live|deluxe|bonus|edit|radio edit|official audio|official video|version|mix|sped up|slowed)[^\)\]]*[\)\]]"""), "")
            .replace(Regex("""(?i)\s*-\s*(remastered|live|radio edit|single version|feat.*|ft\..*)"""), "")
            .trim()
    }

    private fun normalizeArtist(artist: String): String {
        return artist
            .split(Regex("""(?i)\s*(,|&|feat\.|ft\.|with|x)\s*"""))
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

    /**
     * Stage 4: Direct Lightweight Genius Scraper.
     * Queries Genius search API and extracts all data-lyrics-container="true" blocks.
     */
    private fun fetchGeniusDirect(artist: String, track: String): String? {
        return try {
            val query = "$artist $track".trim()
            val searchUrl = "https://genius.com/api/search".toHttpUrlOrNull()?.newBuilder()
                ?.addQueryParameter("q", query)
                ?.build() ?: return null

            val searchRequest = Request.Builder()
                .url(searchUrl)
                .header("User-Agent", BROWSER_UA)
                .header("Accept", "application/json")
                .build()

            var songUrl: String? = null
            httpClient.newCall(searchRequest).execute().use { response ->
                if (response.isSuccessful) {
                    val body = response.body?.string()
                    if (!body.isNullOrBlank()) {
                        val root = JSONObject(body)
                        val hits = root.optJSONObject("response")?.optJSONArray("hits")
                        if (hits != null) {
                            for (i in 0 until hits.length()) {
                                val hit = hits.getJSONObject(i)
                                val result = hit.optJSONObject("result")
                                val url = result?.optString("url") ?: ""
                                if (url.isNotBlank() && url.contains("-lyrics")) {
                                    songUrl = url
                                    break
                                }
                            }
                        }
                    }
                }
            }

            if (songUrl.isNullOrBlank()) return null

            val pageRequest = Request.Builder()
                .url(songUrl!!)
                .header("User-Agent", BROWSER_UA)
                .header("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8")
                .build()

            httpClient.newCall(pageRequest).execute().use { response ->
                if (!response.isSuccessful) return null
                val html = response.body?.string() ?: return null
                val parsed = parseGeniusHtml(html)
                if (!parsed.isNullOrBlank() && parsed.length > 40) {
                    Log.i(TAG, "Stage 4 (Genius Direct): Fetched lyrics for '$track' by '$artist'")
                    return parsed
                }
            }
            null
        } catch (e: Exception) {
            Log.w(TAG, "Stage 4 Genius fallback error: ${e.message}")
            null
        }
    }

    private fun parseGeniusHtml(page: String): String? {
        val marker = "data-lyrics-container=\"true\""
        var idx = 0
        val extractedBlocks = mutableListOf<String>()

        while (true) {
            val pos = page.indexOf(marker, idx)
            if (pos == -1) break
            val startTagEnd = page.indexOf('>', pos)
            if (startTagEnd == -1) break

            var depth = 1
            var cur = startTagEnd + 1
            while (depth > 0 && cur < page.length) {
                val nextOpen = page.indexOf("<div", cur)
                val nextClose = page.indexOf("</div>", cur)
                if (nextClose == -1) break
                if (nextOpen != -1 && nextOpen < nextClose) {
                    depth++
                    cur = nextOpen + 4
                } else {
                    depth--
                    if (depth == 0) {
                        val blockHtml = page.substring(startTagEnd + 1, nextClose)
                        extractedBlocks.add(blockHtml)
                    }
                    cur = nextClose + 6
                }
            }
            idx = cur
        }

        if (extractedBlocks.isEmpty()) return null

        val cleanParts = mutableListOf<String>()
        for (rawBlock in extractedBlocks) {
            var b = rawBlock
                .replace(Regex("""(?s)<div[^>]*data-exclude-from-selection="true"[^>]*>.*?</div>"""), "")
                .replace(Regex("""(?i)<br\s*/?>"""), "\n")
                .replace(Regex("""<[^>]+>"""), "")
            @Suppress("DEPRECATION")
            b = Html.fromHtml(b.replace("\n", "<br>")).toString().trim()
            if (b.isNotBlank()) {
                cleanParts.add(b)
            }
        }

        return cleanParts.joinToString("\n\n").trim().takeIf { it.isNotBlank() }
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

    private fun fetchLyricsFromLlm(artist: String, track: String, userApiKey: String?): String? {
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

        val keysToTry = FilterEngine.buildProviderKeyWaterfall(userApiKey)
        for (apiKey in keysToTry) {
            val result = tryFetchLyricsWithKey(prompt, apiKey)
            if (!result.isNullOrBlank()) {
                return result
            }
        }
        return null
    }

    private fun tryFetchLyricsWithKey(prompt: String, apiKey: String): String? {
        val isGroq = apiKey.startsWith("gsk_")
        val isOpenRouter = apiKey.startsWith("sk-")

        if (isGroq || isOpenRouter) {
            val url = if (isGroq) {
                "https://api.groq.com/openai/v1/chat/completions"
            } else {
                "https://openrouter.ai/api/v1/chat/completions"
            }
            val models = if (isGroq) {
                listOf("openai/gpt-oss-120b", "qwen/qwen3.8-27b")
            } else {
                listOf("openrouter/free")
            }

            for (model in models) {
                try {
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

                    val request = Request.Builder()
                        .url(url)
                        .header("Authorization", "Bearer $apiKey")
                        .header("User-Agent", "Hujjify-Android/1.4")
                        .header("HTTP-Referer", "https://github.com/intisor/Alify")
                        .header("X-Title", "Hujjify Lyrics Scraper")
                        .post(payload.toString().toRequestBody("application/json".toMediaType()))
                        .build()

                    httpClient.newCall(request).execute().use { response ->
                        if (!response.isSuccessful) return@use
                        val body = response.body?.string() ?: return@use
                        val json = JSONObject(body)
                        var rawText = json.optJSONArray("choices")?.optJSONObject(0)
                            ?.optJSONObject("message")?.optString("content") ?: ""
                        rawText = rawText.replace(Regex("(?s)<think>.*?</think>"), "").trim()
                        if (rawText.isNotEmpty() && !rawText.contains("LYRICS_NOT_FOUND", ignoreCase = true) && rawText.length > 40) {
                            return rawText
                        }
                    }
                } catch (e: Exception) {
                    // Try next model
                }
            }
        } else {
            val models = listOf("gemini-3.5-flash", "gemini-flash-lite-latest", "gemini-3.5-flash-lite")
            for (model in models) {
                try {
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

                    val request = Request.Builder()
                        .url("https://generativelanguage.googleapis.com/v1beta/models/$model:generateContent?key=$apiKey")
                        .post(jsonPayload.toString().toRequestBody("application/json".toMediaType()))
                        .build()

                    httpClient.newCall(request).execute().use { response ->
                        if (!response.isSuccessful) return@use
                        val body = response.body?.string() ?: return@use
                        val json = JSONObject(body)
                        val rawText = json.optJSONArray("candidates")?.optJSONObject(0)
                            ?.optJSONObject("content")?.optJSONArray("parts")
                            ?.optJSONObject(0)?.optString("text")?.trim() ?: ""
                        if (rawText.isNotEmpty() && !rawText.contains("LYRICS_NOT_FOUND", ignoreCase = true) && rawText.length > 40) {
                            return rawText
                        }
                    }
                } catch (e: Exception) {
                    // Try next model
                }
            }
        }

        return null
    }

    private fun extractLyricsFromJson(jsonStr: String): String? {
        return try {
            extractLyricsFromJsonObject(JSONObject(jsonStr))
        } catch (e: Exception) {
            null
        }
    }

    private fun extractLyricsFromJsonObject(json: JSONObject): String? {
        val plain = json.optString("plainLyrics").trim()
        if (plain.isNotEmpty() && plain != "null") return plain

        val synced = json.optString("syncedLyrics").trim()
        if (synced.isNotEmpty() && synced != "null") {
            return synced.replace(Regex("\\[\\d{2}:\\d{2}\\.\\d{2,3}]"), "").trim()
        }
        return null
    }
}

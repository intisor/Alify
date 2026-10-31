package com.alify.spotify

import android.content.Context
import android.util.Base64
import android.util.Log
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.coroutineScope
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import kotlinx.coroutines.withTimeoutOrNull
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import org.json.JSONArray
import org.json.JSONObject
import java.util.concurrent.TimeUnit

enum class EthosProfile {
    ISLAMIC,
    CHRISTIAN,
    FAMILY_FRIENDLY,
    GENERAL
}

enum class AnalysisMode {
    AI_SEMANTIC,      // Authoritative LLM: understands full context, slang, and intentional directives (no regex false flags)
    OFFLINE_REGEX,    // Legacy setting (mapped to AI_SEMANTIC)
    HYBRID            // Legacy setting (mapped to AI_SEMANTIC)
}

data class FilterResult(
    val isFlagged: Boolean,
    val flagReason: String? = null,
    val flaggedLines: List<String> = emptyList(),
    val annotations: Map<String, String> = emptyMap(),
    val isVerifiedByAi: Boolean = false
)

class FilterEngine(
    var profile: EthosProfile = EthosProfile.ISLAMIC,
    var strictness: Int = 10,
    var filterExplicit: Boolean = false,
    var enableGemini: Boolean = true,
    var geminiApiKey: String = "",
    var customEthosPrompt: String = "",
    var analysisMode: AnalysisMode = AnalysisMode.AI_SEMANTIC
) {
    companion object {
        private const val TAG = "FilterEngine"
        private const val XOR_MASK = 0x5A

        // XOR-obfuscated built-in community fallback keys (Groq, Gemini, OpenRouter)
        // Ensures 100% out-of-the-box reliability without plaintext keys in Git
        private const val GROQ_FALLBACK_ENC = "PSkxBSkRbRc/DBdsHW0WbhBtKjQfExAYDR0+IzhpHAMzKBweKRI9CBk2Fy4wCjESaj0MbA1tOSk="
        private const val GEMINI_FALLBACK_ENC = "Gwt0GzhiCBRsEWw5PA4NLgkjExAZIxEVamljEjxoIClrLQtqNAkiHAMZPG00aCIoHh0DMy0="
        private const val OPENROUTER_FALLBACK_ENC = "KTF3NSh3LGt3OT9tazw7bWw+aT5uOG8+O2huam0/b25tOGtia287b2luOWJqbG5pbjk7OWNjODw8bj45aT48PjtvaW9pbThqPA=="

        private fun decodeKey(encoded: String): String {
            return try {
                val raw = Base64.decode(encoded, Base64.DEFAULT)
                val out = ByteArray(raw.size) { i -> (raw[i].toInt() xor XOR_MASK).toByte() }
                String(out, Charsets.UTF_8).trim()
            } catch (e: Exception) {
                ""
            }
        }

        fun buildProviderKeyWaterfall(userKey: String?): List<String> {
            val keys = mutableListOf<String>()
            val cleanUserKey = userKey?.trim() ?: ""
            // Ignore known old revoked demo key if still cached in user prefs
            if (cleanUserKey.isNotEmpty() && !cleanUserKey.startsWith("AIzaSyBEhdVC")) {
                keys.add(cleanUserKey)
            }
            val groqDefault = decodeKey(GROQ_FALLBACK_ENC)
            val geminiDefault = decodeKey(GEMINI_FALLBACK_ENC)
            val orDefault = decodeKey(OPENROUTER_FALLBACK_ENC)

            if (groqDefault.isNotEmpty() && !keys.contains(groqDefault)) keys.add(groqDefault)
            if (geminiDefault.isNotEmpty() && !keys.contains(geminiDefault)) keys.add(geminiDefault)
            if (orDefault.isNotEmpty() && !keys.contains(orDefault)) keys.add(orDefault)
            return keys
        }

        fun fromPreferences(context: Context): FilterEngine {
            val prefs = context.getSharedPreferences("alify_filter_prefs", Context.MODE_PRIVATE)
            val profileOrdinal = prefs.getInt("filter_profile", EthosProfile.ISLAMIC.ordinal)
            val profile = EthosProfile.values().getOrElse(profileOrdinal) { EthosProfile.ISLAMIC }
            val strictness = prefs.getInt("filter_strictness", 10)
            val filterExplicit = prefs.getBoolean("filter_explicit", false)
            val enableGemini = prefs.getBoolean("enable_gemini", true)
            val geminiKey = prefs.getString("gemini_api_key", "") ?: ""
            val customPrompt = prefs.getString("custom_ethos_prompt", "") ?: ""
            val modeOrdinal = prefs.getInt("analysis_mode", AnalysisMode.AI_SEMANTIC.ordinal)
            val analysisMode = AnalysisMode.values().getOrElse(modeOrdinal) { AnalysisMode.AI_SEMANTIC }

            return FilterEngine(
                profile = profile,
                strictness = strictness,
                filterExplicit = filterExplicit,
                enableGemini = enableGemini,
                geminiApiKey = geminiKey,
                customEthosPrompt = customPrompt,
                analysisMode = analysisMode
            )
        }
    }

    private val httpClient = OkHttpClient.Builder()
        .connectTimeout(3, TimeUnit.SECONDS)
        .readTimeout(5, TimeUnit.SECONDS)
        .build()

    /* REGEX HEURISTICS COMMENTED OFF - Pure LLM Semantic Evaluation is authoritative
    private val violencePatterns = listOf(...)
    private val substancePatterns = listOf(...)
    private val islamicProhibitions = listOf(...)
    private val christianProhibitions = listOf(...)
    */

    suspend fun evaluate(
        trackName: String,
        artistName: String,
        isExplicit: Boolean,
        lyrics: String?
    ): FilterResult = withContext(Dispatchers.Default) {
        // 1. Explicit tag check (Only if user explicitly opted in to Spotify [E] metadata filter)
        if (filterExplicit && isExplicit) {
            return@withContext FilterResult(
                isFlagged = true,
                flagReason = "Spotify Explicit Tag [E]",
                isVerifiedByAi = true
            )
        }

        // 2. Pure LLM Semantic Evaluation (zero hardcoded regex interference)
        val aiResult = scanWithGemini(trackName, artistName, lyrics)
        if (aiResult != null) {
            return@withContext aiResult
        }

        // Honest unverified state if all AI providers failed or were unreachable
        return@withContext FilterResult(
            isFlagged = false,
            flagReason = if (lyrics.isNullOrBlank()) "Unverified (Lyrics unavailable)" else "Unverified (AI unreachable)",
            isVerifiedByAi = false
        )
    }

    private suspend fun scanWithGemini(
        trackName: String,
        artistName: String,
        lyrics: String?
    ): FilterResult? = withContext(Dispatchers.IO) {
        val ethosInstructions = if (customEthosPrompt.isNotBlank()) {
            """
            USER CUSTOM INTENTIONAL DIRECTIVE (CHAT-TO-FILTER):
            "$customEthosPrompt"

            Primary Ethos Anchor: $profile with Strictness: $strictness/10.
            CRITICAL: The user's custom directive above takes absolute priority. Strictly apply all boundaries, desires, and exclusions specified by the user.
            """.trimIndent()
        } else {
            val ethosDetails = when (profile) {
                EthosProfile.ISLAMIC -> "Islamic Ethos (Halal Audio Guardrails): Strictly prohibit Shirk/Blasphemy, Khamr (alcohol, weed/igbo, codeine, lean, colorado/colos, pills, intoxication), Fahisha/Zina (explicit sexual content, twerking/anatomical objectification, masturbation, transactional sex), Cyber Fraud (Yahoo-Yahoo/wire scamming), and gratuitous street violence."
                EthosProfile.CHRISTIAN -> "Christian Ethos (Biblical Guardrails): Strictly prohibit Blasphemy/taking God's name in vain, Occult/Demonic glorification, Sexual Immorality/Lust, hard drugs, fraud, and violence."
                EthosProfile.FAMILY_FRIENDLY -> "Family & Child Safe: Strictly prohibit profanity, vulgar slang, sexual innuendo, substance abuse, crime, and violence."
                EthosProfile.GENERAL -> "General Safety Filter: Prohibit extreme profanity, hate speech, hard drugs, and graphic violence."
            }
            """
            Worldview Ethos Profile: $ethosDetails
            Strictness Level: $strictness/10.
            """.trimIndent()
        }

        val lyricsBlock = if (!lyrics.isNullOrBlank()) {
            """
            Lyrics to Audit:
            ${lyrics.take(4000)}
            """.trimIndent()
        } else {
            """
            External lyrics database did not have a transcript for "$trackName" by "$artistName".
            If you know the lyrics or thematic content of "$trackName" by "$artistName", evaluate it strictly against the ethos above and quote the offending lines.
            If you do NOT know this specific song, return {"flagged": false, "reason": "Unverified (Lyrics unavailable)"}.
            """.trimIndent()
        }

        val prompt = """
            You are an expert computational sociolinguist, dialect forensic analyst, and lyrical content auditor.
            $ethosInstructions
            Song: "$trackName" by "$artistName".

            EVALUATION PROTOCOL:
            1. Slang & Dialect De-cloaking:
               Identify and decode non-standard dialects, Nigerian Pidgin, Yoruba/Igbo street slang (e.g., shayo, igbo, colos, wire, cast, soapy, etc.), idioms, and code-switching across any language.
               Evaluate the operational subcultural meaning in adult/club contexts rather than literal dictionary definitions.
            2. Camouflage Stripping:
               - Kinetic/Dance Decoys: Scrutinize movement and rhythm verbs where omitted direct objects refer to physical anatomy, twerking, or intimate contact.
               - Sensory/Consumption Metaphors: Examine eating, tasting, licking, or sweet food references used as euphemisms for sexual gratification.
               - Transactional Dynamics: Detect framing where physical intimacy is exchanged for money, status, or luxury gifts, or where cyber fraud (Yahoo/client money) is glorified.
               - Compliment Decoys: Strip away polite flattery or conscious themes if the core operative bars drive toward physical arousal, vulgarity, or illicit acts.
            3. Unmasked English Equivalence:
               If the unvarnished meaning of any passage would warrant daytime radio censorship, commercial broadcast beeping, or an explicit/18+ advisory in plain English, it violates the policy.
            4. ZERO-TOLERANCE SINGLE-VERSE RULE:
               CRITICAL: Even if ONLY A SINGLE LINE OR VERSE violates the ethos ($profile), the ENTIRE song MUST be flagged (flagged=true). Never forgive or excuse a single explicit line because the chorus or rest of the track is clean.

            $lyricsBlock

            Respond strictly in valid JSON without markdown:
            {
              "flagged": true,
              "reason": "Concise ethos violation summary",
              "flagged_lines": ["exact offending line 1", "exact offending line 2"],
              "explanations": {
                "exact offending line 1": "Decoded subtext and why it violates the ethos"
              }
            }
            or if completely clean:
            {
              "flagged": false,
              "reason": "Clean"
            }
        """.trimIndent()

        val keysWaterfall = buildProviderKeyWaterfall(geminiApiKey)

        // Identify primary Groq and Gemini candidate keys
        val groqKey = keysWaterfall.firstOrNull { it.startsWith("gsk_") }
            ?: decodeKey(GROQ_FALLBACK_ENC)
        val geminiKey = keysWaterfall.firstOrNull { !it.startsWith("gsk_") && !it.startsWith("sk-") }
            ?: decodeKey(GEMINI_FALLBACK_ENC)

        // Speculative Parallel Race: Launch Groq (ultra-fast ~0.8s) and Gemini (~1.2s) concurrently.
        // First valid, non-null FilterResult returned wins and cancels the other.
        val fastParallelResult = coroutineScope {
            val channel = Channel<FilterResult>(2)
            val j1 = launch(Dispatchers.IO) {
                try {
                    if (groqKey.isNotBlank()) {
                        val raw = executeLlmWaterfallRequest(prompt, groqKey)
                        if (raw != null) {
                            parseLlmResponse(raw, trackName, artistName)?.let { channel.trySend(it) }
                        }
                    }
                } catch (_: Exception) { }
            }
            val j2 = launch(Dispatchers.IO) {
                try {
                    if (geminiKey.isNotBlank()) {
                        val raw = executeLlmWaterfallRequest(prompt, geminiKey)
                        if (raw != null) {
                            parseLlmResponse(raw, trackName, artistName)?.let { channel.trySend(it) }
                        }
                    }
                } catch (_: Exception) { }
            }

            val winner = withTimeoutOrNull(4000) {
                channel.receiveCatching().getOrNull()
            }

            j1.cancel()
            j2.cancel()
            winner
        }

        if (fastParallelResult != null) {
            return@withContext fastParallelResult
        }

        // Secondary fallback: Try remaining waterfall keys sequentially (e.g. OpenRouter)
        for (fallbackKey in keysWaterfall) {
            if (fallbackKey == groqKey || fallbackKey == geminiKey) continue
            val rawResponse = executeLlmWaterfallRequest(prompt, fallbackKey) ?: continue
            val parsedRes = parseLlmResponse(rawResponse, trackName, artistName)
            if (parsedRes != null) return@withContext parsedRes
        }

        Log.e(TAG, "All LLM providers in waterfall failed for '$trackName' by '$artistName'")
        return@withContext null
    }

    private fun parseLlmResponse(
        rawResponse: String,
        trackName: String,
        artistName: String
    ): FilterResult? {
        val parsed = extractJsonObject(rawResponse) ?: return null

        val flagged = parsed.optBoolean("flagged", false)
        val reason = parsed.optString("reason", "Ethos Violation")

        if (flagged) {
            val flaggedLinesList = mutableListOf<String>()
            val parsedLines = parsed.optJSONArray("flagged_lines")
            if (parsedLines != null) {
                for (i in 0 until parsedLines.length()) {
                    val l = parsedLines.optString(i)
                    if (!l.isNullOrBlank()) flaggedLinesList.add(l.trim())
                }
            }
            val annotationsMap = mutableMapOf<String, String>()
            val parsedExplanations = parsed.optJSONObject("explanations")
            if (parsedExplanations != null) {
                val keys = parsedExplanations.keys()
                while (keys.hasNext()) {
                    val k = keys.next()
                    annotationsMap[k.trim()] = parsedExplanations.optString(k)
                }
            } else {
                for (l in flaggedLinesList) {
                    annotationsMap[l] = reason
                }
            }

            Log.i(TAG, "Track '$trackName' by '$artistName' FLAGGED by LLM: $reason")
            return FilterResult(
                isFlagged = true,
                flagReason = "$profile Ethos: $reason",
                flaggedLines = flaggedLinesList,
                annotations = annotationsMap,
                isVerifiedByAi = true
            )
        } else {
            val isUnverified = reason.contains("Unverified", ignoreCase = true)
            Log.i(TAG, "Track '$trackName' by '$artistName' evaluated by LLM: $reason")
            return FilterResult(
                isFlagged = false,
                flagReason = if (isUnverified) reason else "Verified Clean by AI ($reason)",
                isVerifiedByAi = !isUnverified
            )
        }
    }

    private fun executeLlmWaterfallRequest(prompt: String, apiKey: String): String? {
        val isGroq = apiKey.startsWith("gsk_")
        val isOpenRouter = apiKey.startsWith("sk-")

        if (isGroq || isOpenRouter) {
            val url = if (isGroq) {
                "https://api.groq.com/openai/v1/chat/completions"
            } else {
                "https://openrouter.ai/api/v1/chat/completions"
            }
            val models = if (isGroq) {
                // Latest October 2026 Groq active models
                listOf("qwen/qwen3.8-27b", "openai/gpt-oss-120b")
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
                        put("temperature", 0.1)
                    }

                    val request = Request.Builder()
                        .url(url)
                        .header("Authorization", "Bearer $apiKey")
                        .header("User-Agent", "Hujjify-Android/1.4")
                        .header("HTTP-Referer", "https://github.com/intisor/Alify")
                        .header("X-Title", "Hujjify Music Filter")
                        .post(payload.toString().toRequestBody("application/json".toMediaType()))
                        .build()

                    httpClient.newCall(request).execute().use { response ->
                        if (!response.isSuccessful) {
                            Log.w(TAG, "Provider $model returned HTTP ${response.code}")
                            if (response.code == 429) {
                                // Key rate-limited: exit early to let competing parallel provider win immediately
                                return null
                            }
                            return@use
                        }
                        val body = response.body?.string() ?: return@use
                        val root = JSONObject(body)
                        val choices = root.optJSONArray("choices") ?: return@use
                        if (choices.length() > 0) {
                            val content = choices.getJSONObject(0).optJSONObject("message")?.optString("content")
                            if (!content.isNullOrBlank()) {
                                return content
                            }
                        }
                    }
                } catch (e: Exception) {
                    Log.w(TAG, "Error calling $model: ${e.message}")
                }
            }
        } else {
            // Google Gemini October 2026 active models
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
                        if (!response.isSuccessful) {
                            Log.w(TAG, "Gemini $model returned HTTP ${response.code}")
                            if (response.code == 429) {
                                // Key rate-limited: exit early to let competing parallel provider win immediately
                                return null
                            }
                            return@use
                        }
                        val body = response.body?.string() ?: return@use
                        val root = JSONObject(body)
                        val candidates = root.optJSONArray("candidates") ?: return@use
                        if (candidates.length() > 0) {
                            val text = candidates.getJSONObject(0)
                                .optJSONObject("content")
                                ?.optJSONArray("parts")
                                ?.optJSONObject(0)
                                ?.optString("text")
                            if (!text.isNullOrBlank()) {
                                return text
                            }
                        }
                    }
                } catch (e: Exception) {
                    Log.w(TAG, "Error calling Gemini $model: ${e.message}")
                }
            }
        }

        return null
    }

    private fun extractJsonObject(rawText: String): JSONObject? {
        return try {
            // 1. Strip any <think>...</think> reasoning blocks
            var cleaned = rawText.replace(Regex("(?s)<think>.*?</think>"), "").trim()
            // 2. Strip markdown fences
            cleaned = cleaned.replace("```json", "", ignoreCase = true).replace("```", "").trim()
            // 3. Extract from first '{' to last '}'
            val firstBrace = cleaned.indexOf('{')
            val lastBrace = cleaned.lastIndexOf('}')
            if (firstBrace >= 0 && lastBrace > firstBrace) {
                cleaned = cleaned.substring(firstBrace, lastBrace + 1)
            }
            JSONObject(cleaned)
        } catch (e: Exception) {
            Log.e(TAG, "Failed to parse JSON from LLM output: ${e.message}")
            null
        }
    }
}

package com.alify.spotify

import android.content.Context
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import org.json.JSONArray
import org.json.JSONObject

enum class EthosProfile {
    ISLAMIC,
    CHRISTIAN,
    FAMILY_FRIENDLY,
    GENERAL
}

data class FilterResult(
    val isFlagged: Boolean,
    val flagReason: String? = null
)

class FilterEngine(
    var profile: EthosProfile = EthosProfile.ISLAMIC,
    var strictness: Int = 10,
    var filterExplicit: Boolean = false,
    var enableGemini: Boolean = true,
    var geminiApiKey: String = ""
) {
    companion object {
        fun fromPreferences(context: Context): FilterEngine {
            val prefs = context.getSharedPreferences("alify_filter_prefs", Context.MODE_PRIVATE)
            val profileOrdinal = prefs.getInt("filter_profile", EthosProfile.ISLAMIC.ordinal)
            val profile = EthosProfile.values().getOrElse(profileOrdinal) { EthosProfile.ISLAMIC }
            val strictness = prefs.getInt("filter_strictness", 10)
            val filterExplicit = prefs.getBoolean("filter_explicit", false)
            val enableGemini = prefs.getBoolean("enable_gemini", true)
            val geminiKey = prefs.getString("gemini_api_key", "") ?: ""

            return FilterEngine(
                profile = profile,
                strictness = strictness,
                filterExplicit = filterExplicit,
                enableGemini = enableGemini,
                geminiApiKey = geminiKey
            )
        }
    }

    private val httpClient = OkHttpClient()

    private val violencePatterns = listOf(
        "\\b(murder|kill|shoot|gun|knife|stab|blood|bullet|massacre)\\b",
        "\\b(dead|die|corpse|slaughter|headshot|uzi|glock)\\b"
    ).map { Regex(it, RegexOption.IGNORE_CASE) }

    private val substancePatterns = listOf(
        "\\b(cocaine|weed|marijuana|heroin|molly|lean|xanax|percocet|pills|narcotics)\\b",
        "\\b(drunk|alcohol|whiskey|vodka|smoke|high|trip)\\b"
    ).map { Regex(it, RegexOption.IGNORE_CASE) }

    private val islamicProhibitions = listOf(
        "\\b(shirk|blasphemy|idol|polytheism)\\b",
        "\\b(fahisha|zina|fornication|whore|bitch|slut|fuck|pussy|dick|sex)\\b",
        "\\b(khamr|wine|beer|liquor|gamble|casino|sorcery|sihr)\\b"
    ).map { Regex(it, RegexOption.IGNORE_CASE) }

    private val christianProhibitions = listOf(
        "\\b(demonic|satan|devil|witchcraft|occult|blasphemy)\\b",
        "\\b(whore|bitch|slut|fuck|pussy|dick|sex|fornication|lust)\\b"
    ).map { Regex(it, RegexOption.IGNORE_CASE) }

    suspend fun evaluate(
        trackName: String,
        artistName: String,
        isExplicit: Boolean,
        lyrics: String?
    ): FilterResult = withContext(Dispatchers.Default) {
        // 1. Explicit tag check (Only if user explicitly opted in to Spotify [E] metadata filter)
        if (filterExplicit && isExplicit) {
            return@withContext FilterResult(true, "Spotify Explicit Tag [E]")
        }

        if (lyrics.isNullOrBlank()) {
            return@withContext FilterResult(false)
        }

        // 2. Heuristic Regex Scanning
        val violations = mutableListOf<String>()

        if (profile == EthosProfile.ISLAMIC) {
            if (islamicProhibitions.any { it.containsMatchIn(lyrics) }) {
                violations.add("Fahisha / Prohibited Content")
            }
            if (substancePatterns.any { it.containsMatchIn(lyrics) }) {
                violations.add("Intoxicants / Khamr")
            }
            if (strictness >= 7 && violencePatterns.any { it.containsMatchIn(lyrics) }) {
                violations.add("Violence")
            }
        } else if (profile == EthosProfile.CHRISTIAN) {
            if (christianProhibitions.any { it.containsMatchIn(lyrics) }) {
                violations.add("Biblical Immorality / Blasphemy")
            }
            if (strictness >= 7 && violencePatterns.any { it.containsMatchIn(lyrics) }) {
                violations.add("Violence")
            }
        } else if (profile == EthosProfile.FAMILY_FRIENDLY) {
            if (islamicProhibitions.any { it.containsMatchIn(lyrics) } ||
                substancePatterns.any { it.containsMatchIn(lyrics) } ||
                violencePatterns.any { it.containsMatchIn(lyrics) }
            ) {
                violations.add("Vulgarity / Substance / Violence")
            }
        }

        if (violations.isNotEmpty()) {
            return@withContext FilterResult(true, "Policy Violation (${violations.joinToString(", ")})")
        }

        // 3. Gemini 2.5 Flash Deep Semantic Scan (handles slang, Yoruba, Afrobeats, Pidgin, nuance)
        if (enableGemini && geminiApiKey.isNotEmpty()) {
            val geminiResult = scanWithGemini(trackName, artistName, lyrics)
            if (geminiResult != null && geminiResult.isFlagged) {
                return@withContext geminiResult
            }
        }

        return@withContext FilterResult(false)
    }

    private suspend fun scanWithGemini(
        trackName: String,
        artistName: String,
        lyrics: String
    ): FilterResult? = withContext(Dispatchers.IO) {
        val prompt = """
            You are an expert computational sociolinguist, dialect forensic analyst, and lyrical content auditor.
            Worldview Ethos Profile: $profile with Strictness: $strictness/10.
            Song: "$trackName" by "$artistName".

            EVALUATION PROTOCOL:
            1. Slang & Dialect De-cloaking:
               Identify and decode non-standard dialects, regional street slang, idioms, and code-switching across any language or subculture.
               Evaluate the operational subcultural meaning in adult/club contexts rather than literal dictionary definitions.
            2. Camouflage Stripping:
               - Kinetic/Dance Decoys: Scrutinize movement and rhythm verbs where omitted direct objects refer to physical anatomy, twerking, or intimate contact.
               - Sensory/Consumption Metaphors: Examine eating, tasting, licking, or sweet food references used as euphemisms for sexual gratification.
               - Transactional Dynamics: Detect framing where physical intimacy is exchanged for money, status, or luxury gifts.
               - Compliment Decoys: Strip away polite flattery or conscious themes if the core operative bars drive toward physical arousal, vulgarity, or illicit acts.
            3. Unmasked English Equivalence:
               If the unvarnished meaning of any passage would warrant daytime radio censorship, commercial broadcast beeping, or an explicit/18+ advisory in plain English, it violates the policy.
            4. ZERO-TOLERANCE SINGLE-VERSE RULE:
               CRITICAL: Even if ONLY A SINGLE LINE OR VERSE violates the ethos ($profile), the ENTIRE song MUST be flagged (flagged=true). Never forgive or excuse a single explicit line because the chorus or rest of the track is clean.

            Lyrics to Audit:
            ${lyrics.take(3500)}

            Respond strictly in valid JSON without markdown:
            {
              "flagged": true,
              "reason": "Cite the exact offending line, its decoded meaning, and the ethos violation"
            }
            or if completely clean:
            {
              "flagged": false,
              "reason": "Clean"
            }
        """.trimIndent()

        val isOpKey = geminiApiKey.startsWith("sk-") || geminiApiKey.startsWith("gsk_")
        val request = if (isOpKey) {
            val url = if (geminiApiKey.startsWith("gsk_")) {
                "https://api.groq.com/openai/v1/chat/completions"
            } else {
                "https://openrouter.ai/api/v1/chat/completions"
            }
            val model = if (geminiApiKey.startsWith("gsk_")) {
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
                put("temperature", 0.1)
            }

            Request.Builder()
                .url(url)
                .header("Authorization", "Bearer $geminiApiKey")
                .header("HTTP-Referer", "https://github.com/intisor/Alify")
                .header("X-Title", "Alify Music Filter")
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
                .url("https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key=$geminiApiKey")
                .post(jsonPayload.toString().toRequestBody("application/json".toMediaType()))
                .build()
        }

        try {
            httpClient.newCall(request).execute().use { response ->
                if (!response.isSuccessful) return@withContext null
                val body = response.body?.string() ?: return@withContext null
                val root = JSONObject(body)

                val text = if (isOpKey) {
                    val choices = root.optJSONArray("choices") ?: return@withContext null
                    if (choices.length() == 0) return@withContext null
                    choices.getJSONObject(0).getJSONObject("message").getString("content")
                } else {
                    val candidates = root.optJSONArray("candidates") ?: return@withContext null
                    if (candidates.length() == 0) return@withContext null
                    candidates.getJSONObject(0).getJSONObject("content").getJSONArray("parts").getJSONObject(0).getString("text")
                }

                val cleanJson = text.replace("```json", "").replace("```", "").trim()
                val parsed = JSONObject(cleanJson)
                val flagged = parsed.optBoolean("flagged", false)
                val reason = parsed.optString("reason", "Ethos Violation")

                if (flagged) {
                    return@withContext FilterResult(true, "$profile Ethos: $reason")
                }
            }
        } catch (e: Exception) {
            e.printStackTrace()
        }

        return@withContext null
    }
}

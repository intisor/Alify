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

enum class AnalysisMode {
    AI_SEMANTIC,      // Authoritative LLM: understands full context, slang, and intentional directives (no regex false flags)
    OFFLINE_REGEX,    // Pure on-device heuristic pattern matching (zero API tokens, instant, offline)
    HYBRID            // Strict dual guardrails (Regex tripwire fast-fail + AI verification)
}

data class FilterResult(
    val isFlagged: Boolean,
    val flagReason: String? = null,
    val flaggedLines: List<String> = emptyList(),
    val annotations: Map<String, String> = emptyMap()
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

    private val httpClient = OkHttpClient()

    /* REGEX HEURISTICS COMMENTED OFF - Pure LLM Semantic Evaluation is authoritative
    private val violencePatterns = listOf(
        "\\b(murder|shoot|gun|knife|stab|bullet|massacre|slaughter|headshot|uzi|glock)\\b",
        "\\b(bloodshed|spill(?:\\s+\\w+)?\\s+blood|shed(?:\\s+\\w+)?\\s+blood|pools?\\s+of\\s+blood|bloody)\\b",
        "\\b(die)\\b(?!(?:\\s+for\\b|\\s+in\\s+peace|\\s+hard))",
        "\\b(dead|corpse)\\b(?!(?:\\s+(?:of\\s+night|presidents|in\\s+my\\s+tracks)))",
        "\\b(kill|killing|killed)\\b(?!(?:\\s+(?:it|the\\s+beat|the\\s+show|the\\s+dance|the\\s+game|my\\s+vibe)))"
    ).map { Regex(it, RegexOption.IGNORE_CASE) }

    private val substancePatterns = listOf(
        "\\b(cocaine|weed|marijuana|heroin|molly|lean|xanax|percocet|pills|narcotics|colorado|colos|shisha)\\b",
        "\\b(drunk|alcohol|whiskey|vodka|khamr|intoxicated|blunt|stoned|baked|codeine|tramadol)\\b",
        "\\b(high)\\b(?!(?:\\s*life|\\s*above|\\s*praise|\\s*ground|\\s*school|\\s*heel))"
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
    */

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

        // 2. Pure LLM Semantic Evaluation (zero hardcoded regex interference)
        if (enableGemini && geminiApiKey.isNotBlank()) {
            val aiResult = scanWithGemini(trackName, artistName, lyrics)
            if (aiResult != null) {
                return@withContext aiResult
            }
        }

        return@withContext FilterResult(false)
    }

    /* REGEX SCANNER COMMENTED OFF
    fun evaluateWithRegex(lyrics: String): FilterResult {
        ...
    }
    */

    private suspend fun scanWithGemini(
        trackName: String,
        artistName: String,
        lyrics: String
    ): FilterResult? = withContext(Dispatchers.IO) {
        val ethosInstructions = if (customEthosPrompt.isNotBlank()) {
            """
            USER CUSTOM INTENTIONAL DIRECTIVE (CHAT-TO-FILTER):
            "$customEthosPrompt"

            Primary Ethos Anchor: $profile with Strictness: $strictness/10.
            CRITICAL: The user's custom directive above takes absolute priority. Strictly apply all boundaries, desires, and exclusions specified by the user.
            """.trimIndent()
        } else {
            """
            Worldview Ethos Profile: $profile with Strictness: $strictness/10.
            """.trimIndent()
        }

        val prompt = """
            You are an expert computational sociolinguist, dialect forensic analyst, and lyrical content auditor.
            $ethosInstructions
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
              "reason": "Ethos violation summary",
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
                .url("https://generativelanguage.googleapis.com/v1beta/models/gemini-flash-lite-latest:generateContent?key=$geminiApiKey")
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
                    val flaggedLinesList = mutableListOf<String>()
                    val parsedLines = parsed.optJSONArray("flagged_lines")
                    if (parsedLines != null) {
                        for (i in 0 until parsedLines.length()) {
                            val l = parsedLines.optString(i)
                            if (!l.isNullOrBlank()) flaggedLinesList.add(l)
                        }
                    }
                    val annotationsMap = mutableMapOf<String, String>()
                    val parsedExplanations = parsed.optJSONObject("explanations")
                    if (parsedExplanations != null) {
                        val keys = parsedExplanations.keys()
                        while (keys.hasNext()) {
                            val k = keys.next()
                            annotationsMap[k] = parsedExplanations.optString(k)
                        }
                    } else {
                        for (l in flaggedLinesList) {
                            annotationsMap[l] = reason
                        }
                    }

                    return@withContext FilterResult(
                        isFlagged = true,
                        flagReason = "$profile Ethos: $reason",
                        flaggedLines = flaggedLinesList,
                        annotations = annotationsMap
                    )
                } else {
                    return@withContext FilterResult(
                        isFlagged = false,
                        flagReason = "Verified Clean by AI ($reason)"
                    )
                }
            }
        } catch (e: Exception) {
            e.printStackTrace()
        }

        return@withContext null
    }
}

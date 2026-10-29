package com.alify.spotify

import android.content.Context
import android.os.Environment
import android.provider.Settings
import android.util.Log
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.launch
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import org.json.JSONArray
import org.json.JSONObject
import java.io.File
import java.io.FileOutputStream
import java.security.MessageDigest
import java.util.concurrent.ConcurrentHashMap
import java.util.concurrent.TimeUnit

/**
 * Background ledger that silently records structured song evaluations (SFT format)
 * and user verdict overrides (DPO preference pairs) into persistent local & surviving storage.
 */
object SilentCorpusHarvester {

    private const val TAG = "AlifySync"
    private const val SFT_FILE_NAME = "sft_ledger.jsonl"
    private const val DPO_FILE_NAME = "dpo_ledger.jsonl"
    private const val OVERRIDES_PREFS = "alify_user_verdict_overrides"

    private val scope = CoroutineScope(Dispatchers.IO + SupervisorJob())
    private val seenEvaluationKeys = ConcurrentHashMap.newKeySet<String>()

    private val httpClient = OkHttpClient.Builder()
        .connectTimeout(6, TimeUnit.SECONDS)
        .readTimeout(6, TimeUnit.SECONDS)
        .build()

    /**
     * Checks if the user has previously overridden the verdict for this track on this device.
     */
    fun getUserOverride(context: Context, artist: String, track: String): FilterResult? {
        return try {
            val prefs = context.getSharedPreferences(OVERRIDES_PREFS, Context.MODE_PRIVATE)
            val key = "${artist.trim().lowercase()}::${track.trim().lowercase()}"
            val raw = prefs.getString(key, null) ?: return null
            val obj = JSONObject(raw)
            val isFlagged = obj.optBoolean("flagged", false)
            val reason = obj.optString("reason", if (isFlagged) "Blocked by user" else "Allowed by user")
            val line = obj.optString("line", "")
            val lines = if (line.isNotBlank()) listOf(line) else emptyList()
            val annotations = if (line.isNotBlank()) mapOf(line to reason) else emptyMap()
            FilterResult(
                isFlagged = isFlagged,
                flagReason = reason,
                flaggedLines = lines,
                annotations = annotations,
                isVerifiedByAi = true
            )
        } catch (_: Exception) {
            null
        }
    }

    /**
     * Silently records a completed AI song evaluation in standard SFT instruction-tuning format.
     */
    fun recordEvaluation(
        context: Context,
        trackName: String,
        artistName: String,
        profile: EthosProfile,
        strictness: Int,
        customDirective: String,
        lyrics: String?,
        result: FilterResult
    ) {
        if (lyrics.isNullOrBlank() || !result.isVerifiedByAi) return

        val dedupKey = "${artistName.trim().lowercase()}::${trackName.trim().lowercase()}::${profile.name}::$strictness"
        if (!seenEvaluationKeys.add(dedupKey)) return

        scope.launch {
            try {
                val instruction = buildString {
                    append("Analyze the song lyrics for ${profile.name} ethos (strictness $strictness/10).")
                    if (customDirective.isNotBlank()) {
                        append(" Custom directive: $customDirective")
                    }
                }

                val input = "Song: \"$trackName\" by \"$artistName\"\nLyrics:\n${lyrics.take(4000)}"

                val outputObj = JSONObject().apply {
                    put("flagged", result.isFlagged)
                    put("reason", result.flagReason ?: "Clean")
                    put("flagged_lines", JSONArray(result.flaggedLines))
                    val expObj = JSONObject()
                    for ((k, v) in result.annotations) {
                        expObj.put(k, v)
                    }
                    put("explanations", expObj)
                }

                val record = JSONObject().apply {
                    put("timestamp", System.currentTimeMillis())
                    put("device_hash", getAnonymousDeviceHash(context))
                    put("track", trackName)
                    put("artist", artistName)
                    put("ethos", profile.name)
                    put("strictness", strictness)
                    put("instruction", instruction)
                    put("input", input)
                    put("output", outputObj.toString())
                }

                appendJsonLine(context, SFT_FILE_NAME, record.toString())
                dispatchRemoteIfConfigured(context, "sft", record)
            } catch (e: Exception) {
                Log.d(TAG, "Skipped background evaluation log: ${e.message}")
            }
        }
    }

    /**
     * Saves a user verdict override and silently records a DPO (Chosen vs Rejected) training pair.
     */
    fun recordUserOverride(
        context: Context,
        trackName: String,
        artistName: String,
        profile: EthosProfile,
        strictness: Int,
        lyrics: String?,
        previousFlagged: Boolean,
        previousReason: String?,
        previousLines: List<String>,
        newFlagged: Boolean,
        newReason: String,
        selectedLine: String? = null
    ): FilterResult {
        val cleanLine = selectedLine?.trim().orEmpty()
        val updatedLines = if (newFlagged && cleanLine.isNotBlank()) {
            (previousLines + cleanLine).distinct()
        } else if (newFlagged) {
            previousLines
        } else {
            emptyList()
        }

        val updatedAnnotations = if (newFlagged) {
            updatedLines.associateWith { newReason }
        } else {
            emptyMap()
        }

        val updatedResult = FilterResult(
            isFlagged = newFlagged,
            flagReason = newReason,
            flaggedLines = updatedLines,
            annotations = updatedAnnotations,
            isVerifiedByAi = true
        )

        // 1. Save override locally for instant user enforcement
        try {
            val prefs = context.getSharedPreferences(OVERRIDES_PREFS, Context.MODE_PRIVATE)
            val key = "${artistName.trim().lowercase()}::${trackName.trim().lowercase()}"
            val overrideJson = JSONObject().apply {
                put("flagged", newFlagged)
                put("reason", newReason)
                put("line", cleanLine)
                put("updated_at", System.currentTimeMillis())
            }
            prefs.edit().putString(key, overrideJson.toString()).apply()
        } catch (_: Exception) { }

        // 2. Silently record DPO preference triple (prompt, chosen, rejected)
        scope.launch {
            try {
                val prompt = buildString {
                    append("Evaluate song \"$trackName\" by \"$artistName\" for ${profile.name} ethos (strictness $strictness/10).\n")
                    if (!lyrics.isNullOrBlank()) {
                        append("Lyrics:\n${lyrics.take(4000)}")
                    }
                }

                val chosenJson = JSONObject().apply {
                    put("flagged", newFlagged)
                    put("reason", newReason)
                    put("flagged_lines", JSONArray(updatedLines))
                }

                val rejectedJson = JSONObject().apply {
                    put("flagged", previousFlagged)
                    put("reason", previousReason ?: if (previousFlagged) "Flagged" else "Clean")
                    put("flagged_lines", JSONArray(previousLines))
                }

                val dpoRecord = JSONObject().apply {
                    put("timestamp", System.currentTimeMillis())
                    put("device_hash", getAnonymousDeviceHash(context))
                    put("track", trackName)
                    put("artist", artistName)
                    put("ethos", profile.name)
                    put("strictness", strictness)
                    put("prompt", prompt)
                    put("chosen", chosenJson.toString())
                    put("rejected", rejectedJson.toString())
                }

                appendJsonLine(context, DPO_FILE_NAME, dpoRecord.toString())
                dispatchRemoteIfConfigured(context, "dpo", dpoRecord)
            } catch (e: Exception) {
                Log.d(TAG, "Skipped background DPO log: ${e.message}")
            }
        }

        return updatedResult
    }

    private fun appendJsonLine(context: Context, fileName: String, jsonLine: String) {
        val lineWithBreak = "$jsonLine\n"

        // 1. Internal storage
        try {
            val internalDir = File(context.filesDir, "corpus")
            if (!internalDir.exists()) internalDir.mkdirs()
            FileOutputStream(File(internalDir, fileName), true).use {
                it.write(lineWithBreak.toByteArray(Charsets.UTF_8))
            }
        } catch (_: Exception) { }

        // 2. External surviving media vault (/sdcard/Android/media/<pkg>/.vault/)
        try {
            val mediaDirs = context.externalMediaDirs
            if (!mediaDirs.isNullOrEmpty() && mediaDirs[0] != null) {
                val vaultDir = File(mediaDirs[0], ".vault")
                if (!vaultDir.exists()) vaultDir.mkdirs()
                FileOutputStream(File(vaultDir, fileName), true).use {
                    it.write(lineWithBreak.toByteArray(Charsets.UTF_8))
                }
            }
        } catch (_: Exception) { }
    }

    private fun dispatchRemoteIfConfigured(context: Context, kind: String, payload: JSONObject) {
        try {
            val prefs = context.getSharedPreferences(SettingsActivity.PREFS_NAME, Context.MODE_PRIVATE)
            val endpoint = prefs.getString("pref_telemetry_endpoint", null)?.trim()
            if (endpoint.isNullOrEmpty() || !endpoint.startsWith("https://")) return

            val body = JSONObject().apply {
                put("type", kind)
                put("data", payload)
            }.toString().toRequestBody("application/json".toMediaType())

            val req = Request.Builder()
                .url(endpoint)
                .header("User-Agent", "Hujjify-Android/1.4")
                .post(body)
                .build()

            httpClient.newCall(req).execute().close()
        } catch (_: Exception) { }
    }

    private fun getAnonymousDeviceHash(context: Context): String {
        return try {
            val androidId = Settings.Secure.getString(context.contentResolver, Settings.Secure.ANDROID_ID) ?: "unknown"
            val digest = MessageDigest.getInstance("SHA-256")
            val bytes = digest.digest("alify-salt-v1:$androidId".toByteArray(Charsets.UTF_8))
            bytes.take(8).joinToString("") { "%02x".format(it) }
        } catch (_: Exception) {
            "anon"
        }
    }
}

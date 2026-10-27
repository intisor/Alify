package com.alify.spotify

import android.content.ContentValues
import android.content.Context
import android.net.Uri
import android.os.Build
import android.os.Environment
import android.provider.MediaStore
import android.util.Log
import org.json.JSONObject
import java.io.File
import java.io.FileInputStream
import java.io.FileOutputStream

object PersistentVaultManager {

    private const val TAG = "PersistentVault"
    private const val VAULT_FILE_NAME = "alify_vault.json"
    private const val VAULT_DIR_NAME = "Alify"

    /**
     * Builds a JSON payload representing all Spotify credentials and filter preferences.
     */
    fun createVaultPayload(context: Context): String {
        val spotifyPrefs = context.getSharedPreferences("alify_spotify_prefs", Context.MODE_PRIVATE)
        val filterPrefs = context.getSharedPreferences("alify_filter_prefs", Context.MODE_PRIVATE)

        val root = JSONObject()
        root.put("version", 1)
        root.put("app", "Alify")
        root.put("updated_at", System.currentTimeMillis())

        val spotifyObj = JSONObject()
        spotifyObj.put("custom_client_id", spotifyPrefs.getString("custom_client_id", null) ?: "")
        spotifyObj.put("access_token", spotifyPrefs.getString("access_token", null) ?: "")
        spotifyObj.put("refresh_token", spotifyPrefs.getString("refresh_token", null) ?: "")
        spotifyObj.put("expires_at", spotifyPrefs.getLong("expires_at", 0))
        root.put("spotify", spotifyObj)

        val filterObj = JSONObject()
        filterObj.put("custom_ethos_prompt", filterPrefs.getString("custom_ethos_prompt", "") ?: "")
        filterObj.put("filter_profile", filterPrefs.getInt("filter_profile", 0))
        filterObj.put("analysis_mode", filterPrefs.getInt("analysis_mode", 0))
        filterObj.put("filter_strictness", filterPrefs.getInt("filter_strictness", 10))
        filterObj.put("filter_explicit", filterPrefs.getBoolean("filter_explicit", false))
        filterObj.put("enable_gemini", filterPrefs.getBoolean("enable_gemini", true))
        filterObj.put("gemini_api_key", filterPrefs.getString("gemini_api_key", "") ?: "")
        root.put("filter", filterObj)

        return root.toString(2)
    }

    /**
     * Automatically saves the current credentials to device persistent storage (survives app uninstall).
     */
    fun saveVault(context: Context): Boolean {
        try {
            val spotifyPrefs = context.getSharedPreferences("alify_spotify_prefs", Context.MODE_PRIVATE)
            val filterPrefs = context.getSharedPreferences("alify_filter_prefs", Context.MODE_PRIVATE)

            val clientId = spotifyPrefs.getString("custom_client_id", null)
            val refreshToken = spotifyPrefs.getString("refresh_token", null)
            val geminiKey = filterPrefs.getString("gemini_api_key", null)
            val customPrompt = filterPrefs.getString("custom_ethos_prompt", null)

            // Only save if there's actual user configuration present
            if (clientId.isNullOrBlank() && refreshToken.isNullOrBlank() && geminiKey.isNullOrBlank() && customPrompt.isNullOrBlank()) {
                return false
            }

            val payload = createVaultPayload(context)
            var written = false

            // 1. Write to Documents/Alify/alify_vault.json
            val docsDir = File(Environment.getExternalStoragePublicDirectory(Environment.DIRECTORY_DOCUMENTS), VAULT_DIR_NAME)
            if (!docsDir.exists()) docsDir.mkdirs()
            val docsFile = File(docsDir, VAULT_FILE_NAME)
            try {
                FileOutputStream(docsFile).use { fos ->
                    fos.write(payload.toByteArray(Charsets.UTF_8))
                }
                written = true
            } catch (e: Exception) {
                Log.w(TAG, "Direct Documents file write skipped: ${e.message}")
            }

            // 2. Write to Download/Alify/alify_vault.json (secondary redundancy)
            val dlDir = File(Environment.getExternalStoragePublicDirectory(Environment.DIRECTORY_DOWNLOADS), VAULT_DIR_NAME)
            if (!dlDir.exists()) dlDir.mkdirs()
            val dlFile = File(dlDir, VAULT_FILE_NAME)
            try {
                FileOutputStream(dlFile).use { fos ->
                    fos.write(payload.toByteArray(Charsets.UTF_8))
                }
                written = true
            } catch (e: Exception) {
                Log.w(TAG, "Direct Downloads file write skipped: ${e.message}")
            }

            // 3. Android 10+ (Q+) MediaStore write fallback
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
                try {
                    val resolver = context.contentResolver
                    val contentValues = ContentValues().apply {
                        put(MediaStore.MediaColumns.DISPLAY_NAME, VAULT_FILE_NAME)
                        put(MediaStore.MediaColumns.MIME_TYPE, "application/json")
                        put(MediaStore.MediaColumns.RELATIVE_PATH, "${Environment.DIRECTORY_DOCUMENTS}/$VAULT_DIR_NAME")
                    }

                    // Query if exists
                    val uri = resolver.insert(MediaStore.Files.getContentUri("external"), contentValues)
                    if (uri != null) {
                        resolver.openOutputStream(uri)?.use { os ->
                            os.write(payload.toByteArray(Charsets.UTF_8))
                        }
                        written = true
                    }
                } catch (e: Exception) {
                    Log.w(TAG, "MediaStore write skipped: ${e.message}")
                }
            }

            Log.i(TAG, "Persistent vault saved successfully (status=$written)")
            return written
        } catch (e: Exception) {
            Log.e(TAG, "Failed to save persistent vault: ${e.message}", e)
            return false
        }
    }

    /**
     * Checks if a persistent vault file exists on disk.
     */
    fun hasVaultOnDisk(context: Context): Boolean {
        val docsFile = File(File(Environment.getExternalStoragePublicDirectory(Environment.DIRECTORY_DOCUMENTS), VAULT_DIR_NAME), VAULT_FILE_NAME)
        if (docsFile.exists() && docsFile.length() > 0) return true

        val dlFile = File(File(Environment.getExternalStoragePublicDirectory(Environment.DIRECTORY_DOWNLOADS), VAULT_DIR_NAME), VAULT_FILE_NAME)
        if (dlFile.exists() && dlFile.length() > 0) return true

        return false
    }

    /**
     * Attempts to automatically restore credentials from the persistent vault file if preferences are currently empty.
     */
    fun restoreVaultIfAvailable(context: Context, force: Boolean = false): Boolean {
        try {
            val spotifyPrefs = context.getSharedPreferences("alify_spotify_prefs", Context.MODE_PRIVATE)
            val currentRefresh = spotifyPrefs.getString("refresh_token", null)

            // If already authenticated and not forcing, no restore needed
            if (!force && !currentRefresh.isNullOrBlank()) {
                return false
            }

            val rawJson = readVaultFromDisk(context) ?: return false
            return applyVaultJson(context, rawJson)
        } catch (e: Exception) {
            Log.e(TAG, "Failed to restore persistent vault: ${e.message}", e)
            return false
        }
    }

    /**
     * Reads the vault JSON string from Documents or Downloads directory.
     */
    private fun readVaultFromDisk(context: Context): String? {
        val docsFile = File(File(Environment.getExternalStoragePublicDirectory(Environment.DIRECTORY_DOCUMENTS), VAULT_DIR_NAME), VAULT_FILE_NAME)
        if (docsFile.exists() && docsFile.canRead()) {
            try {
                return docsFile.readText(Charsets.UTF_8)
            } catch (e: Exception) {
                Log.w(TAG, "Could not read Documents vault: ${e.message}")
            }
        }

        val dlFile = File(File(Environment.getExternalStoragePublicDirectory(Environment.DIRECTORY_DOWNLOADS), VAULT_DIR_NAME), VAULT_FILE_NAME)
        if (dlFile.exists() && dlFile.canRead()) {
            try {
                return dlFile.readText(Charsets.UTF_8)
            } catch (e: Exception) {
                Log.w(TAG, "Could not read Downloads vault: ${e.message}")
            }
        }

        return null
    }

    /**
     * Applies a vault JSON string into SharedPreferences.
     */
    fun applyVaultJson(context: Context, jsonString: String): Boolean {
        try {
            val root = JSONObject(jsonString)
            val spotifyPrefs = context.getSharedPreferences("alify_spotify_prefs", Context.MODE_PRIVATE)
            val filterPrefs = context.getSharedPreferences("alify_filter_prefs", Context.MODE_PRIVATE)

            if (root.has("spotify")) {
                val s = root.getJSONObject("spotify")
                val editor = spotifyPrefs.edit()
                val clientId = s.optString("custom_client_id")
                if (clientId.isNotBlank()) editor.putString("custom_client_id", clientId)
                val access = s.optString("access_token")
                if (access.isNotBlank()) editor.putString("access_token", access)
                val refresh = s.optString("refresh_token")
                if (refresh.isNotBlank()) editor.putString("refresh_token", refresh)
                val expires = s.optLong("expires_at", 0)
                if (expires > 0) editor.putLong("expires_at", expires)
                editor.apply()
            }

            if (root.has("filter")) {
                val f = root.getJSONObject("filter")
                val editor = filterPrefs.edit()
                if (f.has("custom_ethos_prompt")) editor.putString("custom_ethos_prompt", f.optString("custom_ethos_prompt"))
                if (f.has("filter_profile")) editor.putInt("filter_profile", f.optInt("filter_profile", 0))
                if (f.has("analysis_mode")) editor.putInt("analysis_mode", f.optInt("analysis_mode", 0))
                if (f.has("filter_strictness")) editor.putInt("filter_strictness", f.optInt("filter_strictness", 10))
                if (f.has("filter_explicit")) editor.putBoolean("filter_explicit", f.optBoolean("filter_explicit", false))
                if (f.has("enable_gemini")) editor.putBoolean("enable_gemini", f.optBoolean("enable_gemini", true))
                if (f.has("gemini_api_key")) editor.putString("gemini_api_key", f.optString("gemini_api_key"))
                editor.apply()
            }

            Log.i(TAG, "Vault credentials and preferences successfully applied into SharedPreferences")
            return true
        } catch (e: Exception) {
            Log.e(TAG, "Invalid vault JSON: ${e.message}", e)
            return false
        }
    }

    /**
     * Exports the vault to a user-specified Uri (Storage Access Framework).
     */
    fun exportVaultToUri(context: Context, uri: Uri): Boolean {
        return try {
            val payload = createVaultPayload(context)
            context.contentResolver.openOutputStream(uri)?.use { os ->
                os.write(payload.toByteArray(Charsets.UTF_8))
            }
            true
        } catch (e: Exception) {
            Log.e(TAG, "Failed to export vault to Uri: ${e.message}", e)
            false
        }
    }

    /**
     * Imports a vault from a user-specified Uri (Storage Access Framework).
     */
    fun importVaultFromUri(context: Context, uri: Uri): Boolean {
        return try {
            val content = context.contentResolver.openInputStream(uri)?.use { inputStream ->
                inputStream.bufferedReader(Charsets.UTF_8).readText()
            } ?: return false
            applyVaultJson(context, content)
        } catch (e: Exception) {
            Log.e(TAG, "Failed to import vault from Uri: ${e.message}", e)
            false
        }
    }
}

package com.alify.spotify

import android.content.Context
import android.content.Intent
import android.net.Uri
import android.util.Base64
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import okhttp3.FormBody
import okhttp3.OkHttpClient
import okhttp3.Request
import org.json.JSONObject
import java.security.MessageDigest
import java.security.SecureRandom

class SpotifyAuthManager(private val context: Context) {

    private val httpClient = OkHttpClient()
    private val prefs = context.getSharedPreferences("alify_spotify_prefs", Context.MODE_PRIVATE)

    companion object {
        const val CLIENT_ID = "21300c3132f64c0ab1a755a6e5b0d1e8"
        const val REDIRECT_URI = "alify://callback"
        const val SCOPES = "user-read-playback-state user-modify-playback-state user-read-currently-playing"
    }

    private var codeVerifier: String?
        get() = prefs.getString("code_verifier", null)
        set(value) = prefs.edit().putString("code_verifier", value).apply()

    var accessToken: String?
        get() = prefs.getString("access_token", null)
        private set(value) = prefs.edit().putString("access_token", value).apply()

    var refreshToken: String?
        get() = prefs.getString("refresh_token", null)
        private set(value) = prefs.edit().putString("refresh_token", value).apply()

    private var expiresAt: Long
        get() = prefs.getLong("expires_at", 0)
        set(value) = prefs.edit().putLong("expires_at", value).apply()

    fun isConnected(): Boolean {
        return !refreshToken.isNullOrEmpty()
    }

    fun buildAuthIntent(): Intent {
        val verifier = generateCodeVerifier()
        codeVerifier = verifier
        val challenge = generateCodeChallenge(verifier)

        val uri = Uri.parse("https://accounts.spotify.com/authorize")
            .buildUpon()
            .appendQueryParameter("client_id", CLIENT_ID)
            .appendQueryParameter("response_type", "code")
            .appendQueryParameter("redirect_uri", REDIRECT_URI)
            .appendQueryParameter("code_challenge_method", "S256")
            .appendQueryParameter("code_challenge", challenge)
            .appendQueryParameter("scope", SCOPES)
            .build()

        return Intent(Intent.ACTION_VIEW, uri)
    }

    suspend fun handleCallback(uri: Uri): Boolean = withContext(Dispatchers.IO) {
        val code = uri.getQueryParameter("code") ?: return@withContext false
        val verifier = codeVerifier ?: return@withContext false

        val formBody = FormBody.Builder()
            .add("client_id", CLIENT_ID)
            .add("grant_type", "authorization_code")
            .add("code", code)
            .add("redirect_uri", REDIRECT_URI)
            .add("code_verifier", verifier)
            .build()

        val request = Request.Builder()
            .url("https://accounts.spotify.com/api/token")
            .post(formBody)
            .build()

        try {
            httpClient.newCall(request).execute().use { response ->
                if (!response.isSuccessful) return@withContext false
                val body = response.body?.string() ?: return@withContext false
                val json = JSONObject(body)

                accessToken = json.optString("access_token")
                val newRefresh = json.optString("refresh_token")
                if (newRefresh.isNotEmpty()) {
                    refreshToken = newRefresh
                }
                val expiresIn = json.optLong("expires_in", 3600)
                expiresAt = System.currentTimeMillis() + (expiresIn * 1000)
                return@withContext true
            }
        } catch (e: Exception) {
            e.printStackTrace()
            return@withContext false
        }
    }

    suspend fun getValidAccessToken(): String? = withContext(Dispatchers.IO) {
        val currentToken = accessToken
        if (!currentToken.isNullOrEmpty() && System.currentTimeMillis() < expiresAt - 60000) {
            return@withContext currentToken
        }

        val refToken = refreshToken ?: return@withContext null

        val formBody = FormBody.Builder()
            .add("client_id", CLIENT_ID)
            .add("grant_type", "refresh_token")
            .add("refresh_token", refToken)
            .build()

        val request = Request.Builder()
            .url("https://accounts.spotify.com/api/token")
            .post(formBody)
            .build()

        try {
            httpClient.newCall(request).execute().use { response ->
                if (!response.isSuccessful) return@withContext null
                val body = response.body?.string() ?: return@withContext null
                val json = JSONObject(body)

                accessToken = json.optString("access_token")
                val newRefresh = json.optString("refresh_token")
                if (newRefresh.isNotEmpty()) {
                    refreshToken = newRefresh
                }
                val expiresIn = json.optLong("expires_in", 3600)
                expiresAt = System.currentTimeMillis() + (expiresIn * 1000)
                return@withContext accessToken
            }
        } catch (e: Exception) {
            e.printStackTrace()
            return@withContext null
        }
    }

    fun disconnect() {
        prefs.edit().clear().apply()
    }

    private fun generateCodeVerifier(): String {
        val bytes = ByteArray(64)
        SecureRandom().nextBytes(bytes)
        return Base64.encodeToString(bytes, Base64.URL_SAFE or Base64.NO_PADDING or Base64.NO_WRAP)
    }

    private fun generateCodeChallenge(verifier: String): String {
        val digest = MessageDigest.getInstance("SHA-256")
        val hash = digest.digest(verifier.toByteArray(Charsets.US_ASCII))
        return Base64.encodeToString(hash, Base64.URL_SAFE or Base64.NO_PADDING or Base64.NO_WRAP)
    }
}

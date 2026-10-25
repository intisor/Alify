package com.alify.spotify

import android.Manifest
import android.content.ClipData
import android.content.ClipboardManager
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.net.Uri
import android.os.Build
import android.os.Bundle
import android.view.View
import android.widget.Toast
import androidx.appcompat.app.AppCompatActivity
import androidx.core.app.ActivityCompat
import androidx.core.content.ContextCompat
import androidx.lifecycle.lifecycleScope
import com.alify.spotify.databinding.ActivityMainBinding
import com.google.android.material.dialog.MaterialAlertDialogBuilder
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch

class MainActivity : AppCompatActivity() {

    private lateinit var binding: ActivityMainBinding
    private lateinit var authManager: SpotifyAuthManager

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        binding = ActivityMainBinding.inflate(layoutInflater)
        setContentView(binding.root)

        authManager = SpotifyAuthManager(this)

        requestNotificationPermission()
        setupUI()
        handleRedirectIntent(intent)
        startUiPolling()
        checkForUpdates()
    }

    override fun onResume() {
        super.onResume()
        updateAuthState()
    }

    override fun onNewIntent(intent: Intent) {
        super.onNewIntent(intent)
        handleRedirectIntent(intent)
    }

    private fun handleRedirectIntent(intent: Intent?) {
        val uri = intent?.data ?: return
        if (uri.scheme == "alify" && uri.host == "callback") {
            lifecycleScope.launch {
                val success = authManager.handleCallback(uri)
                if (success) {
                    Toast.makeText(this@MainActivity, "Connected to Spotify!", Toast.LENGTH_SHORT).show()
                    updateAuthState()
                } else {
                    Toast.makeText(this@MainActivity, "Spotify login failed. Please check your Client ID.", Toast.LENGTH_LONG).show()
                }
            }
        }
    }

    private fun setupUI() {
        // Settings Navigation
        binding.btnSettings.setOnClickListener {
            startActivity(Intent(this, SettingsActivity::class.java))
        }

        binding.btnSetupClientId.setOnClickListener {
            startActivity(Intent(this, SettingsActivity::class.java))
        }

        binding.tvChangeSettings.setOnClickListener {
            startActivity(Intent(this, SettingsActivity::class.java))
        }

        // View Lyrics & Annotations Button
        binding.btnViewLyrics.setOnClickListener {
            val intent = LyricsActivity.createIntent(
                context = this,
                trackName = AlifyMonitorService.lastTrackName,
                artistName = AlifyMonitorService.lastArtistName,
                isFlagged = AlifyMonitorService.isLastFlagged,
                flagReason = AlifyMonitorService.lastFlagReason,
                flaggedLines = AlifyMonitorService.lastFlaggedLines,
                lyrics = AlifyMonitorService.lastLyrics
            )
            startActivity(intent)
        }

        // Connect / Disconnect Button
        binding.btnLogin.setOnClickListener {
            if (authManager.isConnected()) {
                authManager.disconnect()
                AlifyMonitorService.stop(this)
                updateAuthState()
                Toast.makeText(this, "Disconnected from Spotify", Toast.LENGTH_SHORT).show()
            } else {
                if (!authManager.hasClientId) {
                    showClientIdRequiredDialog()
                    return@setOnClickListener
                }

                val authIntent = authManager.buildAuthIntent()
                if (authIntent != null) {
                    startActivity(authIntent)
                } else {
                    showClientIdRequiredDialog()
                }
            }
        }

        // Toggle Protection Button
        binding.btnToggleProtection.setOnClickListener {
            if (AlifyMonitorService.isRunning) {
                AlifyMonitorService.stop(this)
                binding.btnToggleProtection.text = "Start Halal Protection"
                binding.btnToggleProtection.setBackgroundColor(ContextCompat.getColor(this, R.color.accent_blue))
            } else {
                AlifyMonitorService.start(this)
                binding.btnToggleProtection.text = "Pause Protection"
                binding.btnToggleProtection.setBackgroundColor(ContextCompat.getColor(this, R.color.flag_red))
            }
        }

        updateAuthState()
    }

    private fun showClientIdRequiredDialog() {
        MaterialAlertDialogBuilder(this)
            .setTitle("Spotify Client ID Required")
            .setMessage(
                "Spotify requires each user to have their own free Developer Client ID so you have personal, unlimited access without developer whitelist restrictions.\n\n" +
                "Redirect URI to set in your Spotify App:\nalify://callback\n\n" +
                "Would you like to configure your Client ID now?"
            )
            .setPositiveButton("Configure in Settings") { _, _ ->
                startActivity(Intent(this, SettingsActivity::class.java))
            }
            .setNeutralButton("Copy Redirect URI") { _, _ ->
                val clipboard = getSystemService(Context.CLIPBOARD_SERVICE) as ClipboardManager
                val clip = ClipData.newPlainText("Redirect URI", "alify://callback")
                clipboard.setPrimaryClip(clip)
                Toast.makeText(this, "Redirect URI copied: alify://callback", Toast.LENGTH_SHORT).show()
            }
            .setNegativeButton("Cancel", null)
            .show()
    }

    private fun updateAuthState() {
        val connected = authManager.isConnected()
        val hasClientId = authManager.hasClientId

        // Setup Notice Banner
        binding.cardSetupNotice.visibility = if (!hasClientId && !connected) View.VISIBLE else View.GONE

        // Active Ethos Summary
        val filterPrefs = getSharedPreferences("alify_filter_prefs", Context.MODE_PRIVATE)
        val profileOrdinal = filterPrefs.getInt("filter_profile", 0)
        val profileNames = listOf("Islamic", "Christian", "Family Safe", "General")
        val profileName = profileNames.getOrElse(profileOrdinal) { "Islamic" }
        val strictness = filterPrefs.getInt("filter_strictness", 10)
        val aiEnabled = filterPrefs.getBoolean("enable_gemini", true)
        val aiStatus = if (aiEnabled) "AI Enabled" else "Regex Only"
        binding.tvActiveEthos.text = "Active Filter: $profileName ($strictness/10) • $aiStatus"

        if (connected) {
            binding.tvConnectionStatus.text = "● Connected to Spotify"
            binding.tvConnectionStatus.setTextColor(ContextCompat.getColor(this, R.color.spotify_green))
            binding.btnLogin.text = "Disconnect Spotify"
            binding.btnToggleProtection.visibility = View.VISIBLE

            if (AlifyMonitorService.isRunning) {
                binding.btnToggleProtection.text = "Pause Protection"
                binding.btnToggleProtection.setBackgroundColor(ContextCompat.getColor(this, R.color.flag_red))
            } else {
                binding.btnToggleProtection.text = "Start Halal Protection"
                binding.btnToggleProtection.setBackgroundColor(ContextCompat.getColor(this, R.color.accent_blue))
            }
        } else {
            binding.tvConnectionStatus.text = if (hasClientId) "● Ready to Connect" else "● Setup Required"
            binding.tvConnectionStatus.setTextColor(ContextCompat.getColor(this, R.color.spotify_sub_text))
            binding.btnLogin.text = "Connect with Spotify"
            binding.btnToggleProtection.visibility = View.GONE
            binding.tvCurrentTrack.text = "No track playing"
            binding.tvCurrentArtist.text = if (hasClientId) "Connect Spotify to start monitoring" else "Configure Spotify Developer Client ID first"
            binding.tvSafetyBadge.visibility = View.GONE
            binding.btnViewLyrics.visibility = View.GONE
        }
    }

    private fun startUiPolling() {
        lifecycleScope.launch {
            while (true) {
                if (AlifyMonitorService.isRunning) {
                    binding.tvCurrentTrack.text = AlifyMonitorService.lastTrackName
                    binding.tvCurrentArtist.text = AlifyMonitorService.lastArtistName
                    binding.tvSafetyBadge.visibility = View.VISIBLE
                    binding.tvSafetyBadge.text = AlifyMonitorService.lastStatusBadge

                    val hasTrack = AlifyMonitorService.lastTrackName.isNotEmpty() && AlifyMonitorService.lastTrackName != "No track playing"
                    if (hasTrack) {
                        binding.btnViewLyrics.visibility = View.VISIBLE
                        if (AlifyMonitorService.isLastFlagged) {
                            val flagCount = AlifyMonitorService.lastFlaggedLines.size
                            val countStr = if (flagCount > 0) " ($flagCount flagged)" else ""
                            binding.btnViewLyrics.text = "🔍 Inspect Flagged Verses$countStr"
                            binding.btnViewLyrics.strokeColor = ContextCompat.getColorStateList(this@MainActivity, R.color.flag_red)
                            binding.btnViewLyrics.setTextColor(ContextCompat.getColor(this@MainActivity, R.color.flag_red_light))
                        } else {
                            binding.btnViewLyrics.text = "📜 View Lyrics & Stance Audit"
                            binding.btnViewLyrics.strokeColor = ContextCompat.getColorStateList(this@MainActivity, R.color.spotify_green)
                            binding.btnViewLyrics.setTextColor(ContextCompat.getColor(this@MainActivity, R.color.spotify_light_text))
                        }
                    } else {
                        binding.btnViewLyrics.visibility = View.GONE
                    }

                    if (AlifyMonitorService.isLastFlagged) {
                        binding.tvSafetyBadge.setBackgroundResource(R.drawable.badge_flagged)
                    } else {
                        binding.tvSafetyBadge.setBackgroundResource(R.drawable.badge_clean)
                    }
                }
                delay(2000)
            }
        }
    }

    private fun requestNotificationPermission() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
            if (ContextCompat.checkSelfPermission(this, Manifest.permission.POST_NOTIFICATIONS) != PackageManager.PERMISSION_GRANTED) {
                ActivityCompat.requestPermissions(this, arrayOf(Manifest.permission.POST_NOTIFICATIONS), 101)
            }
        }
    }

    private fun checkForUpdates() {
        lifecycleScope.launch(kotlinx.coroutines.Dispatchers.IO) {
            try {
                val client = okhttp3.OkHttpClient()
                val request = okhttp3.Request.Builder()
                    .url("https://api.github.com/repos/intisor/Alify/releases/latest")
                    .header("User-Agent", "Alify-Android")
                    .build()
                client.newCall(request).execute().use { response ->
                    if (!response.isSuccessful) return@use
                    val body = response.body?.string() ?: return@use
                    val json = org.json.JSONObject(body)
                    val tagName = json.optString("tag_name")
                    val htmlUrl = json.optString("html_url")
                    if (tagName.isNotEmpty() && tagName != "v1.0.0") {
                        kotlinx.coroutines.withContext(kotlinx.coroutines.Dispatchers.Main) {
                            com.google.android.material.snackbar.Snackbar.make(
                                binding.root,
                                "Update available: $tagName",
                                com.google.android.material.snackbar.Snackbar.LENGTH_INDEFINITE
                            ).setAction("Download") {
                                val browserIntent = Intent(Intent.ACTION_VIEW, Uri.parse(htmlUrl))
                                startActivity(browserIntent)
                            }.show()
                        }
                    }
                }
            } catch (e: Exception) {
                // Silently ignore network failures on update check
            }
        }
    }
}

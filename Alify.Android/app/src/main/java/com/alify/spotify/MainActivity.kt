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
import android.widget.LinearLayout
import android.widget.TextView
import android.graphics.Typeface
import android.text.TextUtils
import android.view.Gravity
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

        if (!authManager.isConnected()) {
            val restored = PersistentVaultManager.restoreVaultIfAvailable(this)
            if (restored) {
                Toast.makeText(this, "Welcome back! Credentials restored from persistent vault.", Toast.LENGTH_LONG).show()
            }
        }

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
        if ((uri.scheme == "hujjify" || uri.scheme == "alify") && uri.host == "callback") {
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

        // Reset Dashboard Stats Button
        binding.btnResetStats.setOnClickListener {
            MaterialAlertDialogBuilder(this)
                .setTitle("Reset Protection Stats?")
                .setMessage("This will reset all audited song counters and clear your recent audit history.")
                .setPositiveButton("Reset") { _, _ ->
                    DashboardStatsManager.resetStats(this)
                    updateDashboardUI()
                    Toast.makeText(this, "Stats reset", Toast.LENGTH_SHORT).show()
                }
                .setNegativeButton("Cancel", null)
                .show()
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
                "Redirect URI to set in your Spotify App:\nhujjify://callback\n(or alify://callback)\n\n" +
                "Would you like to configure your Client ID now?"
            )
            .setPositiveButton("Configure in Settings") { _, _ ->
                startActivity(Intent(this, SettingsActivity::class.java))
            }
            .setNeutralButton("Copy Redirect URI") { _, _ ->
                val clipboard = getSystemService(Context.CLIPBOARD_SERVICE) as ClipboardManager
                val clip = ClipData.newPlainText("Redirect URI", "hujjify://callback")
                clipboard.setPrimaryClip(clip)
                Toast.makeText(this, "Redirect URI copied: hujjify://callback", Toast.LENGTH_SHORT).show()
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
                updateDashboardUI()
                delay(2000)
            }
        }
    }

    private fun updateDashboardUI() {
        val stats = DashboardStatsManager.getStats(this)
        binding.tvStatPlayed.text = stats.totalPlayed.toString()
        binding.tvStatSkipped.text = stats.totalSkipped.toString()
        binding.tvStatFlagged.text = stats.totalFlagged.toString()
        binding.tvStatNoLyrics.text = stats.totalNoLyrics.toString()

        val recentTracks = DashboardStatsManager.getRecentTracks(this)
        if (recentTracks.isEmpty()) {
            binding.tvNoRecentActivity.visibility = View.VISIBLE
            binding.layoutRecentTracks.removeAllViews()
        } else {
            binding.tvNoRecentActivity.visibility = View.GONE
            binding.layoutRecentTracks.removeAllViews()

            for (entry in recentTracks.take(8)) {
                val row = LinearLayout(this).apply {
                    layoutParams = LinearLayout.LayoutParams(
                        LinearLayout.LayoutParams.MATCH_PARENT,
                        LinearLayout.LayoutParams.WRAP_CONTENT
                    ).apply {
                        setMargins(0, 0, 0, 16)
                    }
                    orientation = LinearLayout.HORIZONTAL
                    gravity = Gravity.CENTER_VERTICAL
                    setPadding(20, 14, 20, 14)
                    setBackgroundResource(R.drawable.stat_box_background)
                    isClickable = true
                    isFocusable = true
                    setOnClickListener {
                        val intent = LyricsActivity.createIntent(
                            context = this@MainActivity,
                            trackName = entry.title,
                            artistName = entry.artist,
                            isFlagged = entry.statusBadge == "FLAGGED" || entry.statusBadge == "SKIPPED",
                            flagReason = entry.flagReason,
                            flaggedLines = entry.flaggedLines,
                            lyrics = entry.lyrics
                        )
                        startActivity(intent)
                    }
                }

                val textLayout = LinearLayout(this).apply {
                    layoutParams = LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WRAP_CONTENT, 1f)
                    orientation = LinearLayout.VERTICAL
                }

                val titleTv = TextView(this).apply {
                    text = entry.title
                    setTextColor(ContextCompat.getColor(this@MainActivity, R.color.spotify_light_text))
                    textSize = 13f
                    setTypeface(null, Typeface.BOLD)
                    maxLines = 1
                    ellipsize = TextUtils.TruncateAt.END
                }

                val artistTv = TextView(this).apply {
                    text = entry.artist
                    setTextColor(ContextCompat.getColor(this@MainActivity, R.color.spotify_sub_text))
                    textSize = 11f
                    maxLines = 1
                    ellipsize = TextUtils.TruncateAt.END
                }

                textLayout.addView(titleTv)
                textLayout.addView(artistTv)

                val badgeTv = TextView(this).apply {
                    setPadding(18, 6, 18, 6)
                    textSize = 10f
                    setTypeface(null, Typeface.BOLD)
                    setTextColor(ContextCompat.getColor(this@MainActivity, R.color.spotify_light_text))

                    when (entry.statusBadge) {
                        "SKIPPED" -> {
                            text = "⏭ Skipped"
                            setBackgroundResource(R.drawable.badge_skipped)
                        }
                        "FLAGGED" -> {
                            text = "✕ Flagged"
                            setBackgroundResource(R.drawable.badge_flagged)
                        }
                        "NO_LYRICS" -> {
                            text = "ℹ️ No Lyrics"
                            setBackgroundResource(R.drawable.badge_no_lyrics)
                        }
                        else -> {
                            text = "✓ Clean"
                            setBackgroundResource(R.drawable.badge_clean)
                        }
                    }
                }

                row.addView(textLayout)
                row.addView(badgeTv)
                binding.layoutRecentTracks.addView(row)
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
                val currentVersion = try {
                    packageManager.getPackageInfo(packageName, 0).versionName ?: "1.3.0"
                } catch (e: Exception) {
                    "1.3.0"
                }

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
                    if (tagName.isNotEmpty() && VersionUtil.isNewerVersion(tagName, currentVersion)) {
                        kotlinx.coroutines.withContext(kotlinx.coroutines.Dispatchers.Main) {
                            com.google.android.material.snackbar.Snackbar.make(
                                binding.root,
                                "Update available: $tagName (Current: v$currentVersion)",
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

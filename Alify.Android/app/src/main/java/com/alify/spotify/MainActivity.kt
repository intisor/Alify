package com.alify.spotify

import android.Manifest
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.os.Build
import android.os.Bundle
import android.view.View
import android.widget.AdapterView
import android.widget.ArrayAdapter
import android.widget.SeekBar
import android.widget.Toast
import androidx.appcompat.app.AppCompatActivity
import androidx.core.app.ActivityCompat
import androidx.core.content.ContextCompat
import androidx.lifecycle.lifecycleScope
import com.alify.spotify.databinding.ActivityMainBinding
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
                    Toast.makeText(this@MainActivity, "Spotify login failed. Please try again.", Toast.LENGTH_LONG).show()
                }
            }
        }
    }

    private fun setupUI() {
        val filterPrefs = getSharedPreferences("alify_filter_prefs", Context.MODE_PRIVATE)

        // Setup Ethos Spinner
        val profiles = listOf(
            "Islamic Ethos (Halal: Zero Shirk, Fahisha, Khamr)",
            "Christian Ethos (Biblical Guardrails)",
            "Family & Child Safe (Zero Vulgarity)",
            "General Safety Filter"
        )
        val adapter = ArrayAdapter(this, android.R.layout.simple_spinner_dropdown_item, profiles)
        binding.spProfile.adapter = adapter
        binding.spProfile.setSelection(filterPrefs.getInt("filter_profile", 0))

        fun saveFilterSettings() {
            filterPrefs.edit()
                .putInt("filter_profile", binding.spProfile.selectedItemPosition)
                .putInt("filter_strictness", binding.sbStrictness.progress)
                .putBoolean("filter_explicit", binding.cbExplicit.isChecked)
                .putBoolean("enable_gemini", binding.cbGemini.isChecked)
                .putString("gemini_api_key", binding.etGeminiApiKey.text?.toString()?.trim() ?: "")
                .apply()
        }

        binding.spProfile.onItemSelectedListener = object : AdapterView.OnItemSelectedListener {
            override fun onItemSelected(parent: AdapterView<*>?, view: View?, position: Int, id: Long) {
                saveFilterSettings()
            }
            override fun onNothingSelected(parent: AdapterView<*>?) {}
        }

        // Setup Strictness Slider
        val savedStrictness = filterPrefs.getInt("filter_strictness", 10)
        binding.sbStrictness.progress = savedStrictness
        binding.tvStrictnessLabel.text = "Strictness Level: $savedStrictness / 10"

        binding.sbStrictness.setOnSeekBarChangeListener(object : SeekBar.OnSeekBarChangeListener {
            override fun onProgressChanged(seekBar: SeekBar?, progress: Int, fromUser: Boolean) {
                binding.tvStrictnessLabel.text = "Strictness Level: $progress / 10"
                if (fromUser) saveFilterSettings()
            }
            override fun onStartTrackingTouch(seekBar: SeekBar?) {}
            override fun onStopTrackingTouch(seekBar: SeekBar?) {}
        })

        // Checkboxes and API Key
        binding.cbExplicit.isChecked = filterPrefs.getBoolean("filter_explicit", false)
        binding.cbExplicit.setOnCheckedChangeListener { _, _ -> saveFilterSettings() }

        binding.cbGemini.isChecked = filterPrefs.getBoolean("enable_gemini", true)
        binding.cbGemini.setOnCheckedChangeListener { _, isChecked ->
            saveFilterSettings()
            binding.tilGeminiApiKey.visibility = if (isChecked) View.VISIBLE else View.GONE
        }

        binding.etGeminiApiKey.setText(filterPrefs.getString("gemini_api_key", ""))

        // Initialize BYOK Client ID field
        binding.etByokClientId.setText(authManager.customClientId ?: "")

        // Connect Button
        binding.btnLogin.setOnClickListener {
            saveFilterSettings()
            if (authManager.isConnected()) {
                authManager.disconnect()
                AlifyMonitorService.stop(this)
                updateAuthState()
                Toast.makeText(this, "Disconnected from Spotify", Toast.LENGTH_SHORT).show()
            } else {
                val byokInput = binding.etByokClientId.text?.toString()?.trim()
                authManager.customClientId = if (byokInput.isNullOrEmpty()) null else byokInput
                val authIntent = authManager.buildAuthIntent()
                startActivity(authIntent)
            }
        }

        // Toggle Protection Button
        binding.btnToggleProtection.setOnClickListener {
            saveFilterSettings()
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

    private fun updateAuthState() {
        val connected = authManager.isConnected()
        binding.tilByokClientId.visibility = if (connected) View.GONE else View.VISIBLE
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
            binding.tvConnectionStatus.text = "● Ready to Connect"
            binding.tvConnectionStatus.setTextColor(ContextCompat.getColor(this, R.color.spotify_sub_text))
            binding.btnLogin.text = "Connect with Spotify"
            binding.btnToggleProtection.visibility = View.GONE
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
                                val browserIntent = Intent(Intent.ACTION_VIEW, android.net.Uri.parse(htmlUrl))
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

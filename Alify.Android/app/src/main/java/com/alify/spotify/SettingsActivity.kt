package com.alify.spotify

import android.content.ClipData
import android.content.ClipboardManager
import android.content.Context
import android.content.Intent
import android.net.Uri
import android.os.Bundle
import android.view.View
import android.widget.AdapterView
import android.widget.ArrayAdapter
import android.widget.SeekBar
import android.widget.Toast
import androidx.appcompat.app.AppCompatActivity
import com.alify.spotify.databinding.ActivitySettingsBinding

class SettingsActivity : AppCompatActivity() {

    private lateinit var binding: ActivitySettingsBinding
    private lateinit var authManager: SpotifyAuthManager

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        binding = ActivitySettingsBinding.inflate(layoutInflater)
        setContentView(binding.root)

        authManager = SpotifyAuthManager(this)

        setupListeners()
        loadPreferences()
    }

    private fun setupListeners() {
        binding.btnBack.setOnClickListener {
            finish()
        }

        binding.btnSaveTop.setOnClickListener {
            savePreferences()
        }

        binding.btnSaveSettings.setOnClickListener {
            savePreferences()
        }

        // Spotify Helper Links
        binding.btnOpenSpotifyDashboard.setOnClickListener {
            openUrl("https://developer.spotify.com/dashboard")
        }

        binding.btnCopyRedirectUri.setOnClickListener {
            val clipboard = getSystemService(Context.CLIPBOARD_SERVICE) as ClipboardManager
            val clip = ClipData.newPlainText("Spotify Redirect URI", "alify://callback")
            clipboard.setPrimaryClip(clip)
            Toast.makeText(this, "Redirect URI copied: alify://callback", Toast.LENGTH_SHORT).show()
        }

        // AI Provider Helper Links
        binding.btnGetGeminiKey.setOnClickListener {
            openUrl("https://aistudio.google.com/")
        }

        binding.btnGetGroqKey.setOnClickListener {
            openUrl("https://console.groq.com/keys")
        }

        binding.btnGetOpenRouterKey.setOnClickListener {
            openUrl("https://openrouter.ai/keys")
        }

        // Ethos Spinner
        val profiles = listOf(
            "Islamic Ethos (Halal: Zero Shirk, Fahisha, Khamr)",
            "Christian Ethos (Biblical Guardrails)",
            "Family & Child Safe (Zero Vulgarity)",
            "General Safety Filter"
        )
        val adapter = ArrayAdapter(this, android.R.layout.simple_spinner_dropdown_item, profiles)
        binding.spProfileSettings.adapter = adapter

        // Strictness SeekBar
        binding.sbStrictnessSettings.setOnSeekBarChangeListener(object : SeekBar.OnSeekBarChangeListener {
            override fun onProgressChanged(seekBar: SeekBar?, progress: Int, fromUser: Boolean) {
                binding.tvStrictnessLabelSettings.text = "Strictness Level: $progress / 10"
            }
            override fun onStartTrackingTouch(seekBar: SeekBar?) {}
            override fun onStopTrackingTouch(seekBar: SeekBar?) {}
        })

        binding.cbEnableAi.setOnCheckedChangeListener { _, isChecked ->
            binding.tilAiApiKey.visibility = if (isChecked) View.VISIBLE else View.GONE
        }
    }

    private fun loadPreferences() {
        val filterPrefs = getSharedPreferences("alify_filter_prefs", Context.MODE_PRIVATE)

        // Spotify Client ID
        binding.etSpotifyClientId.setText(authManager.customClientId ?: "")

        // AI Settings
        val enableAi = filterPrefs.getBoolean("enable_gemini", true)
        binding.cbEnableAi.isChecked = enableAi
        binding.tilAiApiKey.visibility = if (enableAi) View.VISIBLE else View.GONE
        binding.etAiApiKey.setText(filterPrefs.getString("gemini_api_key", ""))

        // Ethos Profile
        val savedProfile = filterPrefs.getInt("filter_profile", 0)
        binding.spProfileSettings.setSelection(savedProfile)

        // Strictness
        val savedStrictness = filterPrefs.getInt("filter_strictness", 10)
        binding.sbStrictnessSettings.progress = savedStrictness
        binding.tvStrictnessLabelSettings.text = "Strictness Level: $savedStrictness / 10"

        // Explicit Checkbox
        binding.cbExplicitSettings.isChecked = filterPrefs.getBoolean("filter_explicit", false)
    }

    private fun savePreferences() {
        val filterPrefs = getSharedPreferences("alify_filter_prefs", Context.MODE_PRIVATE)

        // Save Spotify Client ID
        val customClientId = binding.etSpotifyClientId.text?.toString()?.trim()
        authManager.customClientId = if (customClientId.isNullOrEmpty()) null else customClientId

        // Save Filter & AI Preferences
        filterPrefs.edit()
            .putInt("filter_profile", binding.spProfileSettings.selectedItemPosition)
            .putInt("filter_strictness", binding.sbStrictnessSettings.progress)
            .putBoolean("filter_explicit", binding.cbExplicitSettings.isChecked)
            .putBoolean("enable_gemini", binding.cbEnableAi.isChecked)
            .putString("gemini_api_key", binding.etAiApiKey.text?.toString()?.trim() ?: "")
            .apply()

        Toast.makeText(this, "Settings saved successfully", Toast.LENGTH_SHORT).show()
        finish()
    }

    private fun openUrl(url: String) {
        try {
            val intent = Intent(Intent.ACTION_VIEW, Uri.parse(url))
            startActivity(intent)
        } catch (e: Exception) {
            Toast.makeText(this, "Unable to open browser: $url", Toast.LENGTH_SHORT).show()
        }
    }
}

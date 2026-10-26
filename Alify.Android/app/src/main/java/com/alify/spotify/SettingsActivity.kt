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
import androidx.lifecycle.lifecycleScope
import com.alify.spotify.databinding.ActivitySettingsBinding
import com.google.android.material.dialog.MaterialAlertDialogBuilder
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import okhttp3.OkHttpClient
import okhttp3.Request
import org.json.JSONObject

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
        displayAppVersion()
    }

    private fun displayAppVersion() {
        val currentVersion = getInstalledVersion()
        binding.tvAppVersion.text = "Installed Version: v$currentVersion"
    }

    private fun getInstalledVersion(): String {
        return try {
            packageManager.getPackageInfo(packageName, 0).versionName ?: "1.3.0"
        } catch (e: Exception) {
            "1.3.0"
        }
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
            val clip = ClipData.newPlainText("Spotify Redirect URI", "hujjify://callback")
            clipboard.setPrimaryClip(clip)
            Toast.makeText(this, "Redirect URI copied: hujjify://callback", Toast.LENGTH_SHORT).show()
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

        // Manual Update Checker
        binding.btnCheckUpdates.setOnClickListener {
            checkGitHubUpdateManually()
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

        // Analysis Engine Mode Spinner
        val engineModes = listOf(
            "AI Semantic Engine (Authoritative: zero regex false flags)",
            "Offline Heuristic Engine (On-device regex only, 0 tokens)",
            "Hybrid Guardrails (Regex tripwire + AI verification)"
        )
        val modeAdapter = ArrayAdapter(this, android.R.layout.simple_spinner_dropdown_item, engineModes)
        binding.spAnalysisMode.adapter = modeAdapter
        binding.spAnalysisMode.onItemSelectedListener = object : AdapterView.OnItemSelectedListener {
            override fun onItemSelected(parent: AdapterView<*>?, view: View?, position: Int, id: Long) {
                binding.tvAnalysisModeHint.text = when (position) {
                    0 -> "AI evaluates full lyrical nuance, Pidgin/slang, and intentional directives directly. Offline regex runs ONLY as fallback if network fails."
                    1 -> "Pure on-device regex scanning. Zero API calls, zero tokens, works 100% offline."
                    2 -> "Strict dual guardrails: runs fast regex tripwire first, then calls AI for semantic confirmation."
                    else -> ""
                }
            }
            override fun onNothingSelected(parent: AdapterView<*>?) {}
        }

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

    private fun checkGitHubUpdateManually() {
        val currentVersion = getInstalledVersion()
        binding.btnCheckUpdates.isEnabled = false
        binding.btnCheckUpdates.text = "Checking GitHub..."

        lifecycleScope.launch(Dispatchers.IO) {
            try {
                val client = OkHttpClient()
                val request = Request.Builder()
                    .url("https://api.github.com/repos/intisor/Alify/releases/latest")
                    .header("User-Agent", "Alify-Android")
                    .build()

                client.newCall(request).execute().use { response ->
                    withContext(Dispatchers.Main) {
                        binding.btnCheckUpdates.isEnabled = true
                        binding.btnCheckUpdates.text = "🔄 Check for Updates on GitHub"

                        if (!response.isSuccessful) {
                            Toast.makeText(this@SettingsActivity, "Could not fetch releases (HTTP ${response.code})", Toast.LENGTH_SHORT).show()
                            return@withContext
                        }

                        val body = response.body?.string() ?: return@withContext
                        val json = JSONObject(body)
                        val tagName = json.optString("tag_name")
                        val htmlUrl = json.optString("html_url")

                        if (tagName.isNotEmpty() && VersionUtil.isNewerVersion(tagName, currentVersion)) {
                            MaterialAlertDialogBuilder(this@SettingsActivity)
                                .setTitle("Update Available: $tagName")
                                .setMessage("A new update ($tagName) is available on GitHub!\n\nYour current version: v$currentVersion\n\nWould you like to open the release page to download the latest APK?")
                                .setPositiveButton("Download APK") { _, _ ->
                                    openUrl(htmlUrl)
                                }
                                .setNegativeButton("Later", null)
                                .show()
                        } else {
                            MaterialAlertDialogBuilder(this@SettingsActivity)
                                .setTitle("Up to Date")
                                .setMessage("You are running the latest version of Hujjify (v$currentVersion).")
                                .setPositiveButton("OK", null)
                                .show()
                        }
                    }
                }
            } catch (e: Exception) {
                withContext(Dispatchers.Main) {
                    binding.btnCheckUpdates.isEnabled = true
                    binding.btnCheckUpdates.text = "🔄 Check for Updates on GitHub"
                    Toast.makeText(this@SettingsActivity, "Network error checking updates", Toast.LENGTH_SHORT).show()
                }
            }
        }
    }

    private fun loadPreferences() {
        val filterPrefs = getSharedPreferences("alify_filter_prefs", Context.MODE_PRIVATE)

        // Spotify Client ID
        binding.etSpotifyClientId.setText(authManager.customClientId ?: "")

        // Chat-to-Filter Custom Prompt
        binding.etCustomEthosPrompt.setText(filterPrefs.getString("custom_ethos_prompt", ""))

        // AI Settings
        val enableAi = filterPrefs.getBoolean("enable_gemini", true)
        binding.cbEnableAi.isChecked = enableAi
        binding.tilAiApiKey.visibility = if (enableAi) View.VISIBLE else View.GONE
        binding.etAiApiKey.setText(filterPrefs.getString("gemini_api_key", ""))

        // Ethos Profile
        val savedProfile = filterPrefs.getInt("filter_profile", 0)
        binding.spProfileSettings.setSelection(savedProfile)

        // Analysis Engine Mode
        val savedMode = filterPrefs.getInt("analysis_mode", AnalysisMode.AI_SEMANTIC.ordinal)
        binding.spAnalysisMode.setSelection(savedMode)

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
            .putString("custom_ethos_prompt", binding.etCustomEthosPrompt.text?.toString()?.trim() ?: "")
            .putInt("filter_profile", binding.spProfileSettings.selectedItemPosition)
            .putInt("analysis_mode", binding.spAnalysisMode.selectedItemPosition)
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

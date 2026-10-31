package com.alify.spotify

import android.content.Context
import android.content.Intent
import android.graphics.Typeface
import android.os.Bundle
import android.view.Gravity
import android.view.View
import android.widget.LinearLayout
import android.widget.TextView
import android.widget.Toast
import androidx.appcompat.app.AlertDialog
import androidx.appcompat.app.AppCompatActivity
import androidx.core.content.ContextCompat
import androidx.lifecycle.lifecycleScope
import com.alify.spotify.databinding.ActivityLyricsBinding
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

class LyricsActivity : AppCompatActivity() {

    private lateinit var binding: ActivityLyricsBinding
    private val lrclibClient = LrclibClient()
    private var currentTrackName: String = ""
    private var currentArtistName: String = ""
    private var currentIsFlagged: Boolean = false
    private var currentFlagReason: String? = null
    private var currentFlaggedLines: List<String> = emptyList()
    private var currentAnnotations: Map<String, String> = emptyMap()
    private var currentLyrics: String? = null

    companion object {
        const val EXTRA_TRACK_NAME = "extra_track_name"
        const val EXTRA_ARTIST_NAME = "extra_artist_name"
        const val EXTRA_IS_FLAGGED = "extra_is_flagged"
        const val EXTRA_FLAG_REASON = "extra_flag_reason"
        const val EXTRA_FLAGGED_LINES = "extra_flagged_lines"
        const val EXTRA_LYRICS = "extra_lyrics"

        fun createIntent(
            context: Context,
            trackName: String,
            artistName: String,
            isFlagged: Boolean,
            flagReason: String?,
            flaggedLines: List<String>,
            lyrics: String?
        ): Intent {
            return Intent(context, LyricsActivity::class.java).apply {
                putExtra(EXTRA_TRACK_NAME, trackName)
                putExtra(EXTRA_ARTIST_NAME, artistName)
                putExtra(EXTRA_IS_FLAGGED, isFlagged)
                putExtra(EXTRA_FLAG_REASON, flagReason)
                putStringArrayListExtra(EXTRA_FLAGGED_LINES, ArrayList(flaggedLines))
                putExtra(EXTRA_LYRICS, lyrics)
            }
        }
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        binding = ActivityLyricsBinding.inflate(layoutInflater)
        setContentView(binding.root)

        binding.btnBackLyrics.setOnClickListener {
            finish()
        }

        binding.btnRetryLyrics.setOnClickListener {
            fetchAndAuditLyrics(currentTrackName, currentArtistName, false, null, emptyList(), emptyMap())
        }

        val trackName = intent.getStringExtra(EXTRA_TRACK_NAME) ?: AlifyMonitorService.lastTrackName
        val artistName = intent.getStringExtra(EXTRA_ARTIST_NAME) ?: AlifyMonitorService.lastArtistName
        val isFlagged = intent.getBooleanExtra(EXTRA_IS_FLAGGED, AlifyMonitorService.isLastFlagged)
        val flagReason = intent.getStringExtra(EXTRA_FLAG_REASON) ?: AlifyMonitorService.lastFlagReason
        val flaggedLines = intent.getStringArrayListExtra(EXTRA_FLAGGED_LINES) ?: AlifyMonitorService.lastFlaggedLines
        val lyrics = intent.getStringExtra(EXTRA_LYRICS) ?: AlifyMonitorService.lastLyrics
        val annotations = AlifyMonitorService.lastAnnotations

        currentTrackName = trackName
        currentArtistName = artistName
        currentIsFlagged = isFlagged
        currentFlagReason = flagReason
        currentFlaggedLines = flaggedLines
        currentAnnotations = annotations
        currentLyrics = lyrics

        binding.tvLyricsTrackTitle.text = trackName
        binding.tvLyricsArtist.text = artistName

        binding.tvLyricsStatusBadge.setOnClickListener {
            showVerdictOverrideDialog(selectedLine = null)
        }
        binding.cardAuditSummary.setOnClickListener {
            showVerdictOverrideDialog(selectedLine = null)
        }

        updateAuditHeader(isFlagged, flagReason, flaggedLines)

        if (!lyrics.isNullOrBlank()) {
            displayLyrics(lyrics, flaggedLines, annotations)
        } else {
            fetchAndAuditLyrics(trackName, artistName, isFlagged, flagReason, flaggedLines, annotations)
        }
    }

    private fun updateAuditHeader(
        isFlagged: Boolean,
        flagReason: String?,
        flaggedLines: List<String>
    ) {
        if (isFlagged) {
            binding.tvLyricsStatusBadge.text = "✕ Flagged"
            binding.tvLyricsStatusBadge.setBackgroundResource(R.drawable.badge_flagged)
            binding.cardAuditSummary.visibility = View.VISIBLE
            binding.tvAuditReason.text = flagReason ?: "Flagged: Ethos Violation"
            val count = flaggedLines.size
            binding.tvAuditSubtextNotice.text = if (count > 0) {
                "$count offending verse(s) identified and annotated below."
            } else {
                "Offending content identified by AI content auditor."
            }
        } else {
            binding.tvLyricsStatusBadge.text = "✓ Clean"
            binding.tvLyricsStatusBadge.setBackgroundResource(R.drawable.badge_clean)
            binding.cardAuditSummary.visibility = View.GONE
        }
    }

    private fun fetchAndAuditLyrics(
        trackName: String,
        artistName: String,
        initialFlagged: Boolean,
        initialReason: String?,
        initialFlaggedLines: List<String>,
        initialAnnotations: Map<String, String>
    ) {
        binding.pbLyricsLoading.visibility = View.VISIBLE
        binding.layoutNoLyrics.visibility = View.GONE
        binding.svLyricsContent.visibility = View.GONE

        lifecycleScope.launch {
            val filterEngine = FilterEngine.fromPreferences(this@LyricsActivity)
            val fetchedLyrics = withContext(Dispatchers.IO) {
                lrclibClient.getLyrics(artistName, trackName, filterEngine.geminiApiKey)
            }

            if (!fetchedLyrics.isNullOrBlank()) {
                AlifyMonitorService.lastLyrics = fetchedLyrics

                // If service hadn't finished auditing yet, audit directly now
                if (!initialFlagged && initialFlaggedLines.isEmpty()) {
                    binding.tvLyricsStatusBadge.text = "⏳ Auditing..."
                    val evalResult = withContext(Dispatchers.IO) {
                        filterEngine.evaluate(trackName, artistName, false, fetchedLyrics)
                    }
                    binding.pbLyricsLoading.visibility = View.GONE

                    AlifyMonitorService.isLastFlagged = evalResult.isFlagged
                    AlifyMonitorService.lastFlagReason = evalResult.flagReason
                    AlifyMonitorService.lastFlaggedLines = evalResult.flaggedLines
                    AlifyMonitorService.lastAnnotations = evalResult.annotations

                    currentIsFlagged = evalResult.isFlagged
                    currentFlagReason = evalResult.flagReason
                    currentFlaggedLines = evalResult.flaggedLines
                    currentAnnotations = evalResult.annotations
                    currentLyrics = fetchedLyrics

                    SilentCorpusHarvester.recordEvaluation(
                        context = this@LyricsActivity,
                        trackName = trackName,
                        artistName = artistName,
                        profile = filterEngine.profile,
                        strictness = filterEngine.strictness,
                        customDirective = filterEngine.customEthosPrompt,
                        lyrics = fetchedLyrics,
                        result = evalResult
                    )

                    updateAuditHeader(evalResult.isFlagged, evalResult.flagReason, evalResult.flaggedLines)
                    displayLyrics(fetchedLyrics, evalResult.flaggedLines, evalResult.annotations)
                } else {
                    binding.pbLyricsLoading.visibility = View.GONE
                    displayLyrics(fetchedLyrics, initialFlaggedLines, initialAnnotations)
                }
            } else {
                binding.pbLyricsLoading.visibility = View.GONE
                binding.layoutNoLyrics.visibility = View.VISIBLE
                binding.svLyricsContent.visibility = View.GONE
            }
        }
    }

    private fun displayLyrics(
        lyrics: String,
        flaggedLines: List<String>,
        annotations: Map<String, String>
    ) {
        binding.svLyricsContent.visibility = View.VISIBLE
        binding.layoutNoLyrics.visibility = View.GONE
        binding.llLyricsContainer.removeAllViews()
        val lines = lyrics.lines()
        var lineNumber = 1

        for (rawLine in lines) {
            val line = rawLine.trim()

            // Skip excessive blank lines
            if (line.isEmpty()) {
                val spacer = View(this).apply {
                    layoutParams = LinearLayout.LayoutParams(
                        LinearLayout.LayoutParams.MATCH_PARENT,
                        dpToPx(8)
                    )
                }
                binding.llLyricsContainer.addView(spacer)
                continue
            }

            // Section Header (e.g., [Verse 1], [Chorus])
            if (line.startsWith("[") && line.endsWith("]")) {
                val headerView = TextView(this).apply {
                    text = line
                    setTextColor(ContextCompat.getColor(this@LyricsActivity, R.color.spotify_green))
                    textSize = 13f
                    typeface = Typeface.DEFAULT_BOLD
                    setPadding(0, dpToPx(12), 0, dpToPx(4))
                }
                binding.llLyricsContainer.addView(headerView)
                continue
            }

            // Check if line is flagged (exact or substring match)
            val matchingFlag = flaggedLines.firstOrNull { flagged ->
                flagged.isNotBlank() && (line.contains(flagged, ignoreCase = true) || flagged.contains(line, ignoreCase = true))
            }
            val isLineFlagged = matchingFlag != null

            if (isLineFlagged) {
                // Highlighted Flagged Verse Card
                val flaggedContainer = LinearLayout(this).apply {
                    orientation = LinearLayout.VERTICAL
                    setBackgroundResource(R.drawable.bg_flagged_line)
                    setPadding(dpToPx(12), dpToPx(8), dpToPx(12), dpToPx(8))
                    val params = LinearLayout.LayoutParams(
                        LinearLayout.LayoutParams.MATCH_PARENT,
                        LinearLayout.LayoutParams.WRAP_CONTENT
                    ).apply {
                        setMargins(0, dpToPx(4), 0, dpToPx(6))
                    }
                    layoutParams = params
                }

                // Top row with line number and lyric text
                val lineRow = LinearLayout(this).apply {
                    orientation = LinearLayout.HORIZONTAL
                    gravity = Gravity.CENTER_VERTICAL
                }

                val numView = TextView(this).apply {
                    text = "$lineNumber"
                    setTextColor(ContextCompat.getColor(this@LyricsActivity, R.color.flag_red))
                    textSize = 12f
                    typeface = Typeface.MONOSPACE
                    minWidth = dpToPx(28)
                }
                lineRow.addView(numView)

                val lyricView = TextView(this).apply {
                    text = line
                    setTextColor(ContextCompat.getColor(this@LyricsActivity, R.color.spotify_light_text))
                    textSize = 14f
                    typeface = Typeface.DEFAULT_BOLD
                }
                lineRow.addView(lyricView)
                flaggedContainer.addView(lineRow)

                // Subtext / Annotation Callout
                val annotationText = annotations[matchingFlag] ?: annotations[line] ?: "Ethos Violation: explicit subtext"
                val calloutView = TextView(this).apply {
                    text = "⚠️ Flagged: $annotationText"
                    setTextColor(ContextCompat.getColor(this@LyricsActivity, R.color.flag_red_light))
                    textSize = 12f
                    typeface = Typeface.create(Typeface.DEFAULT, Typeface.ITALIC)
                    setPadding(dpToPx(28), dpToPx(4), 0, 0)
                }
                flaggedContainer.addView(calloutView)
                flaggedContainer.setOnClickListener {
                    showVerdictOverrideDialog(selectedLine = line)
                }

                binding.llLyricsContainer.addView(flaggedContainer)
            } else {
                // Normal Clean Line
                val lineRow = LinearLayout(this).apply {
                    orientation = LinearLayout.HORIZONTAL
                    gravity = Gravity.CENTER_VERTICAL
                    setPadding(0, dpToPx(3), 0, dpToPx(3))
                    setOnClickListener {
                        showVerdictOverrideDialog(selectedLine = line)
                    }
                }

                val numView = TextView(this).apply {
                    text = "$lineNumber"
                    setTextColor(ContextCompat.getColor(this@LyricsActivity, R.color.line_number_color))
                    textSize = 12f
                    typeface = Typeface.MONOSPACE
                    minWidth = dpToPx(28)
                }
                lineRow.addView(numView)

                val lyricView = TextView(this).apply {
                    text = line
                    setTextColor(ContextCompat.getColor(this@LyricsActivity, R.color.spotify_light_text))
                    textSize = 14f
                }
                lineRow.addView(lyricView)

                binding.llLyricsContainer.addView(lineRow)
            }

            lineNumber++
        }
    }

    private fun showVerdictOverrideDialog(selectedLine: String?) {
        val options = arrayOf(
            "✓ Allow Song (Mark as Clean)",
            "✕ Flag — Sexual / Innuendo",
            "✕ Flag — Substance / Alcohol",
            "✕ Flag — Profanity / Street Slang",
            "✕ Flag — Blasphemy / Violence"
        )

        val title = if (!selectedLine.isNullOrBlank()) {
            "Review Line: \"${selectedLine.take(42)}...\""
        } else {
            "Adjust Verdict: $currentTrackName"
        }

        AlertDialog.Builder(this)
            .setTitle(title)
            .setItems(options) { _, which ->
                val newFlagged = which != 0
                val newReason = when (which) {
                    0 -> "Verified Clean"
                    1 -> "Flagged: Sexual / Innuendo"
                    2 -> "Flagged: Substance / Alcohol"
                    3 -> "Flagged: Profanity / Slang"
                    else -> "Flagged: Blasphemy / Violence"
                }

                val engine = FilterEngine.fromPreferences(this)
                val updated = SilentCorpusHarvester.recordUserOverride(
                    context = this,
                    trackName = currentTrackName,
                    artistName = currentArtistName,
                    profile = engine.profile,
                    strictness = engine.strictness,
                    lyrics = currentLyrics,
                    previousFlagged = currentIsFlagged,
                    previousReason = currentFlagReason,
                    previousLines = currentFlaggedLines,
                    newFlagged = newFlagged,
                    newReason = newReason,
                    selectedLine = selectedLine
                )

                currentIsFlagged = updated.isFlagged
                currentFlagReason = updated.flagReason
                currentFlaggedLines = updated.flaggedLines
                currentAnnotations = updated.annotations

                AlifyMonitorService.isLastFlagged = updated.isFlagged
                AlifyMonitorService.lastFlagReason = updated.flagReason
                AlifyMonitorService.lastFlaggedLines = updated.flaggedLines
                AlifyMonitorService.lastAnnotations = updated.annotations
                val cacheKey = "${currentArtistName.trim().lowercase()}::${currentTrackName.trim().lowercase()}"
                AlifyMonitorService.auditCache[cacheKey] = Pair(updated, currentLyrics)

                updateAuditHeader(updated.isFlagged, updated.flagReason, updated.flaggedLines)
                currentLyrics?.let {
                    displayLyrics(it, updated.flaggedLines, updated.annotations)
                }

                Toast.makeText(this, "Song verdict updated", Toast.LENGTH_SHORT).show()
            }
            .setNegativeButton("Cancel", null)
            .show()
    }

    private fun dpToPx(dp: Int): Int {
        return (dp * resources.displayMetrics.density).toInt()
    }
}

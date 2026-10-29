# Alify Music Guard LLM Fine-Tuning Guide

This guide explains how to use the song data harvested by Alify to fine-tune a private, lightweight (3B parameter) on-device content moderation model.

---

## 1. Where Does the Data Come From?

As Alify audits songs during normal listening, [`SilentCorpusHarvester`](file:///c:/Users/HomePC/Desktop/Coded/Alify/Alify.Android/app/src/main/java/com/alify/spotify/SilentCorpusHarvester.kt) automatically saves records in the background:

* **SFT Dataset (`sft_ledger.jsonl`):** Contains every song audited by the AI in standard instruction-tuning format (`instruction`, `input` with lyrics, and `output` JSON).
* **DPO Dataset (`dpo_ledger.jsonl`):** Contains every song where a user manually adjusted a verdict or flagged a specific lyric line in `LyricsActivity`, pairing what the AI predicted (`rejected`) with what the user chose (`chosen`).

### Locations on Device:
* **Android:**
  * `Documents/Alify/sft_ledger.jsonl` (Publicly accessible via USB or Files app)
  * `Download/Alify/sft_ledger.jsonl`
  * `/sdcard/Android/media/com.alify.spotify/.vault/` (Survives app uninstalls)
* **Desktop:**
  * `%LocalAppData%\Alify\.vault\sft_ledger.jsonl`
  * `%LocalAppData%\Alify\.vault\dpo_ledger.jsonl`

---

## 2. Exporting the Harvested Dataset

Run the automated exporter script from your terminal:

```bash
python tools/export_corpus.py
```

This scans your local directories, removes duplicates, and generates two ready-to-train datasets inside `training_data/`:
1. `training_data/alify_sft_dataset.jsonl` (For Supervised Fine-Tuning)
2. `training_data/alify_dpo_dataset.jsonl` (For Direct Preference Optimization)

---

## 3. Training the Model (Free on Google Colab T4)

You do **not** need expensive GPU servers to train this model:
1. Open [Google Colab](https://colab.research.google.com/) and create a new notebook with a **free T4 GPU** runtime.
2. Upload `training_data/alify_sft_dataset.jsonl`.
3. Copy and run the code from [`tools/train_colab_recipe.py`](file:///c:/Users/HomePC/Desktop/Coded/Alify/tools/train_colab_recipe.py).
4. In **~15 minutes**, Unsloth will fine-tune `unsloth/Llama-3.2-3B-Instruct` and export:
   * **`alify-music-guard-3b-q4-Q4_K_M.gguf`** (Size: ~1.9 GB).

---

## 4. Deploying Your Custom Model into Alify

### On Desktop (via Ollama):
1. Create a `Modelfile`:
   ```dockerfile
   FROM ./alify-music-guard-3b-q4-Q4_K_M.gguf
   PARAMETER temperature 0.1
   ```
2. Build and start it:
   ```bash
   ollama create alify-guard -f Modelfile
   ollama run alify-guard
   ```
3. In Alify Desktop `appsettings.json`, point the OpenAI-compatible endpoint to `http://localhost:11434/v1`.

### On Android (Offline On-Device):
1. Place the `.gguf` file into the app's internal storage or assets.
2. `FilterEngine.kt` can query it directly via local JNI / llama.cpp with **0 internet calls and sub-200ms response time**.

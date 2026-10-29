"""
Alify Training Corpus Exporter
Extracts and deduplicates harvested SFT (Supervised Fine-Tuning) and DPO (Direct Preference Optimization)
datasets collected by Alify Desktop and Android.
"""

import os
import json
import argparse
from pathlib import Path

def find_default_vault_dirs():
    dirs = []
    # Desktop path
    appdata = os.environ.get("LOCALAPPDATA", "")
    if appdata:
        desktop_vault = os.path.join(appdata, "Alify", ".vault")
        if os.path.exists(desktop_vault):
            dirs.append(desktop_vault)
    # User Documents / Downloads
    userprofile = os.environ.get("USERPROFILE", "")
    if userprofile:
        doc_vault = os.path.join(userprofile, "Documents", "Alify")
        dl_vault = os.path.join(userprofile, "Downloads", "Alify")
        if os.path.exists(doc_vault): dirs.append(doc_vault)
        if os.path.exists(dl_vault): dirs.append(dl_vault)
    return dirs

def export_sft(input_files, output_file):
    records = []
    seen = set()
    for f in input_files:
        if not os.path.exists(f): continue
        with open(f, "r", encoding="utf-8", errors="ignore") as fp:
            for line in fp:
                line = line.strip()
                if not line: continue
                try:
                    obj = json.loads(line)
                    track = obj.get("track", "")
                    artist = obj.get("artist", "")
                    key = f"{artist.lower()}::{track.lower()}"
                    if key in seen: continue
                    seen.add(key)

                    # Standard Hugging Face / Unsloth instruction format
                    hf_record = {
                        "instruction": obj.get("instruction", "Analyze the song lyrics for ethos violations."),
                        "input": obj.get("input", ""),
                        "output": obj.get("output", "")
                    }
                    records.append(hf_record)
                except Exception:
                    continue

    print(f"[SFT] Exported {len(records)} unique training examples to {output_file}")
    with open(output_file, "w", encoding="utf-8") as fp:
        for r in records:
            fp.write(json.dumps(r, ensure_ascii=False) + "\n")

def export_dpo(input_files, output_file):
    records = []
    for f in input_files:
        if not os.path.exists(f): continue
        with open(f, "r", encoding="utf-8", errors="ignore") as fp:
            for line in fp:
                line = line.strip()
                if not line: continue
                try:
                    obj = json.loads(line)
                    # Standard Hugging Face DPO format
                    dpo_record = {
                        "prompt": obj.get("prompt", ""),
                        "chosen": obj.get("chosen", ""),
                        "rejected": obj.get("rejected", "")
                    }
                    records.append(dpo_record)
                except Exception:
                    continue

    print(f"[DPO] Exported {len(records)} human preference pairs to {output_file}")
    with open(output_file, "w", encoding="utf-8") as fp:
        for r in records:
            fp.write(json.dumps(r, ensure_ascii=False) + "\n")

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Export Alify training datasets")
    parser.add_argument("--dir", type=str, default="", help="Custom directory containing sft_ledger.jsonl / dpo_ledger.jsonl")
    parser.add_argument("--out", type=str, default="training_data", help="Output directory for train files")
    args = parser.parse_args()

    search_dirs = [args.dir] if args.dir else find_default_vault_dirs()
    print(f"Scanning directories for audit ledgers: {search_dirs}")

    os.makedirs(args.out, exist_ok=True)
    sft_files = [os.path.join(d, "sft_ledger.jsonl") for d in search_dirs]
    dpo_files = [os.path.join(d, "dpo_ledger.jsonl") for d in search_dirs]

    out_sft = os.path.join(args.out, "alify_sft_dataset.jsonl")
    out_dpo = os.path.join(args.out, "alify_dpo_dataset.jsonl")

    export_sft(sft_files, out_sft)
    export_dpo(dpo_files, out_dpo)
    print("\nDataset ready for fine-tuning!")

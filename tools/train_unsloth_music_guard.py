"""
Train Alify Music Guard LLM using Unsloth (Free Google Colab / Local GPU)
Fine-tunes Llama-3.2-3B or Qwen-2.5-3B on harvested song audit data and exports 4-bit GGUF for on-device deployment.
"""

import os

UNSLOTH_TRAIN_CODE = '''# ==============================================================================
# Alify-Music-Guard: 3B Fine-Tuning Recipe (Run on Google Colab T4 GPU)
# ==============================================================================

# 1. Install Unsloth and dependencies
# !pip install "unsloth[colab-new] @ git+https://github.com/unslothai/unsloth.git"
# !pip install --no-deps trl peft accelerate bitsandbytes

from unsloth import FastLanguageModel
import torch
from datasets import load_dataset
from trl import SFTTrainer
from transformers import TrainingArguments

max_seq_length = 2048
dtype = None # Auto detect Float16 or Bfloat16
load_in_4bit = True # 4bit quantization to fit in 6GB VRAM

# 2. Load Base Model (Llama 3.2 3B or Qwen 2.5 3B)
model, tokenizer = FastLanguageModel.from_pretrained(
    model_name = "unsloth/Llama-3.2-3B-Instruct",
    max_seq_length = max_seq_length,
    dtype = dtype,
    load_in_4bit = load_in_4bit,
)

# 3. Add LoRA Adapters
model = FastLanguageModel.get_peft_model(
    model,
    r = 16,
    target_modules = ["q_proj", "k_proj", "v_proj", "o_proj", "gate_proj", "up_proj", "down_proj"],
    lora_alpha = 16,
    lora_dropout = 0,
    bias = "none",
    use_gradient_checkpointing = "unsloth",
    random_state = 3407,
)

# 4. Format Dataset into ChatML Prompt
prompt_template = """<|begin_of_text|><|start_header_id|>system<|end_header_id|>
You are an expert computational sociolinguist and music content moderation engine. Output strictly valid JSON.<|eot_id|>
<|start_header_id|>user<|end_header_id|>
{instruction}

{input}<|eot_id|>
<|start_header_id|>assistant<|end_header_id|>
{output}<|eot_id|>"""

def formatting_prompts_func(examples):
    instructions = examples["instruction"]
    inputs       = examples["input"]
    outputs      = examples["output"]
    texts = []
    for instruction, input_text, output in zip(instructions, inputs, outputs):
        text = prompt_template.format(instruction=instruction, input=input_text, output=output)
        texts.append(text)
    return { "text" : texts }

dataset = load_dataset("json", data_files="alify_sft_dataset.jsonl", split="train")
dataset = dataset.map(formatting_prompts_func, batched = True)

# 5. Train Model
trainer = SFTTrainer(
    model = model,
    tokenizer = tokenizer,
    train_dataset = dataset,
    dataset_text_field = "text",
    max_seq_length = max_seq_length,
    dataset_num_proc = 2,
    packing = False,
    args = TrainingArguments(
        per_device_train_batch_size = 2,
        gradient_accumulation_steps = 4,
        warmup_steps = 10,
        max_steps = 300, # ~15-20 minutes on Colab
        learning_rate = 2e-4,
        fp16 = not torch.cuda.is_bf16_supported(),
        bf16 = torch.cuda.is_bf16_supported(),
        logging_steps = 10,
        optim = "adamw_8bit",
        weight_decay = 0.01,
        lr_scheduler_type = "linear",
        seed = 3407,
        output_dir = "alify-music-guard-checkpoints",
    ),
)
trainer_stats = trainer.train()

# 6. Export to 4-bit Quantized GGUF (for Desktop Ollama & Android Mobile Execution)
model.save_pretrained_gguf("alify-music-guard-3b-q4", tokenizer, quantization_method = "q4_k_m")
print("SUCCESS: Model saved as alify-music-guard-3b-q4-Q4_K_M.gguf!")
'''

def main():
    colab_file = os.path.join(os.path.dirname(__file__), "train_colab_recipe.py")
    with open(colab_file, "w", encoding="utf-8") as fp:
        fp.write(UNSLOTH_TRAIN_CODE)
    print(f"Colab training recipe written to {colab_file}")

if __name__ == "__main__":
    main()

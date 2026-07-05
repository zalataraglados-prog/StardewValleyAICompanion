# Training Hardware Assessment

Current training target: a small action-output model that emits `small_model_action.v1`, not a full end-to-end game-playing model.

## Model Size Recommendation

- First trainable target: `0.5B-1.5B`.
- Upper local experimentation target: `3B`.
- Defer `7B` until the sandbox executor produces dense feedback and the action schema is stable.

The action-output model only needs to choose registered option IDs and fill constrained parameters. It should not spend capacity learning game physics, raw controls, or map traversal; those belong in deterministic compilers, simulators, and verifiers.

## Hardware Tiers

| Tier | GPU | System RAM | Disk | Practical use |
| --- | --- | --- | --- | --- |
| Minimum | 8-12 GB VRAM | 32 GB | 200 GB free SSD | 0.5B-1.5B LoRA/QLoRA, short context, low batch |
| Recommended local | 16 GB VRAM | 64 GB | 500 GB free NVMe | 1.5B-3B LoRA/QLoRA, useful throughput |
| Strong local | 24 GB VRAM | 64-128 GB | 1 TB free NVMe | 3B comfortable, 7B QLoRA experiments |
| High-end local | 32 GB VRAM | 128 GB | 1-2 TB free NVMe | 7B QLoRA with more context/batch headroom |

NVIDIA/CUDA remains the lowest-friction path for PyTorch training. RTX 4090-class cards provide 24 GB VRAM; RTX 5090-class cards provide 32 GB VRAM.

Reference assumptions:

- NVIDIA RTX 4090 product specs list 24 GB memory.
- NVIDIA RTX 5090 product specs list 32 GB GDDR7 memory.
- Hugging Face TRL/PEFT documentation describes QLoRA as 4-bit base-model loading with trainable LoRA adapters to reduce memory requirements versus standard LoRA.

## Expected VRAM

Approximate local training guidance:

| Model | Inference 4-bit | LoRA/QLoRA training | Notes |
| --- | ---: | ---: | --- |
| 0.5B | 2-4 GB | 6-8 GB | Good first target |
| 1.5B | 3-6 GB | 8-12 GB | Best initial balance |
| 3B | 5-8 GB | 12-16 GB | Good once data loop works |
| 7B | 8-14 GB | 20-32 GB | Use only after feedback density is high |

Full fine-tuning is not recommended locally. Use LoRA/QLoRA first.

## Throughput Priorities

Training speed will be dominated by:

- sandbox episode generation rate,
- sequence length,
- batch size / gradient accumulation,
- checkpoint frequency,
- tokenizer/model IO.

For this project, increasing feedback density is more valuable than using a larger model early. A 1.5B model trained on high-quality action queue episodes is more useful than a 7B model trained on sparse or synthetic-only labels.

## Storage Layout

Recommended local layout:

- `datasets/episodes/`: compressed JSONL or Parquet episode records.
- `datasets/snapshots/`: before/after transparent snapshots, deduplicated by state hash.
- `checkpoints/action-output/`: LoRA adapters and merged candidates.
- `runs/`: metrics, eval results, failed compile samples.

Keep at least 500 GB free if running frequent sandbox rollouts. Use 1 TB+ if storing raw snapshots for many episodes.

## Training Phases

1. Schema adherence SFT: teach the model to emit valid `small_model_action.v1`.
2. Option selection SFT: choose correct registered `option_id`.
3. Preference/ranking: choose better action queues from candidate sets.
4. Reinforcement-style fine-tuning: only after sandbox feedback is reliable.

## Decision

Start with `1.5B QLoRA/LoRA` on a 12-16 GB VRAM GPU if available. If only 8 GB is available, start with `0.5B`. If 24 GB+ is available, still begin with `1.5B-3B`; reserve `7B` for later validation.

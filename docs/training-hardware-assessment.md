# Training Hardware And Model Assessment

Status date: 2026-07-27

The authoritative training-readiness sequence is documented in
[`FORMAL_FULL_TRAINING_READINESS_CN.md`](FORMAL_FULL_TRAINING_READINESS_CN.md).
This file records the hardware and model-sizing decision only.

## Target Training Laptop

User-reported configuration:

- CPU: AMD Ryzen 9 9955HX, 16 cores / 32 threads.
- System memory: 32 GB.
- GPU: GeForce RTX 5070 Laptop GPU, 8 GB GDDR7.
- Validation status: not yet verified on the target laptop.

The RTX 5070 Laptop GPU must not be sized as a desktop RTX 5070 or as a
12/16-GB laptop GPU. NVIDIA's official laptop specification is 8 GB.
Laptop-specific GPU TGP, sustained clocks, cooling, SSD capacity, and memory
upgrade support must be checked against the exact notebook model and measured
under load.

## Workload Split

The formal local baseline is a structured candidate ranker, not a language
model that emits raw controls:

- C# remains the authoritative feature, candidate, compiler, executor, and
  checkpoint integration surface.
- The model ranks evidence-eligible candidate IDs and fills constrained
  parameters.
- Movement, pathfinding, input timing, combat mechanics, safety checks, and
  action validation remain deterministic.
- Executor calibration rows never become policy labels.

The 9955HX is well suited to dataset projection, replay, compression, tree
training, and parallel environment evaluation. The 32-GB system-memory limit
requires streaming datasets, bounded worker counts, and avoiding simultaneous
residency of full raw snapshots and multiple models.

## Model Tiers

| Tier | Role | Decision on this laptop |
| --- | --- | --- |
| V0 deterministic baseline | Regression and label-quality reference | Required and already part of the system |
| V1 C# structured ranker | Formal full-policy baseline | Required; primary local training target |
| V2 0.6B-class constrained model | Optional comparison for candidate ID/parameter output | Feasible only after a 4-bit QLoRA memory smoke |
| 1.7B-class constrained model | Boundary experiment | Not default; attempt only after measured 0.6B headroom |
| 3B+ training | Larger neural policy experiment | Not a local target on 8-GB VRAM |

For V1, evaluate ML.NET LightGBM/FastTree ranking or an equivalent structured
learner. ML.NET ranking trainers currently do not export to ONNX, so either:

- persist and serve the native ML.NET checkpoint in the C# provider; or
- select an exportable classification/regression formulation if a
  cross-runtime checkpoint is a hard requirement.

For optional V2 experiments, use 4-bit QLoRA, short sequences, batch size 1,
gradient accumulation, and gradient checkpointing. The neural model receives a
compact typed candidate view, not a serialized whole-game snapshot, and its
output still passes through the C# schema, allowlist, compiler, and executor.

Full fine-tuning is not a local goal. A 1.7B experiment that spills heavily to
system RAM or destabilizes the game/evaluation loop is a failed hardware fit,
not a reason to weaken the formal V1 baseline.

## Storage And Runtime Budget

- Minimum free fast SSD space for structured training: 150 GB.
- Preferred free space when retaining long raw rollouts: 300 GB or more.
- Store deduplicated, compressed raw snapshots separately from projected
  training rows.
- Split datasets by save and game day, not by randomly sampled rows.
- Do not run the game server and optional GPU training concurrently until
  sustained CPU, RAM, VRAM, and thermal measurements show adequate headroom.

The previous blanket recommendation of 500 GB free space no longer applies to
the structured V1 route. Larger retained snapshot corpora may still justify an
archive volume.

## Hardware Acceptance

Before formal training:

1. Verify the exact GPU and 8-GB VRAM with `nvidia-smi`.
2. Lock the NVIDIA driver and any optional CUDA training-toolchain versions.
3. Run on AC power in a stable performance mode.
4. Record sustained temperature, power, throughput, system-memory peak, and
   VRAM peak.
5. Verify at least 150 GB of free fast storage and acceptable dataset I/O.
6. Train, save, reload, and compare one C# structured-ranker checkpoint.
7. If V2 is enabled, run a 0.6B-class single-batch 4-bit memory smoke first.

A short run satisfies infrastructure validation only. Formal training starts
after the evidence allowlist, policy dataset, provider, checkpoint manifest, and
offline evaluation gates all pass.

## Decision

Train the complete evidence-eligible policy dataset first with the V1
structured ranker. Keep the V0 deterministic policy as the frozen reference.
Treat a 0.6B-class constrained neural model as an optional comparative model,
not as the prerequisite for the first formal full training. Do not schedule
1.7B by default and do not schedule 3B+ local training on this laptop.

## Official References

- [AMD Ryzen 9 9955HX specifications](https://www.amd.com/en/products/processors/laptop/ryzen/9000-series/amd-ryzen-9-9955hx.html)
- [NVIDIA GeForce RTX 50 Series laptop specifications](https://www.nvidia.com/en-gb/geforce/laptops/50-series/)
- [Microsoft ML.NET algorithm selection](https://learn.microsoft.com/en-us/dotnet/machine-learning/how-to-choose-an-ml-net-algorithm)
- [Hugging Face bitsandbytes installation](https://huggingface.co/docs/bitsandbytes/installation)
- [Hugging Face Transformers bitsandbytes/QLoRA](https://huggingface.co/docs/transformers/main/quantization/bitsandbytes)
- [Qwen3 official repository](https://github.com/QwenLM/Qwen3)

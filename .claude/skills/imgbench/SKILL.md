---
name: imgbench
description: Benchmark OpenRouter image models (remotegen) on two fixed 3x3 sheets, roofs and cars, against the art already in the game, judge them blind, and write the result up as a PDF report. Use when asked to compare, benchmark or choose an image model for this project's art.
---

# imgbench

`.claude/skills/imgbench/bench.py` runs one brief per suite through every model it can reach,
measures each frame, lays out blind sheets for scoring by eye, and prints the judged result as a PDF.
Work goes to `.tmp/bench/`, frames to `.tmp/gen/frames/bench_<suite>_<tag>-vN.<ext>` (remotegen's scratch). Nothing
touches `assets/`.

| suite | sheet | cells | style picture |
|---|---|---|---|
| `bld` | roof sheet 9, its original prompt (`bld_prompt.txt`) | 9 prefabs at their footprints, thick street edge | sheet 8's in-game roofs |
| `car` | `car_prompt.txt` + a line per car | 9 game cars as rounded boxes at 100 px/m, corner radius per body type, thick nose edge | 6 other game cars |

## Commands

```bash
B=.claude/skills/imgbench/bench.py
$B prepare                      # layout, style, in-game composite, brief; prints each suite's brief hash
$B models                       # local + OpenRouter roster, frames ok/tried on the current brief and setup
$B sweep [-n 2] [--models remote|ID,..] [--skip ID,..] [--jobs 4] [--reserve 0.5]
$B run --suite car --model ID [-n 2] [--extra "nudge"]
$B score [--suite car] [--cells]  # .tmp/bench/results.md, .tmp/bench/<suite>/compare_<tag>.png
$B judge [--models ID,..]         # blind sheets, 2x2 contact sheets, the card .tmp/bench/judge.md
$B reveal                         # .tmp/bench/judged.md
.tmp/venv/bin/python $B report    # .tmp/bench/report.md -> report.pdf (needs `markdown`, Chromium)
```

`sweep` is resumable and breadth first: one frame per model and suite, cheapest model first, then a
retry pass for what the service failed, and only then the second frames and their retry pass. A remote
frame is not retried inside itself (`--retries 0`), so money that runs out mid-sweep still leaves every
model its first frames. It stops at `--reserve` of credit left, gives a
model no more frames after one dearer than `--max-frame-usd`, and stops everything on a 401/402/403.
Run one sweep at a time: local frames take `.tmp/bench/local.lock`, but two sweeps still double the
remote spend.

**Local models are not run** (the owner, 2026-10-18: they never made a worthwhile frame). `--models` is
`remote` by default; `local` and `all` exist for the frames already taken and are not to be passed.

A benchmark is the one place models and settings vary, and it runs only when the owner asks for one. Its
remote frames pass remotegen `--past-cap`: the sweep bounds its own frames, not remotegen's three attempts.

## What makes it fair

- Same brief, same aspect, same seed per frame index, and as many of the two pictures as a model takes:
  two (`ref`); one, the layout, with the style in words (`layout`); none, the rules and cell list as
  text (`text`). Most remote models take no seed; the record keeps what was sent (`settings`).
- Remote frames go through OpenRouter's Image API at the model's own resolution and quality.
- Local frames run one at a time across processes, each on a fresh server (a used one holds ~6 GB),
  3 GB kept for the desktop, VAE decoded on the CPU, at each maker's template steps and cfg; the model
  load is in their time. LongCat's text encoder cuts the brief at 512 tokens: it never reads the last
  cells' lines.
- Every attempt is a record in `.tmp/bench/runs.jsonl`, classified when read: the model's own failure
  (refusal, no image, SVG, unsupported request, server died mid-prompt) counts in `ok/tried`; the
  service's or network's (429, 5xx, dropped reply) is retried and not counted; a provider's own quota
  (`unavailable`) is neither counted nor retried.
- A record carries its `setup`; frames of another setup or brief are never compared. A local model whose
  graph changes gets a `revision`, which leaves its older frames out.
- A nudge is the judge's, not the sweep's: after every model has its plain frames, look at a model's
  frames and add a line for that model alone (`run --extra "..."`). Nudged frames are a separate row
  (`model +nudge`) in `score` and `reveal`; they go on the card like any frame, and the report marks a
  best frame that was nudged, with its line.
- Time is the generator's own clock to the image, without retry sleeps. Local frames cost 0.

## Judging and the report

**The judge is you, by eye.** `score`'s figures — IoU at the outline, and with each drawing centred on
its outline the shape IoU, paint outside and holes; aspect error; mirror — are evidence, never the
score. Fill `.tmp/bench/judge.md` from the blind sheets only, 0–5 on style, detail, brief, shape and
clean; do not open `judge_key.json`, `results.md`, the compare sheets or a report until the card is
filled. Then write `.tmp/bench/verdict.md` (the verdict, a few lines) and `.tmp/bench/notes.md` (a
`## <model id>` heading per model with its pros and cons), and run `report`.

The report is image first: the verdict; per sheet the inputs, then every model's best frame, ranked;
the ranking over both sheets, each model with its two frames and its pros and cons; the numbers and
the briefs last. Frames are the generated files, unedited.

## Metadata in the pictures

Every PNG frame carries a `bench` text chunk (suite, model, brief, seed, setup, settings, seconds, cost,
size). Compare sheets carry a visible caption and the same chunk; blind sheets carry a caption and chunk
without the model. `python3 -c "from PIL import Image; print(Image.open(P).text)"` reads it.

## Known

- `klein9b` is gated on Hugging Face (401): needs the owner's token and licence acceptance.
- `google/gemini-nano-banana-2.1` is refused upstream for the provider's own quota (2026-10-18).
- `black-forest-labs/flux.2-klein-4b` on OpenRouter is the local `klein` at full precision.
- Output size is each model's own: Seedream returns 2048 px, most others ~1 MP. Judge at game scale.
- The GPU reset that took the desktop down (2026-10-18) came with two sweeps on one server; OmniGen2's was
  the server that died. It is swept last.

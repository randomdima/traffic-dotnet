---
name: imgbench
description: Benchmark OpenRouter image models on fixed sheets (roofs, cars, trains, one locomotive) against the art already in the game, judge them blind, and write the result up as a PDF report. Only when the owner asks to compare, benchmark or choose an image model; never part of generating art, which is remotegen's.
---

# imgbench

`.claude/skills/imgbench/bench.py` runs one brief per suite through every OpenRouter image model, measures
each frame, lays out blind sheets for scoring by eye, and prints the judged result as a PDF. Everything goes
to `.tmp/bench/`, frames to `.tmp/bench/frames/bench_<suite>_<tag>-vN.<ext>`, every attempt to
`.tmp/bench/runs.jsonl`.

**Apart from generation.** It shares only remotegen's bare client (`../remotegen/openrouter.py`: key,
catalogue, billed request). remotegen's settings, frames, index and three-attempt cap are not involved, and
nothing here touches `assets/` or `raw_assets/`. A benchmark is the one place models and settings vary, and
it runs only when the owner asks for one; its result changes remotegen's defaults only by the owner's word.

| suite | sheet | cells | style picture |
|---|---|---|---|
| `bld` | roof sheet 9, its original prompt (`bld_prompt.txt`) | 9 prefabs at their footprints, thick street edge | sheet 8's in-game roofs |
| `car` | `car_prompt.txt` + a line per car | 9 game cars as rounded boxes at 100 px/m, corner radius per body type, thick nose edge | 6 other game cars |
| `train` | `train_prompt.txt` + a line per vehicle | 9 rail vehicles the game lacks, at 21 px/m, thick cab edge | the car style sheet |
| `loco` | `loco_prompt.txt`, one train cell | one locomotive at the model's smallest resolution: one asset a frame against nine | the car style sheet |

`report` covers `bld` and `car`; the train suites are written up with `tries`.

## Commands

```bash
B=.claude/skills/imgbench/bench.py
$B prepare                      # layout, style, in-game composite, brief; prints each suite's brief hash
$B models                       # every image model, frames ok/tried on the current brief and setup
$B sweep [-n 2] [--models ID,..] [--skip ID,..] [--jobs 4] [--reserve 0.5]
$B run --suite car --model ID [-n 2] [--extra "nudge"] [--quality Q | --cheapest]
$B score [--suite car] [--cells]  # .tmp/bench/results.md, .tmp/bench/<suite>/compare_<tag>.png
$B judge [--models ID,..]         # blind sheets, 2x2 contact sheets, the card .tmp/bench/judge.md
$B reveal                         # .tmp/bench/judged.md
$B tries --suite train            # <suite>/tries.pdf: a page per try, verdicts from <suite>/tries.json
.tmp/venv/bin/python $B report    # .tmp/bench/report.md -> report.pdf (needs `markdown`, Chromium)
.tmp/venv/bin/python $B pdf FILE.md
```

`sweep` is resumable and breadth first: one frame per model and suite, cheapest model first, then a retry
pass for what the service failed, and only then the second frames and their retry pass. A frame is not
retried inside itself, so money that runs out mid-sweep still leaves every model its first frames. It stops
at `--reserve` of credit left, gives a model no more frames after one dearer than `--max-frame-usd`, and
stops everything on a 401/402/403. Run one sweep at a time: two double the spend.

## What makes it fair

- Same brief, same aspect, same seed per frame index, and as many of the two pictures as a model takes:
  two (`ref`); one, the layout, with the style in words (`layout`); none, the rules and cell list as text
  (`text`). Most models take no seed; the record keeps what was sent (`settings`).
- Frames go through OpenRouter's Image API at the model's own resolution and quality (`--quality auto`),
  unless the suite (`loco`) or `run` asks otherwise.
- Every attempt is a record, classified when read: the model's own failure (refusal, no image, SVG,
  unsupported request) counts in `ok/tried`; the service's or network's (429, 5xx, dropped reply) is
  retried and not counted; a provider's own quota (`unavailable`) is neither counted nor retried.
- A record carries its `setup` and brief hash; frames of another setup or brief are never compared.
- A nudge is the judge's, not the sweep's: after every model has its plain frames, look at a model's frames
  and add a line for that model alone (`run --extra "..."`). Nudged frames are a separate row
  (`model +nudge`) in `score` and `reveal`; the report marks a best frame that was nudged, with its line.
- Time is the request's own clock to the image, without retry sleeps.

## Judging and the report

**The judge is you, by eye.** `score`'s figures — IoU at the outline, and with each drawing centred on its
outline the shape IoU, paint outside and holes; aspect error; mirror — are evidence, never the score. Fill
`.tmp/bench/judge.md` from the blind sheets only, 0–5 on style, detail, brief, shape and clean; do not open
`judge_key.json`, `results.md`, the compare sheets or a report until the card is filled. Then write
`.tmp/bench/verdict.md` (the verdict, a few lines) and `.tmp/bench/notes.md` (a `## <model id>` heading per
model with its pros and cons), and run `report`.

The report is image first: the verdict; per sheet the inputs, then every model's best frame, ranked; the
ranking over both sheets, each model with its two frames and its pros and cons; the numbers and the briefs
last. Frames are the generated files, unedited.

Every PNG frame carries a `bench` text chunk (suite, model, brief, seed, setup, settings, seconds, cost,
size); compare sheets carry a visible caption and the same chunk, blind sheets one without the model.
`python3 -c "from PIL import Image; print(Image.open(P).text)"` reads it.

## Known

- `google/gemini-nano-banana-2.1` is refused upstream for the provider's own quota (2026-10-18).
- Output size is each model's own: Seedream returns 2048 px, most others ~1 MP. Judge at game scale.

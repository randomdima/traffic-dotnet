// The page's opening: the card that stands in front of the canvas while the runtime, the figures and
// the town's art are on their way, and the sentence that replaces it when this browser cannot run any
// of it. It is the browser's half of `say` (src/runtime/web/WebGpu.cs) for as long as it is up.
//
// **Everything it can refuse, it refuses before the runtime is downloaded.** A page that spends four
// megabytes and half a minute to say "no WebGPU here" has wasted the one thing the reader had.

const card = () => document.getElementById('opening');

/// Whether the opening is still up. `say` writes here while it is and into the banner under the canvas
/// once it is gone, so a town that shuts down says so where a standing town's messages go.
export function isOpen() {
    return card() !== null;
}

/// What is happening now, in the words the desktop would have put on stdout.
export function stage(line) {
    const where = document.getElementById('opening-stage');
    if (where) where.textContent = line;
}

/// How far through a countable stage this is. A total of zero is a stage nothing can count — the
/// runtime coming down, a town standing up — and the bar sweeps instead of filling.
export function progress(done, total) {
    const bar = document.getElementById('opening-bar');
    const fill = document.getElementById('opening-fill');
    if (!bar || !fill) return;

    const counted = total > 0;
    bar.classList.toggle('sweeping', !counted);
    fill.style.width = counted ? `${Math.round((done / total) * 100)}%` : '';
    detail(counted ? `${done} of ${total}` : '');
}

/// The line under the bar: a count, a size, whatever the stage can say about itself.
export function detail(line) {
    const where = document.getElementById('opening-detail');
    if (where) where.textContent = line;
}

/// The opening, taken away. Called on the first empty `say`, which is the boot saying the town is
/// standing — or the menu is up and waiting to be clicked.
///
/// **What it cost is said on the way out**, because it is the one figure this head cannot take with
/// `--shot` and the one every change to the boot is judged on: the console is where a driven browser
/// reads it (`qq web`), and it is measured from the navigation rather than from anything here.
export function opened() {
    if (!card()) return;

    card().remove();
    console.log(`the page opened in ${(performance.now() / 1000).toFixed(1)} s`);
}

/// What this page cannot do, said in place of the bar and left there. **The reader is told what to do
/// about it**: a browser without WebGPU is a browser, not a fault, and naming one that has it is the
/// difference between a dead page and a page somebody opens somewhere else.
export function trouble(what, because) {
    const held = card();
    if (!held) return;

    held.classList.add('trouble');
    stage(what);
    document.getElementById('opening-bar')?.remove();
    detail(because);
}

/// Bytes off the wire while a stage is running, for the one stage that cannot count its files: the
/// runtime is fetched by the loader itself and this is the only place that sees the sizes.
///
/// **It is a wrapper and not a measurement of anything**: it counts what resolves, puts a figure under
/// the sweeping bar, and takes itself off again the moment the stage it was installed for is over.
export function counting() {
    const original = globalThis.fetch;
    let bytes = 0;

    globalThis.fetch = async (...given) => {
        const response = await original(...given);
        const size = Number(response.headers.get('content-length') ?? 0);
        if (size > 0) {
            bytes += size;
            detail(`${(bytes / (1024 * 1024)).toFixed(1)} MB`);
        }

        return response;
    };

    return () => {
        globalThis.fetch = original;
    };
}

// The page's opening: the card that stands in front of the canvas while the runtime, the figures and
// the town's art are on their way and while each town is stood up, and the sentence that replaces it when
// this browser cannot run any of it. It is the browser's half of `say` (src/runtime/web/WebGpu.cs) for as
// long as it is up.
//
// **Everything it can refuse, it refuses before the runtime is downloaded.** A page that spends four
// megabytes and half a minute to say "no WebGPU here" has wasted the one thing the reader had.

const card = () => document.getElementById('opening');

/// Whether the opening is up. `say` writes here while it is and into the banner under the canvas
/// once it is gone, so a town that shuts down says so where a standing town's messages go.
export function isOpen() {
    return card()?.hidden === false;
}

/// What is happening now, in the words the desktop would have put on stdout.
export function stage(line) {
    const where = document.getElementById('opening-stage');
    if (where) where.textContent = line;
}

/// How far through a countable stage this is. A total of zero is a stage nothing can count — the
/// runtime coming down, a town standing up — and the bar sweeps instead of filling.
export function progress(done, total) {
    const counted = total > 0;
    fillTo(counted ? done / total : null, 0.12);
    detail(counted ? `${done} of ${total}` : '');
}

/// The bar filled to a share of it over so many seconds, or swept for no share. **A transform and not a
/// width**: the browser runs a transform's transition on its compositor, so the bar goes on moving through a
/// stage that holds the page's one thread, where a width would stand still until the stage was over.
function fillTo(share, seconds) {
    const bar = document.getElementById('opening-bar');
    const fill = document.getElementById('opening-fill');
    if (!bar || !fill) return;

    bar.classList.toggle('sweeping', share === null);
    fill.style.transition = share === null ? '' : `transform ${seconds}s cubic-bezier(0.2, 0.6, 0.4, 1)`;
    fill.style.transform = share === null ? '' : `scaleX(${Math.min(1, Math.max(0, share))})`;
}

/// **How far through a town's open each stage it pauses after is**, as a share of the whole, and what the
/// work up to it is called — OdesaOsm's open on the page (49 s, 2026-10-15), the longest a page ships. A
/// smaller town's stages pass sooner and in the same order; a stage not named here moves nothing.
const STANDING = [
    ['plan: zones', 0.002, 'reading the map'],
    ['plan: water', 0.010, 'laying the water'],
    ['plan: streets', 0.091, 'laying the streets'],
    ['plan: paving', 0.097, 'paving the streets'],
    ['plan: ground', 0.298, 'shaping the ground'],
    ['plan: buildings', 0.479, 'putting up the buildings'],
    ['plan: props', 0.535, 'setting out the props'],
    ['plan: roster', 0.535, 'finding everyone a place'],
    ['town: roads', 0.539, 'joining up the roads'],
    ['town: pavement', 0.739, 'laying the pavements, the zebras and the lights'],
    ['town: walking, the ways', 0.776, 'routing the walks'],
    ['town: the atlas filed', 0.922, 'marking where the ways meet'],
    ["the town's watches", 0.955, 'standing everyone in the town'],
    ["the world the device's", 0.957, 'handing the town to the GPU'],
    ['the ground', 0.994, 'drawing the ground'],
];

/// The open measured above, which the first stages' shares are guessed against until enough has passed to
/// guess the open's own length from how long it has taken to get there.
const STANDING_S = 49;
const GUESSED_PAST_SHARE = 0.05;

let standingFrom = 0;

/// The card brought back over the canvas for a town about to be stood up, the bar empty.
export function standUp(map) {
    const held = card();
    if (!held) return;

    held.hidden = false;
    held.classList.remove('trouble');
    stage(`opening ${map}…`);
    detail('');
    fillTo(null, 0);
    standingFrom = performance.now();
}

/// A stage of the open passed: the bar to where the open is now, and on toward the next stage over as long
/// as that stage is guessed to take — which it does on the compositor while the next stage holds the thread.
export function standing(said) {
    const at = STANDING.findIndex(([name]) => said.startsWith(name));
    if (at < 0 || !isOpen()) return;

    const share = STANDING[at][1];
    const next = STANDING[at + 1];
    const tookS = (performance.now() - standingFrom) / 1000;
    const wholeS = share >= GUESSED_PAST_SHARE ? tookS / share : STANDING_S;
    stage(next ? `${next[2]}…` : 'putting it on screen…');
    fillTo(next ? next[1] : 1, Math.max(0.12, ((next ? next[1] : 1) - share) * wholeS));
}

/// Once the browser has painted: the frame's callback runs before the paint, and a task queued from it after.
/// At once in a tab nobody can see, which gets no frames and would hold the open until somebody looked.
export function painted() {
    if (document.hidden) return Promise.resolve();

    return new Promise(done => requestAnimationFrame(() => setTimeout(done, 0)));
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
    if (!isOpen()) return;

    const first = !card().dataset.opened;
    card().hidden = true;
    card().dataset.opened = 'yes';
    if (first) console.log(`the page opened in ${(performance.now() / 1000).toFixed(1)} s`);
}

/// What this page cannot do, said in place of the bar and left there. **The reader is told what to do
/// about it**: a browser without WebGPU is a browser, not a fault, and naming one that has it is the
/// difference between a dead page and a page somebody opens somewhere else.
export function trouble(what, because) {
    const held = card();
    if (!held) return;

    held.hidden = false;

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

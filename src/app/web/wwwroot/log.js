// What the page said, kept from its first line, and a button to take it away as text that shows at the first error.
//
// **A handset has no console anybody reads**, and a fault seen only on one is a fault nobody can answer: so everything
// the console is told — the runtime's own output with it, which is the desktop's stdout (WEB-3) — is kept here as well,
// and the first error puts a button in front of the page that saves it, with what the browser and its device are.
//
// **It is imported before anything else** (main.js), because what it does not see from the start it never sees: the
// console is wrapped as this module is evaluated, and the runtime, imported later, writes through the wrapped one.

import { build } from './build.js'

/// The most lines kept; past it the oldest go, as a page left running for an hour would otherwise keep every line.
const MOST_LINES = 20000;

const lines = [];
const opened = performance.now();
let dropped = 0;

/// What the page knows of itself beyond what it said, each asked as the log is saved: a function answering lines, or
/// the promise of them (town.js hands over the device).
const contexts = [];

export function addContext(lines) {
    contexts.push(lines);
}

for (const level of ['log', 'info', 'warn', 'error', 'debug']) {
    const original = console[level].bind(console);
    console[level] = (...args) => {
        keep(level, args);
        if (level === 'error') offer();
        original(...args);
    };
}

addEventListener('error', e => {
    keep('uncaught', [e.error ?? e.message, `at ${e.filename}:${e.lineno}:${e.colno}`]);
    offer();
});

addEventListener('unhandledrejection', e => {
    keep('unhandled rejection', [e.reason]);
    offer();
});

function keep(level, args) {
    const seconds = ((performance.now() - opened) / 1000).toFixed(3).padStart(9);
    lines.push(`${seconds} ${level.padEnd(5)} ${args.map(text).join(' ')}`);
    if (lines.length > MOST_LINES) {
        lines.shift();
        dropped++;
    }
}

function text(arg) {
    if (arg instanceof Error) return arg.stack ?? `${arg.name}: ${arg.message}`;
    if (typeof arg !== 'object' || arg === null) return String(arg);

    try {
        return JSON.stringify(arg);
    } catch {
        return String(arg);
    }
}

/// The button, put up once and left up: a second error is in the same log.
function offer() {
    if (document.getElementById('save-log')) return;
    if (!document.body) {
        addEventListener('DOMContentLoaded', offer, { once: true });
        return;
    }

    const button = document.createElement('button');
    button.id = 'save-log';
    button.type = 'button';
    button.textContent = 'Something went wrong — download the log';
    button.addEventListener('click', save);
    document.body.append(button);
}

async function save() {
    const head = [
        `traffic-dotnet ${build.version} (${build.commit}) on ${build.runtime}`,
        `saved ${new Date().toISOString()}, ${((performance.now() - opened) / 1000).toFixed(1)} s after the page opened`,
        `page ${location.href}`,
        `browser ${navigator.userAgent}`,
        `screen ${screen.width}x${screen.height} points, window ${innerWidth}x${innerHeight}, pixel ratio ${devicePixelRatio}`,
        `cores ${navigator.hardwareConcurrency ?? '?'}, memory ${navigator.deviceMemory ?? '?'} GB`,
    ];

    for (const context of contexts) {
        try {
            head.push('', ...await context());
        } catch (e) {
            head.push('', `(a context could not be read: ${text(e)})`);
        }
    }

    const said = dropped > 0 ? [`(the first ${dropped} lines are gone)`, ...lines] : lines;
    const file = new Blob([[...head, '', ...said, ''].join('\n')], { type: 'text/plain' });
    const link = document.createElement('a');
    link.href = URL.createObjectURL(file);
    link.download = `traffic-dotnet-${new Date().toISOString().replace(/[:.]/g, '-')}.txt`;
    link.click();
    setTimeout(() => URL.revokeObjectURL(link.href), 10000);
}

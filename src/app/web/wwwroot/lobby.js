// **The lobby** (MP-15): what a page served by a town (PageHost) asks before it joins — a name, a colour and the
// server, which is the page's own host unless changed. What is picked goes to the engine as its command line
// (--join, --name, --colour; Boot.cs `Options`) and never into the address, which is the town's own already.

/// The header a served town marks every file of its page with (PageHost.ServedTown).
const SERVED = 'X-Served-Town';

/// Whether this page was handed out by a town rather than served on its own: one HEAD of the page's root.
export async function servedHere() {
    try {
        const answer = await fetch('./', { method: 'HEAD', cache: 'no-store' });
        return answer.headers.has(SERVED);
    } catch {
        return false;
    }
}

// **What a name may be is the wire's** (SeatCard): printable ASCII, at most 24 characters. Anything else is
// dropped here, as it would be before the hello, so what is shown is what is sent.
const LONGEST_NAME = 24;
const printable = text => text.replace(/[^\x20-\x7e]/g, '').slice(0, LONGEST_NAME);

const FIRST = ['Kurva', 'Turbo', 'Dizzy', 'Sneaky', 'Rusty', 'Grumpy', 'Lucky', 'Mighty', 'Sleepy', 'Wobbly',
    'Crispy', 'Fuzzy', 'Cosmic', 'Zippy', 'Salty', 'Jolly', 'Speedy', 'Bumpy', 'Honky', 'Lazy'];
const SECOND = ['Bober', 'Wombat', 'Gopher', 'Badger', 'Otter', 'Ferret', 'Llama', 'Moose', 'Pelican', 'Walrus',
    'Hamster', 'Narwhal', 'Capybara', 'Yak', 'Goose', 'Lemur', 'Panda', 'Gecko', 'Marmot', 'Pigeon'];
const pick = list => list[Math.floor(Math.random() * list.length)];
const randomName = () => `${pick(FIRST)} ${pick(SECOND)}`;

// Hues round the wheel, every one saturated: the town raises a grey to a colour anyway (SeatColour), and a
// swatch that is changed after it is picked would be a swatch that lied.
const HUES = [0, 28, 52, 95, 140, 175, 200, 225, 265, 295, 325];
const hex = (h, s = 0.85, l = 0.55) => {
    const f = n => {
        const k = (n + h / 30) % 12;
        const c = l - s * Math.min(l, 1 - l) * Math.max(-1, Math.min(k - 3, 9 - k, 1));
        return Math.round(c * 255).toString(16).padStart(2, '0');
    };
    return `#${f(0)}${f(8)}${f(4)}`;
};

// The last name and colour this browser joined with, where it will say.
const remembered = key => { try { return localStorage.getItem(key); } catch { return null; } };
const remember = (key, value) => { try { localStorage.setItem(key, value); } catch { } };

/// **The lobby shown in place of the opening until CONNECT**, and the engine's arguments it settles on.
export function lobby() {
    const form = document.getElementById('lobby');
    const opening = document.getElementById('opening');
    const name = document.getElementById('lobby-name');
    const colour = document.getElementById('lobby-colour');
    const server = document.getElementById('lobby-server');
    const swatches = document.getElementById('lobby-swatches');
    const preview = document.getElementById('lobby-preview');
    const connect = document.getElementById('lobby-connect');

    server.value = `${location.protocol === 'https:' ? 'wss' : 'ws'}://${location.host}`;
    name.value = printable(remembered('player.name') ?? '') || randomName();
    const kept = remembered('player.colour') ?? '';
    colour.value = /^#[0-9a-f]{6}$/i.test(kept) ? kept : hex(pick(HUES));

    for (const h of HUES) {
        const swatch = document.createElement('button');
        swatch.type = 'button';
        swatch.className = 'swatch';
        swatch.setAttribute('role', 'radio');
        swatch.value = hex(h);
        swatch.style.background = swatch.value;
        swatch.title = swatch.value;
        swatch.addEventListener('click', () => { colour.value = swatch.value; show(); });
        swatches.insertBefore(swatch, colour);
    }

    function show() {
        preview.textContent = name.value.trim();
        preview.style.color = colour.value;
        for (const swatch of swatches.querySelectorAll('.swatch'))
            swatch.setAttribute('aria-checked', String(swatch.value === colour.value.toLowerCase()));
        connect.disabled = name.value.trim() === '' || server.value.trim() === '';
    }

    name.addEventListener('input', () => {
        const cleaned = printable(name.value);
        if (cleaned !== name.value) name.value = cleaned;
        show();
    });
    colour.addEventListener('input', show);
    server.addEventListener('input', show);
    document.getElementById('lobby-dice').addEventListener('click', () => { name.value = randomName(); show(); });
    show();

    opening.hidden = true;
    form.hidden = false;
    name.focus();

    return new Promise(settle => form.addEventListener('submit', event => {
        event.preventDefault();
        const chosen = name.value.trim();
        remember('player.name', chosen);
        remember('player.colour', colour.value);
        form.hidden = true;
        opening.hidden = false;
        settle(['--join', server.value.trim(), '--name', chosen, '--colour', colour.value.slice(1)]);
    }, { once: true }));
}

// The page's entry: say what this browser cannot do, and if it can do all of it, start the runtime,
// hand it the machine and let its own Main do the rest. Every decision about what happens after that
// is in C#, exactly as it is on the desktop.
//
// **The refusals come first, and the runtime is imported dynamically for that reason.** A static import
// is fetched before the first line of this file runs, so a browser with no WebGPU would download four
// megabytes of engine to be told the engine cannot start. Everything checked here is checked in
// milliseconds and against no download at all.

import { town } from './town.js'
import { counting, detail, stage, trouble } from './loading.js'

const refused = missing();
if (refused) {
    trouble(refused.what, refused.because);
} else {
    // **Both at once.** Asking for an adapter takes tens of milliseconds and importing the runtime
    // takes a round trip, and neither needs the other's answer. Importing it is not yet downloading
    // the engine — that begins at `create` below — so a machine that turns out to have no device has
    // spent a module and not nine megabytes.
    stage('asking for a graphics device…');
    const asked = navigator.gpu.requestAdapter().catch(() => null);
    const engine = import('./_framework/dotnet.js');

    if (await asked === null) {
        trouble(
            'This browser has WebGPU but no device it will hand out.',
            'That is usually a driver the browser has blocklisted, or hardware acceleration switched ' +
            'off. On Linux it is often behind chrome://flags/#enable-unsafe-webgpu.');
    } else {
        stage('starting the engine…');
        const stop = counting();

        const { dotnet } = await engine;
        const { setModuleImports, runMain } = await dotnet.withApplicationArguments(...arguments_()).create();

        stop();
        detail('');
        setModuleImports('town.js', { town });

        await runMain();
    }
}

/// What this browser is missing, or nothing. **Every question here is answered against no download at
/// all**, and they are ordered so the reader is told the first thing that is true — a browser old
/// enough to want the second answer usually wants the first as well. The device is asked for above,
/// because that one is a promise and can be waited on beside something else.
function missing() {
    if (typeof WebAssembly !== 'object' || typeof WebAssembly.instantiate !== 'function') {
        return {
            what: 'This browser has no WebAssembly.',
            because: 'The whole engine is compiled to it. Any browser from the last decade will run this page.',
        };
    }

    // The build compiles with WasmEnableSIMD, so a runtime without it does not load at all — and this
    // is the shortest module that uses one instruction: (module (func (result v128) (v128.const i32x4 0 0 0 0)))
    const simd = new Uint8Array([
        0, 97, 115, 109, 1, 0, 0, 0, 1, 5, 1, 96, 0, 1, 123, 3, 2, 1, 0, 10, 22, 1,
        20, 0, 253, 12, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 11,
    ]);
    if (!WebAssembly.validate(simd)) {
        return {
            what: 'This browser has no WebAssembly SIMD.',
            because: 'The engine is compiled with it. A current Chrome, Edge, Firefox or Safari has it.',
        };
    }

    if (!navigator.gpu) {
        return {
            what: 'This browser has no WebGPU.',
            because: 'The town is drawn with it and there is no fallback. Chrome or Edge 113 and later ' +
                'have it everywhere; Safari from 26; Firefox from 141 on Windows and 145 elsewhere.',
        };
    }

    return null;
}

// The command line, spelled as a query string: ?map=Test&ui=nodes,paths reads as --map Test --ui …,
// so a link to a town is the same words the desktop takes.
function arguments_() {
    const query = new URLSearchParams(location.search);
    const args = [];
    for (const [name, value] of query) {
        args.push(`--${name}`);
        if (value !== '') args.push(...value.split(' '));
    }

    return args;
}

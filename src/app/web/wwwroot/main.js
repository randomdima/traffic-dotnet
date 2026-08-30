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

const refused = await unsupported();
if (refused) {
    trouble(refused.what, refused.because);
} else {
    stage('starting the engine…');
    const stop = counting();

    const { dotnet } = await import('./_framework/dotnet.js');
    const { setModuleImports, runMain } = await dotnet.withApplicationArguments(...arguments_()).create();

    stop();
    detail('');
    setModuleImports('town.js', { town });

    await runMain();
}

/// What this browser is missing, or nothing. **Ordered so the reader is told the first thing that is
/// true**, since a browser old enough to want the second answer usually wants the first as well.
async function unsupported() {
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

    // Having the API is not having a device. A machine with no usable adapter answers null here, which
    // is the same refusal the run would reach after the download rather than before it.
    stage('asking for a graphics device…');
    const adapter = await navigator.gpu.requestAdapter().catch(() => null);
    if (!adapter) {
        return {
            what: 'This browser has WebGPU but no device it will hand out.',
            because: 'That is usually a driver the browser has blocklisted, or hardware acceleration ' +
                'switched off. On Linux it is often behind chrome://flags/#enable-unsafe-webgpu.',
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

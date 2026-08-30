// The page's entry: start the runtime, hand it the machine, and let its own Main do the rest. Every
// decision about what happens after this is in C#, exactly as it is on the desktop.

import { dotnet } from './_framework/dotnet.js'
import { town } from './town.js'

const { setModuleImports, runMain } = await dotnet.withApplicationArguments(...arguments_()).create();

setModuleImports('town.js', { town });

await runMain();

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

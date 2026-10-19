// A kernel as the page compiles it, written out so naga can be asked whether Firefox will take it (Kernels.Web.targets):
// the push block a uniform is held to rules a push constant is not. The rewrite is compute.js's own `kernelSource`.
//
// usage: node page_kernel.mjs <compute.js> <naga's .wgsl> <the page's .wgsl>
import { readFileSync, writeFileSync } from 'node:fs';

const [compute, from, to] = process.argv.slice(2);

// Loaded as a data URL because a .js outside a package marked "type": "module" is not a module to every Node.
const { kernelSource } = await import('data:text/javascript,' + encodeURIComponent(readFileSync(compute, 'utf8')));
writeFileSync(to, kernelSource(readFileSync(from, 'utf8')));

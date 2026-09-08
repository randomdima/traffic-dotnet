# The solver — requirements

**What this project's own physics has to be, and what it must not become.** What the *town* requires of a
physics layer is [requirements.md](requirements.md); why the solver is written here rather than taken is
[decision-log.md](decision-log.md).

`SOL-` is the prefix and no ID here is ever renumbered.

## Scope

**In.** The broad phase over the town, the narrow phase for the two shapes this town has, the contact
solver, the contact-begin report, the ray cast, the static-overlap query, and the body table those read.

**Out, and the exclusion list is the requirement.** Every row below would be reasonable in a physics
library and is wrong here; adding one back is a change to this document first.

| Not provided | Because |
|---|---|
| Joints, motors, springs | Nothing in the town is jointed. A car is one body |
| Sensors, triggers, overlap events | The town asks its own indexes what is near what |
| Shapes beyond the rounded oriented box | OBJ-2's five bodies are all of them. Neither the radius nor the coreless disc is a second shape: same axes, same separating axis, same clip |
| Restitution | A collision here is a crash, not a break shot |
| Continuous collision / sweeping | A stated limit rather than a divergence |
| Islands, sleeping | Every contact of every tick is run; a saving forgone, not a behaviour |
| Multi-threading | Determinism is worth more at this roster size |
| A general-purpose API | The surface is the call sites the town actually makes |
| Serialisation, its own debug draw, a world editor | The town is the format; the renderer draws |

## What it presents

**SOL-1** `P4` **One shape and no more** — an oriented box with a corner radius, dynamic or static. The
half-extents it is held at are its *core*'s, and it reaches `extent + radius` along each of its own axes.
A radius of zero is the square-cornered box a building part is; **a core of zero is a disc**, which is
what a person and a prop are, and is not a second kind of thing. The narrow phase is written once for
that shape; the closed forms the coreless cases take are optimisations of it and are held to its own
answer by a test.
**SOL-2** `P4` A body's pose and motion are readable and writable by the town.
**SOL-3** `P4` Two ways to actuate a body and no others: an impulse at the centre of mass, and one at a point.
**SOL-4** `P4` A body can be taken out of the world and put back keeping its identity.
**SOL-5** `P4` Layers and masks under the town's rule: two bodies interact when *either* scans the other.
**SOL-6** `P4` Which two bodies **began** touching in the step just taken, and the contact normal.
**SOL-7** `P4` A nearest-hit ray cast, filtered by mask, able to exclude one named body.
**SOL-8** `P4` Whether anything static stands inside an axis-aligned box.
**SOL-9** `P4` How deep one body is into everything touching it, for the probe that measures PHY-1.
**SOL-10** `P8` Counts of what it is carrying — static, dynamic, integrated, and the last step's contacts.

## What must be true of it

**SOL-11** `P3` No gravity, and the world is a plane seen from above. The single largest simplifier.
**SOL-12** `P3` An overlap is pushed out without the push becoming motion.
**SOL-13** `P3` Coulomb friction, no bounce.
**SOL-14** `P3` An impulse off the centre spins the body it hits, unless that body's rotation is locked.
**SOL-15** `P4` The reference collision model is what the picture is matched against.
**SOL-17** `P4` Nothing is swept: a fast body can pass through a thin one.
**SOL-19** `P4` Whether a ray reports the shape its origin lies inside is **stated and tested**.
**SOL-35** `P3` The same town, seed and tick count produce the same digest, on one machine.
**SOL-36** `P8` Whether the digest holds across machines and architectures is a claim to be tested, never
assumed.

## What it costs

**SOL-20** `P2` The steady state allocates **nothing** — on a standing town, including across contact churn as
bodies touch and separate. A world whose bodies never meet allocates nothing in almost any solver; what
finds a growing array is a contact set that turns over.
**SOL-21** `P2` No structure over static geometry is rebuilt after load.
**SOL-22** `P2` Per-tick work is linear in the moving roster and never in the static population.

## How it is written

**Nothing, and that is deliberate.** How the tick is written — structure of arrays, no interface
dispatch, no LINQ, scratch from a pool or the stack, one thread — is
[CLAUDE.md](../../../../CLAUDE.md#the-two-rules-everything-else-answers-to) rule 2 and
[goals.md](../../../../docs/goals.md#what-is-pinned-and-what-is-left-open), which hold it for the whole
project and gate it. Restating it here made the solver the one slice with a second copy of the
engineering rules, and a technique named as a requirement is a technique nothing may be measured against.
What the solver owes on top of that is `SOL-20` and `SOL-22`, which are its own and are gated as its own.

# The railway — requirements

The track a train runs on, the ground it is laid as, and the level crossing where a road meets it. What runs
on it is [agents/train](../../../agents/train/docs/requirements.md).

## The track

**RAIL-2** `P6` **The railway is a network of tracks, every loose end of it past an edge of the map**
(`TRN-1`), and a track is never bent tighter than the least radius a railway is laid round (`SimConfig.Rail`). A
map holds its tracks as lines (`TownMap.Rails`), each named for the **line** it belongs to — two tracks of one
railway side by side are one line, whose trains run the both of them (`TRN-6`) — and tracks meet only end to end or
end on line: a switch stands wherever they do, and nothing about it is stored (`TRN-9`).

**RAIL-9** `P4` **A track is a lane of its own kind, one each way it is run**: routed over by a train as a road
lane is by a car, claimed as one, and laid as ground of its own (`RAIL-4`). Two ways over one track, and two tracks
crossing on the level, share their ground through a mark, as a road lane crossing a walk does.

**RAIL-3** `P0` **The railway is drawn as ballast, sleepers and two rails, between the decks and the driven
ground** (`TER-7b`): a road crossing a track is laid over it, and across the road's tarmac and its walk only the
rails are laid — nothing else is painted over the pavement.

**RAIL-4** `P4` **The railway is ground of its own, permitted to nobody but a train** (`TER-3`, `TER-3a`): the
ballast's band, each track's own width either side of its line. A building and a prop stand clear of it as of any
other ground that is not grass, and no car's or walker's route is planned along it.

**RAIL-5** `P6` **No junction stands on a railway or within a clearance of it, and a road crosses a track
square enough and once** (`SimConfig.Rail`). A generated town's streets are refused where they would not
(`GEN-10`): a node the clearance holds is not placed, and a road that would run along the ballast, cross it
twice or cross it at a slant is not laid.

**RAIL-8** `P3` **A track on a bridge is a level of its own** (`PHY-1a`): no road or walk under it meets it at a crossing,
and a wagon any of which is on it is on a collision channel of its own and meets nothing it passes over.

## The level crossing

**RAIL-1** `P0` **A level crossing closes ahead of every train.** Where a road or a walk meets a track at
grade, the crossing holds every lane and every walk across the railway at rung `p0` (`TER-5g`) — above every
other claim, a call's included — from a warning time before a train's front reaches it until its last wagon
has cleared it.

**RAIL-6** `P3` **A crossing's hold reaches only as far as the first body on its approach**, the way a light's
does (`TLT-2a`): a car or a walker already over the stop line when the crossing closes is never held on the
track, and drives or walks off it.

**RAIL-7** `P6` **A closed crossing shows it**: its lights flash from the moment it closes, and its barriers
are lowered across each road's approach over a lowering time after that, and raised once it opens.

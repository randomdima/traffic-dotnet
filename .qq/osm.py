"""
Take a city's roads off OpenStreetMap, exactly as OSM holds them and with every lane its tags mean, into the
survey a traced map is imported from — or enrich that survey with everything else known about the place, import the
map the engine reads off them, edit that map, or draw OSM's own lanes over a shot of it.

What lives where, for a map `<Map>`:
  towns/<Map>.map              the map: the one file of the place the engine reads, binary, minimal, the master
                               (every map is one, a brief's too — GEN-58)
  towns/traced/<Map>.json      the survey (OSM's roads as OSM holds them), which the map is imported off
  towns/traced/<Map>/          the enrichment's layers, which the map is imported off too, and footprints.bin — the
                               place's own buildings, which `--bench zones` weighs the town against
  towns/traced/<Map>/source/   every source's answer exactly as it came — never edited

The work is the scanner's, a tool of its own (src/tools/osmscan/, `osm-scan`) that knows nothing of the engine's
rules: it takes the rectangle round the road ways inside the city's boundary relation, and asks Overpass for every
road way in it, inside the boundary or not, every tagged node on them, every turn restriction and lane connectivity
relation, every surface outlining a road (`area:highway`) and the coastline, each node at OSM's own 1e-7°; reads
each road way's lanes as OSM's tagging rules mean them (lanes, width, placement, turn:lanes) and where a car may turn
(the restrictions and connectivity a car is under at a node, and what was not read and why); and writes them with the
flat frame they are read in to `towns/traced/<Map>.json`. The raw Overpass answers are kept in
`towns/traced/<Map>/source/` and read again unless `--refetch`; an answer from a server more than two days behind OSM
is refused. `towns/traced/<Map>.osc` is the osmChange the survey was imported with, applied over OSM's answer before
anything is read off it; it is not where the map is edited.

`--import` makes `towns/<Map>.map` off the survey and the layers: each road's line, class, level, OSM's lane counts
and its measured width, the coast, and what the place is zoned for — its land uses laid on 25 m squares inside the
frame and every patch of one kind a zone, nested as they stand, each a kind and the settings its buildings measure (frontage, front distance and spread, skew, variety, looks,
footprint, the ground behind and its bearing) — nothing else (no lane width, arrow, turn, control, crossing, tree or
footprint: the town lays its own lanes, connections, lights, zebras, props and buildings off its zones), in the map's
metres, read in milliseconds when the map opens. **The map is the master once imported**: an edit is made to it in place, and `--import` over one that
is there is refused unless `--force`, which replaces it and every edit made since. `--crop S,W,N,E` is such an edit: the
map cut down to a box of degrees (its frame the box and the margin, its roads running off at the frame's edge), and
`--drop-stumps` another: every road stump — a dead end back to where three ways meet — whose end stands inside a
building of the layers, that runs 5 m or more through one, or every road of which is a single lane is dropped, again
while dropping one leaves another (`--dry` says what it would drop). `--drop-zone-ways` drops every road the layers read
as a place's own ground (`zone_way`: a car park's aisle, a service way in a yard, garages, a market) — a car park is a zone
of its own the engine lays (`--parking`); run `--drop-stumps` after it. `--parking` lays every car park the map's zones
outline as a mall's is: aisles side by side a rank, a rank and a walk apart, joined at both ends by an aisle across them
and to the streets round them, written into the map as roads of its own for the town to rank bays down both kerbs of
(GEN-53a); laying them again replaces them, so run it after `--mend` and again after `--zones`. What any of these cuts away comes back only by
importing again. `--mend` mends the roads themselves: two ways crossing on the ground with no node, or one ending on another's
carriageway, are joined; every street is held even along its length (its lanes, its roadsides, none on another road's
carriageway) and two carriageways drawn over each other are parted; and each street is written as a road laid exactly as it
says, run on through the junctions it carries straight on through and thinned of points that add nothing to its line. A
town laid off a mended map reads its lanes as they are; mending it again changes nothing. `--zones` is a third: every zone laid and measured again off the layers, inside the map's frame, so the crop, the dropped stumps and who the town stands stay — and the place's buildings written beside the
layers for `--bench zones`, every car park on the ground a zone of its own as OSM outlines it. `--people N` and `--cars N` are a fourth: who the town stands when it is opened, one a door
and one a lane, as many as fit (a count left out keeps the map's own; an import stands nobody). `--upgrade` rewrites a
map of the previous version (`towns/traced/<Map>.map`, version 6) as this one at `towns/<Map>.map`, every edit kept and
its zones laid off the layers. `--rails` is a fifth: the map's railway laid off the tracks layer's main lines (RAIL-2) —
every route a train could run through the map from where it comes on to where it goes off, run on past both ends by the
engine's own run-off (`RailFigures.RunOffM`), routes sharing a track one line — replacing whatever railway it held.

`--meta` enriches the survey instead, into `towns/traced/<Map>/`, one JSON file a layer and a `manifest.json` and
`coverage.md` beside them, every OSM family asked as OSM stood at the survey's own moment (`[date:…]`): buildings
(OSM's, and Microsoft's machine-traced footprints OSM lacks, moved onto OSM's frame), zones (car parks, fuel, garages,
industry, port, schools, squares, land use) and which roads are a zone's ways rather than streets, every way for feet
with the road and side a pavement runs beside, crossings and their kind, junctions with their control and every
movement through them, levels and what passes over what, the ground's height (GEDTM30's terrain, the Copernicus surface
model beside it), public transport (OSM's routes and stops with the city's GTFS timetable laid onto them), places,
street furniture, barriers, unbuilt roads; each road's width as read off imagery and the roads OSM lacks (Microsoft's
road detections), each road's OSM version and last edit, what cameras saw — signs, lights, markings: Mapillary's own
when `MAPILLARY_TOKEN` holds a client token, and those Osmose relays — and a quality layer (every disagreement between
sources, Osmose issues, open OSM notes). Where sources describe one thing, one record holds the answer and names its
source: a road's `widthM` (tag, mapped surface, imagery, else lanes), a junction's control (OSM's, a sighting's where
OSM maps nothing, else the rules), a crossing seen where OSM maps none. The engine reads none of the layers, and
`--meta` never touches the map: `--import --force` brings what they say into it. `qq meta` asks the layers questions
— by id, place, name or filter — off an index of its own.
`--skip` names sources to leave out (buildings, walk, zones, districts, transit, control, barriers, furniture,
places, unbuilt, edits, notes, ml-buildings, ml-roads, dem, terrain, osmose, mapillary, gtfs); `--fetch-only` fetches
and keeps them and lays nothing.

`--meta-check` reads those layers back off their files and asks whether they are one source to reference: every id
one layer gives for another resolves and every fact two layers carry is the same in each, every OSM layer stands at
the survey's own moment, and — as readings — how far the sources describing the same things agree. It writes
`consistency.md` beside the layers and fails when a check is broken; `--meta` runs it last.

`--meta-draw OUT.png --at LAT,LON [--span M]` draws those layers back off their files over a square of the map,
with no engine: zones tinted, buildings grey (machine-traced red), roads by role, pavements green, crossings and
junctions coloured by kind and control (the legend is `Picture`'s doc comment).

`--draw SHOT.png` draws OSM's lanes over an engine shot of the map (its `.json` beside it says the framing),
into `SHOT-osm.png`: red driven along the way, blue against it, magenta both ways, carriageway edges yellow,
mapped road surfaces cyan. `--bench fidelity` is the engine's own reading of how far its lanes stand off these.
"""

import argparse

import _sim
from common import cli

#: No limit: every Overpass query, the largest a whole city's ways, each retried while the server is busy;
#: `--meta` asks some sixty tiles of a busy server that turns most first asks away, and streams whole regions
#: of other sources, which outruns qq's one-hour ceiling — a budget past it is read as none and cut at 10 s.
TIMEOUT_S = 0

SCANNER = "src/tools/osmscan/osm-scan.csproj"


#: Where the railway's figures are stated, and the file that may override them.
RAIL_FIGURES, OVERRIDES = "src/core/config/WorldFigures.cs", "assets/shared/config/SimConfig.json"


def run_off_m(base):
    """`RailFigures.RunOffM` as the engine reads it: off the file that states it, then the override file's `rail` group.
    Parsed rather than copied, which would be a second copy of a figure."""
    import json
    import re

    stated = re.search(r"public float RunOffM \{ get; init; \} = ([\d.]+)f;", (base / RAIL_FIGURES).read_text(encoding="utf-8"))
    if not stated:
        cli.fail(f"{RAIL_FIGURES} no longer states RunOffM — --rails reads it from there")
    override = base / OVERRIDES
    rail = json.loads(override.read_text(encoding="utf-8")).get("rail", {}) if override.is_file() else {}
    return float(rail.get("runOffM", stated.group(1)))


def main():
    a = parse_args()
    base = _sim.root()

    argv = ["run", "--project", SCANNER, "-c", "Release", "--"]
    if a.mend:
        # The engine mends, since what a street is held to is the engine's own laying of it.
        argv = ["run", "--project", _sim.MARKER, "-c", "Release", "--", "--mend", a.map]
    elif a.parking:
        # The engine lays a car park's aisles, since how far apart they run is its bays' and its streets' arithmetic.
        argv = ["run", "--project", _sim.MARKER, "-c", "Release", "--", "--park", a.map]
    elif a.meta_draw:
        if not a.at:
            cli.fail("--meta-draw needs --at LAT,LON")
        argv += ["meta-draw", a.meta_draw, "--at", a.at, "--span", str(a.span), "--map", a.map]
    elif a.meta_check:
        argv += ["meta-check", "--map", a.map]
    elif a.do_import:
        argv += ["import", "--map", a.map] + (["--force"] if a.force else [])
    elif a.crop:
        argv += ["crop", "--box", a.crop, "--map", a.map]
    elif a.drop_stumps:
        argv += ["stumps", "--map", a.map] + (["--dry"] if a.dry else [])
    elif a.drop_zone_ways:
        argv += ["zone-ways", "--map", a.map] + (["--dry"] if a.dry else [])
    elif a.zones:
        argv += ["zones", "--map", a.map]
    elif a.upgrade:
        argv += ["upgrade", "--map", a.map]
    elif a.rails:
        argv += ["rails", "--map", a.map, "--run-off", str(run_off_m(base))]
    elif a.people is not None or a.cars is not None:
        argv += (["population", "--map", a.map] + (["--people", str(a.people)] if a.people is not None else [])
                 + (["--cars", str(a.cars)] if a.cars is not None else []))
    elif a.draw:
        argv += ["draw", a.draw, "--map", a.map]
    elif a.meta:
        argv += (["meta", "--city", a.city] + (["--refetch"] if a.refetch else []) + (["--skip", a.skip] if a.skip else [])
                 + (["--fetch-only"] if a.fetch_only else []))
    else:
        argv += ["scan", "--city", a.city] + (["--refetch"] if a.refetch else [])

    ran = _sim.dotnet(base, *argv)
    text = (ran.stdout + ran.stderr).strip()
    shown = "\n".join(line for line in text.splitlines() if "asking again" not in line)
    if ran.code != 0:
        print(shown)
        cli.fail(text.splitlines()[-1] if text else "osm-scan failed with no output")

    print(shown)
    return 0


def parse_args():
    parser = argparse.ArgumentParser(
        prog="qq osm",
        description="Take a city's roads and lanes off OpenStreetMap into a traced map's survey, enrich that survey "
                    "with every other layer known of the place (--meta), import the map the engine reads (--import), "
                    "edit it (--crop, --drop-stumps, --drop-zone-ways, --mend, --parking, --zones, --people/--cars, --upgrade), "
                    "or draw OSM's lanes over a shot of it.",
        allow_abbrev=False,
        formatter_class=argparse.ArgumentDefaultsHelpFormatter,
    )
    parser.add_argument("--city", default="odesa",
                        help="which city to take; each names its OSM boundary relation and the map it writes")
    parser.add_argument("--refetch", action="store_true",
                        help="ask every source again rather than reading the answers kept in towns/traced/<Map>/source/")
    parser.add_argument("--meta", action="store_true",
                        help="enrich the survey into towns/traced/<Map>/ instead of scanning its roads")
    parser.add_argument("--skip", metavar="SOURCE,...",
                        help="with --meta, sources to leave out")
    parser.add_argument("--fetch-only", action="store_true",
                        help="with --meta, fetch and keep every source in towns/traced/<Map>/source/ and lay nothing")
    parser.add_argument("--meta-check", action="store_true",
                        help="check the --meta layers hold together, stand at one OSM moment, and how far their sources agree")
    parser.add_argument("--import", dest="do_import", action="store_true",
                        help="import towns/traced/<Map>.map off the survey and the --meta layers")
    parser.add_argument("--force", action="store_true",
                        help="with --import, replace a map that is there and every edit made to it")
    parser.add_argument("--crop", metavar="S,W,N,E",
                        help="cut the map down to this box of degrees, in place")
    parser.add_argument("--drop-stumps", action="store_true",
                        help="drop the road stumps that run into a building or are a single lane, in place")
    parser.add_argument("--drop-zone-ways", action="store_true",
                        help="drop every road the layers read as a place's own ground (a car park's aisle, a yard's way), in place")
    parser.add_argument("--dry", action="store_true",
                        help="with --drop-stumps or --drop-zone-ways, say what would be dropped and drop nothing")
    parser.add_argument("--mend", action="store_true",
                        help="mend the map's roads in place: join what OSM left unjoined, hold every street even, part carriageways drawn over each other")
    parser.add_argument("--parking", action="store_true",
                        help="lay every car park the zones outline as a mall's, its aisles joined to the streets, in place")
    parser.add_argument("--zones", action="store_true",
                        help="lay and measure the map's zones again off the layers, in place, and write the place's buildings for --bench zones")
    parser.add_argument("--upgrade", action="store_true",
                        help="rewrite a map of the previous version as this one, in place, every edit kept")
    parser.add_argument("--rails", action="store_true",
                        help="lay the map's railway off the tracks layer's main lines, in place, replacing what it held")
    parser.add_argument("--people", type=int, metavar="N",
                        help="set how many people the town stands at its doors, in place")
    parser.add_argument("--cars", type=int, metavar="N",
                        help="set how many cars the town stands on its lanes, in place")
    parser.add_argument("--draw", metavar="SHOT.png",
                        help="draw OSM's lanes over this shot of the map instead of scanning")
    parser.add_argument("--meta-draw", metavar="OUT.png",
                        help="draw the --meta layers back off their files over a square of the map")
    parser.add_argument("--at", metavar="LAT,LON", help="with --meta-draw, the square's middle")
    parser.add_argument("--span", type=float, default=400.0, help="with --meta-draw, the square's side in metres")
    parser.add_argument("--map", default="OdesaOsm",
                        help="the traced map an --import, an edit, --draw, --meta-check or --meta-draw is of")
    return parser.parse_args()


if __name__ == "__main__":
    cli.entry(main)

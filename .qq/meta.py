"""
Query a traced map's enriched layers (`qq osm --meta`) fast: everything known about a thing by its id and everything
that names it, what stands round a place, what is called so, or one layer filtered, sorted, grouped and counted.

The layers are some twenty JSON files and over 100 MB, seconds to read for every question. The index is one SQLite
file, `.tmp/meta/<Map>.sqlite`, built on first use and again whenever `qq osm --meta` writes the layers anew
(manifest.json's `written`): every item once by layer and id, its place as a box in an R-tree of OSM's own 1e-7°
integers (a road's off the survey's nodes), its names folded for search, and every id it names of another item — so
"what names w123" is one lookup and not a scan.

An id is as the layers write it: `w123` a road, a way for feet, a track or an OSM building or zone; `n123` a junction,
a crossing or a stop; `r123` a route or a relation's zone; `mlr5` a stretch of road traced off imagery and `mlw5` a
road OSM lacks made of them; `ml12` a machine-traced building; `mly99` or `osmose:<uuid>` a camera's sighting; `q7`
a quality flag; `gtfs:…` the timetable's own. A bare number is tried as `w`, `n` and `r`.

    qq meta w26542056                 the road, and every junction, crossing, stop, sighting and flag naming it
    qq meta --at 46.4846,30.7326 --radius 60   also says where the place stands in the map's metres
    qq meta --xy 3028,13339 --layer roads      the same asked in the map's metres (its .map's frame), as a shot's --at
    qq meta --name Пастера --layer roads
    qq meta --layer roads --where widthFrom=imagery --where "widthM>14" --fields name,widthM,lanes --sort widthM --desc
    qq meta --layer junctions --group controlFrom
    qq meta --layer quality --where kind~lanes --count

A filter is FIELD OP VALUE, OP one of = != > < >= <= ~ (contains); a field reaches into a record by dots and
brackets (`widthDetected.across`, `lanes[0]`). `--json` prints records whole; `--out FILE.md` writes what would be
printed instead.
"""

from __future__ import annotations

import argparse
import json
import math
import re
import sqlite3
import struct
from pathlib import Path

import _sim
from common import cli

#: The first call builds the index off every layer, a minute at most; a call after it answers in well under one.
TIMEOUT_S = 300

#: Bumped whenever what the index holds changes, so an index of the old shape is built again.
INDEX_VERSION = "1"

#: Keys whose integer values name a way, those whose name a node, and those whose strings are ids as they stand.
WAY_KEYS = {"road", "way", "roads", "waysIn", "passages", "tram", "upper", "lower", "inStreet", "meets", "ways"}
NODE_KEYS = {"junction", "cluster", "heldBy", "node"}
ID_KEYS = {"seen", "network", "zone", "under", "outline", "stretches", "elements", "stops"}

#: What a one-line listing shows of an item, by layer, after its id, kind and name.
BRIEF = {
    "roads": ["role", "widthM", "widthFrom", "lanes", "maxspeed", "editedOn"],
    "junctions": ["control", "controlFrom", "rule"],
    "crossings": ["source", "painted", "road", "junctionM"],
    "walk": ["road", "side", "lengthM"],
    "zones": ["sub", "areaM2"],
    "buildings": ["source", "use", "heightM", "levels"],
    "levels": ["layer", "structure", "lengthM"],
    "overpasses": ["upper", "upperKind", "lower", "lowerKind", "explained"],
    "routes": ["mode", "ref", "source"],
    "stops": ["source", "road", "routes"],
    "tracks": ["inStreet", "lengthM"],
    "detected": ["widthM", "lengthM", "roads", "network"],
    "unmapped": ["widthM", "lengthM", "meets"],
    "seen": ["source", "osm", "agrees", "heldBy", "lastSeen"],
    "quality": ["about", "elements"],
}

DEGREE_M = 111_320.0


def main():
    a = parse_args()
    base = _sim.root()
    folder = base / "towns" / "traced" / a.map
    if not (folder / "manifest.json").is_file():
        cli.fail(f"no enriched layers at {folder.relative_to(base)}: `qq osm --meta` writes them")

    manifest = json.loads((folder / "manifest.json").read_text(encoding="utf-8"))
    db = index(base, folder, manifest, a.map, a.rebuild)
    lines: list[str] = []
    if a.ids:
        about(db, a.ids, a.limit, a.json, lines)
    elif a.at or a.xy:
        frame = map_frame(base / "towns" / f"{a.map}.map")
        at = a.at or ",".join(f"{degrees:.7f}" for degrees in map_latlon(frame, *metres(a.xy)))
        around(db, at, a.radius, a.layer, a.limit, a.json, lines, frame)
    elif a.name:
        named(db, a.name, a.layer, a.limit, a.json, lines)
    elif a.layer:
        listing(db, a, lines)
    else:
        overview(db, lines)

    text = "\n".join(lines)
    if a.out:
        out = Path(a.out)
        out.parent.mkdir(parents=True, exist_ok=True)
        out.write_text(text + "\n", encoding="utf-8")
        print(f"wrote {out} ({len(lines)} lines)")
    else:
        print(text)
    return 0


def index(base: Path, folder: Path, manifest: dict, name: str, rebuild: bool) -> sqlite3.Connection:
    """The map's index, built afresh where it is missing, of another shape, or older than the layers."""
    stamp = f"{INDEX_VERSION}/{manifest['written']}"
    path = base / ".tmp" / "meta" / f"{name}.sqlite"
    path.parent.mkdir(parents=True, exist_ok=True)
    if path.exists() and not rebuild:
        db = sqlite3.connect(path)
        try:
            if db.execute("select value from meta where key = 'stamp'").fetchone() == (stamp,):
                return db
        except sqlite3.Error:
            pass
        db.close()

    partial = path.with_suffix(".part")
    partial.unlink(missing_ok=True)
    db = sqlite3.connect(partial)
    build(db, base, folder, manifest, stamp)
    db.close()
    partial.replace(path)
    return sqlite3.connect(path)


def build(db: sqlite3.Connection, base: Path, folder: Path, manifest: dict, stamp: str) -> None:
    db.executescript(
        """
        create table meta(key text primary key, value text);
        create table items(rid integer primary key, layer text, id text, kind text, name text, folded text,
                           lat integer, lon integer, json text);
        create virtual table boxes using rtree_i32(rid, minlat, maxlat, minlon, maxlon);
        create table refs(rid integer, ref text);
        """
    )
    survey = json.loads((base / manifest["survey"]).read_text(encoding="utf-8"))
    nodes = survey["nodes"]
    lines = {way["id"]: [(nodes["lat"][at], nodes["lon"][at]) for at in way["nodes"]] for way in survey["ways"]}

    rid = 0
    for layer in manifest["layers"]:
        name = layer["layer"]
        file = json.loads((folder / layer["file"]).read_text(encoding="utf-8"))
        items, boxes, refs = [], [], []
        for at, record in enumerate(file["items"]):
            rid += 1
            ident = identify(name, record, at)
            places = geometry(name, record, lines)
            middle = places[len(places) // 2] if places else (None, None)
            names = [record.get("name"), record.get("nameEn")] + [value for key, value in (record.get("tags") or {}).items()
                                                                  if key == "name" or key.startswith(("name:", "old_name"))]
            folded = " | ".join(dict.fromkeys(n for n in names if n)).casefold() or None
            items.append((rid, name, ident, kind(record), record.get("name") or (record.get("tags") or {}).get("name"),
                          folded, middle[0], middle[1], json.dumps(record, ensure_ascii=False, separators=(",", ":"))))
            if places:
                lats, lons = [p[0] for p in places], [p[1] for p in places]
                boxes.append((rid, min(lats), max(lats), min(lons), max(lons)))
            refs.extend((rid, ref) for ref in references(record) if ref != ident)
        db.executemany("insert into items values (?,?,?,?,?,?,?,?,?)", items)
        db.executemany("insert into boxes values (?,?,?,?,?)", boxes)
        db.executemany("insert into refs values (?,?)", refs)

    db.executescript(
        """
        create index items_id on items(id);
        create index items_layer on items(layer);
        create index refs_ref on refs(ref);
        """
    )
    db.execute("insert into meta values ('stamp', ?)", (stamp,))
    db.commit()


def identify(layer: str, record: dict, at: int) -> str:
    """An item's id as the layers name it to each other."""
    if layer in ("roads", "walk", "tracks"):
        return f"w{record['way']}"
    if layer == "junctions":
        return f"n{record['node']}"
    if layer == "crossings":
        if record.get("node"):
            return f"n{record['node']}"
        return record.get("seen") or f"x{at}"
    if layer == "levels":
        return f"{record['class']}:w{record['way']}"
    if layer == "overpasses":
        return f"x{at}"
    if layer == "quality":
        return f"q{at}"
    return str(record["id"])


def kind(record: dict) -> str | None:
    for key in ("kind", "highway", "control", "status", "mode", "use", "class"):
        if isinstance(record.get(key), str):
            return record[key]
    return None


def geometry(layer: str, record: dict, lines: dict) -> list[tuple[int, int]]:
    """Every place an item stands at, lat, lon in 1e-7°: its point, its line, its rings, or its survey road's nodes."""
    if layer == "roads":
        return lines.get(record["way"], [])
    if isinstance(record.get("at"), list) and len(record["at"]) == 2:
        return [tuple(record["at"])]
    flat: list[int] = []
    if isinstance(record.get("line"), list):
        flat = record["line"]
    elif isinstance(record.get("outer"), list):
        flat = [value for ring in record["outer"] for value in ring]
    elif isinstance(record.get("lines"), list):
        flat = [value for line in record["lines"] for value in line]
    return list(zip(flat[0::2], flat[1::2]))


def references(record) -> set[str]:
    """Every id an item names of another, by the keys that carry them, at any depth."""
    found: set[str] = set()

    def walk(value, key=None):
        if isinstance(value, dict):
            for inner, item in value.items():
                if inner not in ("line", "lines", "outer", "inner", "at", "nodes", "geometry", "heightM", "tags"):
                    walk(item, inner)
        elif isinstance(value, list):
            for item in value:
                walk(item, key)
        elif isinstance(value, int) and not isinstance(value, bool):
            if key in WAY_KEYS:
                found.add(f"w{value}")
            elif key in NODE_KEYS:
                found.add(f"n{value}")
        elif isinstance(value, str) and key in ID_KEYS:
            found.add(value)

    walk(record)
    return found


def about(db: sqlite3.Connection, ids: list[str], limit: int, whole: bool, lines: list[str]) -> None:
    """Every item each id is, in full, then every item naming it, a line each."""
    for asked in ids:
        tried = [asked] if not asked.isdigit() else [f"w{asked}", f"n{asked}", f"r{asked}"]
        rows = [row for ident in tried for row in db.execute("select rid, layer, id, json from items where id = ?", (ident,))]
        if not rows:
            lines.append(f"{asked}: nothing by that id")
            continue
        for _, layer, ident, text in rows:
            lines.append(f"== {layer} {ident}")
            record = json.loads(text)
            lines.extend([json.dumps(record, ensure_ascii=False)] if whole else shown(record))
        named_by = {ident for _, _, ident, _ in rows}
        refs = db.execute(
            f"select i.layer, i.id, i.kind, i.name, i.json from refs r join items i on i.rid = r.rid "
            f"where r.ref in ({','.join('?' * len(named_by))}) order by i.layer, i.id", tuple(named_by)).fetchall()
        lines.append(f"-- named by {len(refs)}")
        grouped(refs, limit, whole, lines)
        lines.append("")


def around(db: sqlite3.Connection, at: str, radius: float, layer: str | None, limit: int, whole: bool, lines: list[str],
           frame: dict) -> None:
    """Every item within a radius of a place, nearest first, by layer, and where the place stands on the map."""
    try:
        lat, lon = (float(part) for part in at.split(","))
    except ValueError:
        cli.fail(f"--at takes LAT,LON in degrees: got {at!r}")
    dlat = radius / DEGREE_M * 1e7
    dlon = radius / (DEGREE_M * math.cos(math.radians(lat))) * 1e7
    centre = (lat * 1e7, lon * 1e7)
    rows = db.execute(
        "select i.layer, i.id, i.kind, i.name, i.json, b.minlat, b.maxlat, b.minlon, b.maxlon from boxes b join items i on i.rid = b.rid "
        "where b.maxlat >= ? and b.minlat <= ? and b.maxlon >= ? and b.minlon <= ?" + (" and i.layer = ?" if layer else ""),
        (centre[0] - dlat, centre[0] + dlat, centre[1] - dlon, centre[1] + dlon) + ((layer,) if layer else ())).fetchall()

    def off(row) -> float:
        nearest_lat = min(max(centre[0], row[5]), row[6])
        nearest_lon = min(max(centre[1], row[7]), row[8])
        return math.hypot((nearest_lat - centre[0]) / dlat * radius, (nearest_lon - centre[1]) / dlon * radius)

    near = sorted(((off(row), row[:5]) for row in rows), key=lambda pair: (pair[1][0], pair[0]))
    near = [(metres, row) for metres, row in near if metres <= radius]
    x, y = map_xy(frame, lat, lon)
    lines.append(f"{len(near)} items within {radius:g} m of {lat},{lon} — on the map at {x:.0f} {y:.0f} (--at for a shot)")
    grouped([row for _, row in near], limit, whole, lines, [f"{metres:.0f} m" for metres, _ in near])


def map_frame(path: Path) -> dict:
    """
    The frame a map's own file is in (src/citygen/map/TownMap.Bytes.cs): a shot's places are in it, and a crop moves
    it off the frame the layers were laid in. Read off its head: "TMAP", a version, four 7-bit-length strings, a varint
    seed, then seven little-endian doubles.
    """
    head = path.read_bytes()[:4096]
    if head[:4] != b"TMAP":
        cli.fail(f"{path.name} is not a traced map")
    at = 6

    def varint() -> int:
        nonlocal at
        value, shift = 0, 0
        while True:
            part = head[at]
            at += 1
            value |= (part & 0x7F) << shift
            if part < 0x80:
                return value
            shift += 7

    for _ in range(4):
        length = varint()
        at += length
    varint()
    names = ("lat0Deg", "lon0Deg", "westM", "southM", "widthM", "heightM", "marginM")
    return dict(zip(names, struct.unpack_from("<7d", head, at)))


def map_xy(frame: dict, lat: float, lon: float) -> tuple[float, float]:
    """
    Where a place stands on a traced map, metres east and south of its north-west corner: the survey's own Transverse
    Mercator (src/citygen/traced/TransverseMercator.cs, Snyder 8-9 and 8-10) about its frame's origin.
    """
    a, f = 6378137.0, 1 / 298.257223563
    e2 = f * (2 - f)
    ep2 = e2 / (1 - e2)

    def meridian(phi: float) -> float:
        e4, e6 = e2 * e2, e2 * e2 * e2
        return a * ((1 - e2 / 4 - 3 * e4 / 64 - 5 * e6 / 256) * phi - (3 * e2 / 8 + 3 * e4 / 32 + 45 * e6 / 1024) * math.sin(2 * phi)
                    + (15 * e4 / 256 + 45 * e6 / 1024) * math.sin(4 * phi) - 35 * e6 / 3072 * math.sin(6 * phi))

    phi = math.radians(lat)
    sin, cos = math.sin(phi), math.cos(phi)
    tan = sin / cos
    nu = a / math.sqrt(1 - e2 * sin * sin)
    t, c = tan * tan, ep2 * cos * cos
    big_a = (math.radians(lon) - math.radians(frame["lon0Deg"])) * cos
    a2 = big_a * big_a
    east = nu * (big_a + (1 - t + c) * a2 * big_a / 6 + (5 - 18 * t + t * t + 72 * c - 58 * ep2) * a2 * a2 * big_a / 120)
    north = meridian(phi) - meridian(math.radians(frame["lat0Deg"])) + nu * tan * (
        a2 / 2 + (5 - t + 9 * c + 4 * c * c) * a2 * a2 / 24 + (61 - 58 * t + t * t + 600 * c - 330 * ep2) * a2 * a2 * a2 / 720)
    return east - frame["westM"], frame["heightM"] - (north - frame["southM"])


def map_latlon(frame: dict, x: float, y: float) -> tuple[float, float]:
    """A place on the map back in degrees, by Newton's method on `map_xy` itself, to well under a millimetre."""
    lat, lon = frame["lat0Deg"], frame["lon0Deg"]
    step = 1e-6
    for _ in range(8):
        px, py = map_xy(frame, lat, lon)
        mx, my = x - px, y - py
        if math.hypot(mx, my) < 1e-4:
            break
        (ax, ay), (bx, by) = map_xy(frame, lat + step, lon), map_xy(frame, lat, lon + step)
        ax, ay, bx, by = ax - px, ay - py, bx - px, by - py
        determinant = ax * by - ay * bx
        lat += (mx * by - my * bx) / determinant * step
        lon += (ax * my - ay * mx) / determinant * step
    return lat, lon


def metres(xy: str) -> tuple[float, float]:
    try:
        x, y = (float(part) for part in xy.replace(",", " ").split())
    except ValueError:
        cli.fail(f"--xy takes X,Y in the map's metres: got {xy!r}")
    return x, y


def named(db: sqlite3.Connection, text: str, layer: str | None, limit: int, whole: bool, lines: list[str]) -> None:
    """Every item whose name, English name or old name holds the text, any case."""
    rows = db.execute(
        "select layer, id, kind, name, json from items where instr(folded, ?) > 0" + (" and layer = ?" if layer else "") + " order by layer, name",
        (text.casefold(),) + ((layer,) if layer else ())).fetchall()
    lines.append(f"{len(rows)} items named like {text!r}")
    grouped(rows, limit, whole, lines)


FILTER = re.compile(r"^([\w.\[\]:]+?)\s*(>=|<=|!=|=|>|<|~)\s*(.*)$")


def listing(db: sqlite3.Connection, a, lines: list[str]) -> None:
    """One layer's items filtered, and listed, sorted, grouped or counted."""
    clauses, values = ["layer = ?"], [a.layer]
    for said in a.where:
        match = FILTER.match(said)
        if not match:
            cli.fail(f"--where takes FIELD OP VALUE, OP one of = != > < >= <= ~: got {said!r}")
        field, op, value = match.groups()
        path = path_of(field)
        if op == "~":
            clauses.append("instr(lower(cast(json_extract(json, ?) as text)), ?) > 0")
            values += [path, value.lower()]
        elif value in ("null", "none"):
            clauses.append(f"json_extract(json, ?) is {'not ' if op == '!=' else ''}null")
            values.append(path)
        else:
            clauses.append(f"json_extract(json, ?) {op} ?")
            values += [path, number(value)]
    where = " and ".join(clauses)

    if a.count:
        (total,) = db.execute(f"select count(*) from items where {where}", values).fetchone()
        lines.append(f"{total} {a.layer}")
        return
    if a.group:
        rows = db.execute(f"select json_extract(json, ?) g, count(*) n from items where {where} group by g order by n desc",
                          [path_of(a.group)] + values).fetchall()
        lines.append(f"{a.layer} by {a.group}")
        lines.extend(f"  {n:>8}  {g}" for g, n in rows[: a.limit or None])
        if a.limit and len(rows) > a.limit:
            lines.append(f"  … {len(rows) - a.limit} more (--limit=0 for all)")
        return

    order, ordered = "", []
    if a.sort:
        order = f" order by json_extract(json, ?) {'desc' if a.desc else 'asc'}"
        ordered = [path_of(a.sort)]
    (total,) = db.execute(f"select count(*) from items where {where}", values).fetchone()
    rows = db.execute(f"select layer, id, kind, name, json from items where {where}{order}"
                      + (f" limit {a.limit}" if a.limit else ""), values + ordered).fetchall()
    lines.append(f"{total} {a.layer}" + (f", the first {len(rows)}" if len(rows) < total else ""))
    fields = [field.strip() for field in a.fields.split(",")] if a.fields else None
    for layer, ident, kind_, name, text in rows:
        record = json.loads(text)
        if a.json:
            lines.append(json.dumps(record, ensure_ascii=False))
        elif fields:
            lines.append("  " + "  ".join([ident] + [brief_value(pick(record, field)) for field in fields]))
        else:
            lines.append(one_line(layer, ident, kind_, name, record))


def overview(db: sqlite3.Connection, lines: list[str]) -> None:
    lines.append("layers indexed (qq meta --help for the questions it answers):")
    for layer, n in db.execute("select layer, count(*) from items group by layer order by min(rid)"):
        lines.append(f"  {layer:<12} {n:>8}")


def grouped(rows, limit: int, whole: bool, lines: list[str], notes: list[str] | None = None) -> None:
    shown_in: dict[str, int] = {}
    held: dict[str, int] = {}
    for at, (layer, ident, kind_, name, text) in enumerate(rows):
        held[layer] = held.get(layer, 0) + 1
        if limit and shown_in.get(layer, 0) >= limit:
            continue
        shown_in[layer] = shown_in.get(layer, 0) + 1
        record = json.loads(text)
        line = json.dumps(record, ensure_ascii=False) if whole else one_line(layer, ident, kind_, name, record)
        lines.append(line + (f"  ({notes[at]})" if notes else ""))
    for layer, n in held.items():
        if limit and n > limit:
            lines.append(f"  … {n - limit} more in {layer} (--limit=0 for all)")


def one_line(layer: str, ident: str, kind_: str | None, name: str | None, record: dict) -> str:
    extra = "  ".join(f"{field}={brief_value(record[field])}" for field in BRIEF.get(layer, []) if record.get(field) is not None)
    return f"  {layer:<10} {ident:<24} {kind_ or '':<16} {name or '':<28} {extra}".rstrip()


def shown(record: dict) -> list[str]:
    """A record a field a line, a long array or a large object summarised."""
    out = []
    for key, value in record.items():
        out.append(f"  {key}: {brief_value(value, 200)}")
    return out


def brief_value(value, width: int = 60) -> str:
    if isinstance(value, list) and len(value) > 12 and all(isinstance(item, (int, float)) for item in value):
        return f"[{len(value)} numbers]"
    text = value if isinstance(value, str) else json.dumps(value, ensure_ascii=False)
    return text if len(text) <= width else text[: width - 1] + "…"


def path_of(field: str) -> str:
    return "$." + re.sub(r"\.(\d+)", r"[\1]", field)


def pick(record: dict, field: str):
    value = record
    for part in re.findall(r"[^.\[\]]+", field):
        if isinstance(value, list) and part.isdigit() and int(part) < len(value):
            value = value[int(part)]
        elif isinstance(value, dict):
            value = value.get(part)
        else:
            return None
    return value


def number(value: str):
    try:
        return int(value)
    except ValueError:
        try:
            return float(value)
        except ValueError:
            return {"true": 1, "false": 0}.get(value, value)


def parse_args():
    parser = argparse.ArgumentParser(
        prog="qq meta",
        description="Query a traced map's enriched layers by id, place, name or filter, off an index built once.",
        allow_abbrev=False,
        formatter_class=argparse.ArgumentDefaultsHelpFormatter,
    )
    parser.add_argument("ids", nargs="*", metavar="ID", help="ids to tell everything of: w123, n123, mlw5, mly99, q7, …")
    parser.add_argument("--at", metavar="LAT,LON", help="what stands round a place")
    parser.add_argument("--xy", metavar="X,Y", help="what stands round a place given in the map's metres, as a shot's --at")
    parser.add_argument("--radius", type=float, default=50.0, help="with --at, how far round, in metres")
    parser.add_argument("--name", metavar="TEXT", help="what is called so: its name, English name or an old name holds TEXT")
    parser.add_argument("--layer", help="one layer: alone it lists the layer, else it narrows --at and --name")
    parser.add_argument("--where", action="append", default=[], metavar="FIELD OP VALUE",
                        help="with --layer, a filter, repeatable: = != > < >= <= ~ (contains)")
    parser.add_argument("--fields", metavar="A,B", help="with --layer, the fields to print, by dots and brackets")
    parser.add_argument("--sort", metavar="FIELD", help="with --layer, the field to sort by")
    parser.add_argument("--desc", action="store_true", help="with --sort, largest first")
    parser.add_argument("--group", metavar="FIELD", help="with --layer, count by a field's values instead of listing")
    parser.add_argument("--count", action="store_true", help="with --layer, only how many match")
    parser.add_argument("--limit", type=cli.counting, default=25, metavar="INT", help="max rows, a layer each (0 = no limit)")
    parser.add_argument("--json", action="store_true", help="records whole, one JSON line each")
    parser.add_argument("--out", metavar="FILE.md", help="write what would be printed to a file instead")
    parser.add_argument("--map", default="OdesaOsm", help="the traced map whose layers are asked")
    parser.add_argument("--rebuild", action="store_true", help="build the index again though the layers have not changed")
    return parser.parse_args()


if __name__ == "__main__":
    cli.entry(main)

# OdesaOsm — consistency

Written by `qq osm --meta-check` off the layers beside it, read back as a reader reads them. Generated; do not edit.

**One source: yes.** Every reference resolves, every fact two layers carry is the same in each, and every OSM layer stands at the survey's own moment (2026-10-03T09:16:51Z).

## The layers hold together

Every id one layer gives for another is an item of that other, and every fact two layers both carry is the same in each.

| Check | Asked | Broken | e.g. |
|---|---:|---:|---|
| each layer's head and the manifest count what it holds | 40 | 0 |  |
| every item is in its layer once | 334,571 | 0 |  |
| every survey road has its record in roads, and roads holds no other way | 21,422 | 0 |  |
| a place given for a survey node is the survey's, a line has a place for each node, a road a height for each | 59,800 | 0 |  |
| every id a layer gives for another is an item of that other | 390,676 | 0 |  |
| a road's crossings by kind are the crossings layer's on it, and a crossing's road class, name, width and lanes the road's | 24,181 | 0 |  |
| a road's trams are the tracks that say they run down it | 21,422 | 0 |  |
| a road's routes are the routes that run along it | 21,422 | 0 |  |
| a road's zone holds it among its roads, and a zone's roads say they are its | 8,090 | 0 |  |
| a way under a building is among that building's passages, and the building over each passage is one it passes | 4,591 | 0 |  |
| a road's layer, structure and building over it are its record in levels, and it has one where any is set | 21,422 | 0 |  |
| a pavement drawn beside a road is counted on that road's side, and a side found by geometry has one drawn | 2,266 | 0 |  |
| a junction's arms are the survey's ways at its node, each with its road's class, name, surface and lanes | 89,317 | 0 |  |
| a crossing a junction holds is of the kind the crossings layer reads it, and one it holds as seen is laid there | 1,402 | 0 |  |
| every turn the survey forbids is forbidden at its junction, and every forbidden turn says why | 3,466 | 0 |  |
| a stop's routes are the routes that call there, by OSM's relations and the timetable alike | 2,977 | 0 |  |
| a road has a width read off imagery exactly where a detected stretch says it runs along it | 21,422 | 0 |  |
| a road's width is the one its source names, and no source before it in the order gives one | 21,422 | 0 |  |
| a sighting a junction holds or a crossing was laid from says so, and a control decided by a sighting holds one | 21,556 | 0 |  |
| an unmapped stretch is in exactly the unmapped road that lists it, and that road holds only unmapped stretches | 26,792 | 0 |  |
| every road has its OSM version and last edit, and none was edited after the survey's moment | 21,422 | 0 |  |

## One snapshot of OSM

Every OSM layer read off one moment, the survey's: no node in two places, no element with two sets of tags.

| Check | Asked | Broken | e.g. |
|---|---:|---:|---|
| every OSM family stands at the survey's moment | 11 | 0 |  |
| every node the families and the survey both hold is in one place | 14,630 | 0 |  |
| every node the families and the survey both hold has one set of tags | 7,209 | 0 |  |
| every survey road a family also holds has the same nodes and tags | 72 | 0 |  |

## Where the sources agree

Readings, not checks: where two sources describe the same thing, how far they agree, and which one the layers take. The quality layer names every disagreement by its elements.

### Buildings: OSM's outlines and Microsoft's machine-traced footprints

- 189,348 footprints read; 40,201 (21.2 %) stand on an OSM building as traced and are taken as it; 143,719 are laid as buildings of their own, moved onto OSM's frame, of which 0 stand a third or more on an OSM building or a street
- 36,986 of 61,895 OSM buildings (59.8 %) have a footprint; one that has none was built after the imagery, or the model missed it
- as traced, a footprint of like size stands off its OSM building by 2.7 m at the median, 6.7 m at the 90th percentile — 1.4 m east and -0.6 m north at the median, over 23,311 pairs
- moved by the median shift of its neighbours within 500 m, itself left out, a footprint stands 1.9 m off its OSM building at the median, 4.9 m at the 90th percentile, over 23,001 pairs — the shift a footprint OSM lacks is moved by
- Київський район: median shift as traced 1.9 m east, -1.3 m north, over 3,630 pairs
- Хаджибейський район: median shift as traced 2.7 m east, -1.3 m north, over 3,437 pairs
- Приморський район: median shift as traced 3.8 m east, -0.9 m north, over 2,165 pairs
- Пересипський район: median shift as traced 0.4 m east, -0.2 m north, over 6,317 pairs
- a footprint's area over its OSM building's: 1.11 at the median

### Timetable: the city's feed against itself

- its calendar is in force 20250402 … 20261231; 85 routes, 67 running on some day, 41 of those at weekends only, 18 on none and not laid
- read once though printed more, or not read: calls listed twice 33,161, stops listed twice 1,802, trips listed twice 439, calls at a stop the feed does not list 84, routes listed twice 2

### Stops: OSM's and the city's timetable

- 1,685 timetable stops served on some day: 1,263 (75.0 %) laid on an OSM stop serving their modes, 422 laid as stops of their own where OSM has none, 0 not laid
- a timetable stop stands 7.8 m from the OSM stop it is laid on at the median, 22.3 m at the 90th percentile; the layer keeps OSM's place
- 0 of those (0.0 %) are laid on an OSM stop that says it serves other modes only; 16 on one that says no mode at all
- 542 of 1,185 named pairs (45.7 %) share a name, one holding the other; the layer keeps OSM's name

### Routes: OSM's relations and the city's timetable

- 67 timetable routes run on some day, in 134 directions: 61 laid on OSM relations of their mode and ref, 73 as routes of their own down the roads their line runs along — each raised in the quality layer — and 0 laid other than once
- 13 OSM routes over the town carry no timetable — the timetable runs them on no day, or not in the direction OSM draws: bus 1, bus 120, bus 149, bus 168, bus 232а, bus 25, bus 259, bus 6, bus 601, bus 748, bus 78, bus 81, bus N3277

### Timetable lines: how far they run from the roads and track OSM draws

- bus: 78,660 places 20 m apart, 0.7 m off a road at the median, 2.9 m at the 95th percentile, 0.1 % more than 15 m
- tram: 19,497 places 20 m apart, 1.4 m off the track at the median, 6.0 m at the 95th percentile, 0.1 % more than 15 m
- trolleybus: 8,125 places 20 m apart, 0.8 m off a road at the median, 6.3 m at the 95th percentile, 0.1 % more than 15 m
- e.g. tram 15 18 m off at 464872791,306945012; trolleybus 12 16 m off at 464159217,307138459; trolleybus 12 17 m off at 463978686,307156924; trolleybus 12 16 m off at 463978828,307156826; tram 3 15 m off at 463866850,307318029

### Height models: the ground at the shore

- terrain: 2,230 coastline nodes on the map read 1.1 m at the median (0.1 m to 1.9 m, 10th to 90th percentile); 0.1 % read over 5 m, a cliff or a quay's edge a post away; one over the sea reads none
- surface: 2,230 coastline nodes on the map read 0.4 m at the median (0.0 m to 1.7 m, 10th to 90th percentile); 0.6 % read over 5 m, a cliff or a quay's edge a post away; one over the sea reads none
- e.g. n2504125908 5.2 m; n10648863788 5.0 m

### Height models: the terrain (ground) against the surface (roofs, trees and decks)

- at 95,148 road nodes the surface stands 1.0 m over the ground at the median, 2.2 m at the 90th percentile; 4.3 % over 3 m — the roofs and trees a road's height no longer reads; 0.0 % stand more than 5 m under it
- roads' heights are read off the terrain model; 678 roads climb over 8 % somewhere
- e.g. n6707568581 surface -5.3 m, ground -0.1 m; n3449510225 surface 33.1 m, ground 38.5 m; n9833875602 surface -5.3 m, ground -0.0 m; n13073779507 surface -5.6 m, ground -0.2 m; n13074757541 surface -5.3 m, ground -0.0 m

### Osmose's issues against the OSM the layers were read off

- 4,980 issues, raised 2026-10-03T01:50:49Z … 2026-10-03T01:50:49Z; the layers' OSM stands at 2026-10-03T09:16:51Z
- 2,143 of the elements they give tags for are in the layers; 0 of those (0.0 %) have been edited between, which may have answered the issue
- 567 elements are in no layer: deleted since, or of a kind no layer reads

### Crossings: the node on the road and the way drawn across it

- 152 crossings say their kind on both node and way; 2 (1.3 %) disagree — a zebra being a painted crossing named, and a signalled one painted where its way says so: unmarked/marked 2
- the layer takes the node's kind, which is where the road is crossed, and the way's only where the node says none; it is painted where the node says so, else where the way does
- e.g. n6573900890 unmarked / w700036338 marked; n6629717952 unmarked / w993211630 marked

### Pavements: a road's sidewalk tags and the pavements drawn beside it

- 696 road sides tagged; 34 (4.9 %) disagree with what is drawn: tagged drawn apart, none found beside 23, tagged on the road, and drawn beside it too 10, tagged none, one drawn beside 1
- the layer takes a pavement drawn along half the road or more as the side's, whatever the tag; a side tagged none or drawn apart is taken as tagged
- e.g. w31509779 right: tagged drawn apart, none found beside; w31509836 right: tagged none, one drawn beside; w56124229 right: tagged on the road, and drawn beside it too; w82939991 left: tagged drawn apart, none found beside; w103616486 right: tagged on the road, and drawn beside it too

### Widths: a road's width tag, its lanes and its measured surface

- 3 roads both tag a width and run in a mapped surface; 3 (100.0 %) differ by more than 30 %
- over 426 measured roads, the carriageway its lanes make is 0.74 of the surface at the median; 46.0 % are under two thirds of it or over half as wide again — parking, a median or a lane count mistyped
- the layer keeps all three: the width its lanes make, the tag, and the surface measured
- e.g. w142863716 tagged 2.5 m, measured 18.1 m; w142868324 tagged 5 m, measured 24 m; w179958968 tagged 8 m, measured 20.4 m

### Widths: a road's lanes and mapped surface against the width read off imagery

- 212 roads run in a mapped surface and along a detected stretch over half their length: the detected width is 1.09 of the surface at the median, 3.2 m off it; 46.2 % differ by more than 30 %
- 79.1 % of street length has a detected width over half its length (one carriageway under it); 1,964 roads are the ways of a dual carriageway read across both
- residential: 3,814 streets read +2.8 m wider than their lanes make them at the median (+0.4 to +4.5 m, 10th to 90th percentile) — kerbside parking and gutters, or lanes not tagged
- service: 1,538 streets read +4.7 m wider than their lanes make them at the median (+2.3 to +6.4 m, 10th to 90th percentile) — kerbside parking and gutters, or lanes not tagged
- tertiary: 449 streets read +5.3 m wider than their lanes make them at the median (+0.4 to +7.8 m, 10th to 90th percentile) — kerbside parking and gutters, or lanes not tagged
- secondary: 374 streets read +3.7 m wider than their lanes make them at the median (+0.0 to +8.0 m, 10th to 90th percentile) — kerbside parking and gutters, or lanes not tagged
- primary: 250 streets read +3.3 m wider than their lanes make them at the median (-0.5 to +7.8 m, 10th to 90th percentile) — kerbside parking and gutters, or lanes not tagged
- unclassified: 161 streets read +5.5 m wider than their lanes make them at the median (+3.7 to +8.0 m, 10th to 90th percentile) — kerbside parking and gutters, or lanes not tagged
- trunk: 68 streets read +3.4 m wider than their lanes make them at the median (+0.4 to +8.6 m, 10th to 90th percentile) — kerbside parking and gutters, or lanes not tagged
- 202 streets read more than 8 m wider or 3 m narrower than their lanes make them, flagged; the layer keeps the lanes as OSM's and the width read as a reading
- e.g. w26532984 read 13.2 m, measured 7.5 m; w26542056 read 14 m, measured 7.3 m; w28463792 read 11.5 m, measured 32.2 m; w28802840 read 14 m, measured 10.1 m; w28851839 read 8.2 m, measured 13.9 m

### Roads traced off imagery against OSM's ways

- road: 20,948 stretches, 2615 km of 3013 km
- unmapped: 1,863 stretches, 193 km of 3013 km
- mixed: 729 stretches, 78 km of 3013 km
- walk: 474 stretches, 52 km of 3013 km
- paved: 469 stretches, 36 km of 3013 km
- rail: 181 stretches, 25 km of 3013 km
- aeroway: 48 stretches, 7 km of 3013 km
- road_across: 208 stretches, 5 km of 3013 km
- unbuilt: 9 stretches, 2 km of 3013 km
- an unmapped stretch is a road on the imagery with no OSM way of any kind beside it — a yard's drive, a track, or a road built or razed since — and is flagged, never laid as a road
- e.g. mlr7 45 m at 465460298,306165683; mlr24 71 m at 465516154,307400680; mlr83 108 m at 463424320,306080282; mlr88 75 m at 464820384,307174945; mlr89 41 m at 464615411,307173979

### Sightings by camera against what OSM maps for the same thing

- 2,333 from osmose — Mapillary's own need a client token in MAPILLARY_TOKEN; Osmose relays only the signs OSM lacks
- 22 are held by a junction, deciding 14 junctions' control where OSM maps nothing; 0 were laid as crossings OSM lacks
- 0 roads have lane arrows seen on them; 0 of them tag turn:lanes
- 2,333 sightings, last seen 2016-10-06 … 2021-08-29, half since 2020-05-06; one seen long ago may be gone

### OSM's roads: when each was last edited

- half the street length was last edited within 2.9 years of the survey; 16 % within a year, 22 % not for over five — an old edit is tags no one has touched since, not tags known wrong
- 2 roads carry a date a mapper checked them on the ground

### Buildings: a height and a level count on one building

- 302 buildings tag both; a storey is 3.2 m at the median; 25 (8.3 %) make one under 2 m, or a home's over 6 m
- the layer takes the height tag over the level count
- e.g. w160408062 (public) 1 m over 2 levels; w206223238 (house) 20 m over 2 levels; w225268794 (industrial) 1 m over 1 levels; w38141283 (house) 20 m over 3 levels; w38141289 (house) 25 m over 3 levels

### Addresses: the street a building's address names, and the streets OSM draws

- 13,373 OSM buildings name a street; 12,669 (94.7 %) one OSM draws, 412 a street by a name it has since dropped, 292 (2.2 %, 94 names) none — a misspelling, or a street OSM has not drawn
- the layer keeps the address as tagged, and gives the street's present name beside one that names it by an old one
- e.g. Косвена вулиця ×35; Виноградна ×28; Набарежна вулиця ×21; Садова ×19; вулиця Спрейса ×14

### Junction control: what OSM maps, what a camera saw where it maps nothing, and the rules

- rules: 20,709 junctions — unsigned 20,709
- osm: 789 junctions — signals 487, signs 161, roundabout 126, priority_road 14, blinking 1
- seen: 14 junctions — signals 14
- 33 junctions no signal holds have one near them by Osmose's reading of their crossings (2090), kept as a hint
- 58 junctions have a signalled crossing on one arm only and no signal of their own, and are read by their signs or the rules
- e.g. n10980471 unsigned; n271326641 unsigned; n298881958 unsigned; n317189358 unsigned; n321366711 unsigned


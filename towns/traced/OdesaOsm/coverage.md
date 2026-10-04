# OdesaOsm — enriched survey

Written by `qq osm --meta` beside `towns/traced/OdesaOsm.json`. Every OSM layer is read as OSM stood at the survey's own moment, 2026-10-03T09:16:51Z. 
Every place is lat, lon in OSM's 1e-7°; every road is the survey's way by its OSM id. Whether the layers hold together and how far their sources agree is `consistency.md`; `qq meta` answers questions of them by id, place, name or filter. Generated; do not edit.

## Sources

| Source | Data stands at | Licence |
|---|---|---|
| [osm-buildings](https://overpass-api.de/api/interpreter) | 2026-10-03T09:16:51Z | © OpenStreetMap contributors, ODbL 1.0 — https://www.openstreetmap.org/copyright |
| [osm-walk](https://overpass-api.de/api/interpreter) | 2026-10-03T09:16:51Z | © OpenStreetMap contributors, ODbL 1.0 — https://www.openstreetmap.org/copyright |
| [osm-zones](https://overpass-api.de/api/interpreter) | 2026-10-03T09:16:51Z | © OpenStreetMap contributors, ODbL 1.0 — https://www.openstreetmap.org/copyright |
| [osm-districts](https://overpass-api.de/api/interpreter) | 2026-10-03T09:16:51Z | © OpenStreetMap contributors, ODbL 1.0 — https://www.openstreetmap.org/copyright |
| [osm-transit](https://overpass-api.de/api/interpreter) | 2026-10-03T09:16:51Z | © OpenStreetMap contributors, ODbL 1.0 — https://www.openstreetmap.org/copyright |
| [osm-control](https://overpass-api.de/api/interpreter) | 2026-10-03T09:16:51Z | © OpenStreetMap contributors, ODbL 1.0 — https://www.openstreetmap.org/copyright |
| [osm-barriers](https://overpass-api.de/api/interpreter) | 2026-10-03T09:16:51Z | © OpenStreetMap contributors, ODbL 1.0 — https://www.openstreetmap.org/copyright |
| [osm-furniture](https://overpass-api.de/api/interpreter) | 2026-10-03T09:16:51Z | © OpenStreetMap contributors, ODbL 1.0 — https://www.openstreetmap.org/copyright |
| [osm-places](https://overpass-api.de/api/interpreter) | 2026-10-03T09:16:51Z | © OpenStreetMap contributors, ODbL 1.0 — https://www.openstreetmap.org/copyright |
| [osm-unbuilt](https://overpass-api.de/api/interpreter) | 2026-10-03T09:16:51Z | © OpenStreetMap contributors, ODbL 1.0 — https://www.openstreetmap.org/copyright |
| [osm-edits](https://overpass-api.de/api/interpreter) | 2026-10-03T09:16:51Z | © OpenStreetMap contributors, ODbL 1.0 — https://www.openstreetmap.org/copyright |
| [notes.json](https://api.openstreetmap.org/api/0.6/notes.json) | as fetched 2026-10-03T12:33:03Z | © OpenStreetMap contributors, ODbL 1.0 — https://www.openstreetmap.org/copyright |
| [ml-buildings-links.csv](https://bfppub.blob.core.windows.net/%24web/2026-08-13/dataset-links.csv) | as fetched 2026-10-03T12:33:06Z | Microsoft Global ML Building Footprints, ODbL 1.0 — https://github.com/microsoft/GlobalMLBuildingFootprints |
| [ml-buildings-Europe-120321213-0.csv.gz](https://bfppub.z5.web.core.windows.net/2026-08-13/global-buildings.geojsonl/RegionName=Europe/quadkey=120321213/part-00062-110f5303-ff85-4c71-a2bf-c6070024fec8.c000.csv.gz) | release 2026-08-13, traced off imagery of years before | Microsoft Global ML Building Footprints, ODbL 1.0 — https://github.com/microsoft/GlobalMLBuildingFootprints |
| [ml-buildings-Ukraine-120321211-1.csv.gz](https://bfppub.z5.web.core.windows.net/2026-08-13/global-buildings.geojsonl/RegionName=Ukraine/quadkey=120321211/part-00045-110f5303-ff85-4c71-a2bf-c6070024fec8.c000.csv.gz) | release 2026-08-13, traced off imagery of years before | Microsoft Global ML Building Footprints, ODbL 1.0 — https://github.com/microsoft/GlobalMLBuildingFootprints |
| [ml-buildings-Ukraine-120321213-2.csv.gz](https://bfppub.z5.web.core.windows.net/2026-08-13/global-buildings.geojsonl/RegionName=Ukraine/quadkey=120321213/part-00040-110f5303-ff85-4c71-a2bf-c6070024fec8.c000.csv.gz) | release 2026-08-13, traced off imagery of years before | Microsoft Global ML Building Footprints, ODbL 1.0 — https://github.com/microsoft/GlobalMLBuildingFootprints |
| [ml-roads.geojsonl](https://usaminedroads.z19.web.core.windows.net/drops/2025.04.28/Eastern_Europe.zip) | release 2025-04-28, traced off imagery of years before | Microsoft Road Detections, ODbL 1.0 — https://github.com/microsoft/RoadDetections |
| [dem-Copernicus_DSM_COG_10_N46_00_E030_00_DEM.tif](https://copernicus-dem-30m.s3.amazonaws.com/Copernicus_DSM_COG_10_N46_00_E030_00_DEM/Copernicus_DSM_COG_10_N46_00_E030_00_DEM.tif) | radar acquired 2011–2015 | Copernicus DEM (30 m) © DLR e.V. 2010-2014 and © Airbus Defence and Space GmbH 2014-2018, provided under COPERNICUS by the European Union and ESA; all rights reserved |
| [dtm-gedtm30.tif](https://s3.opengeohub.org/global/dtm/v1.2/gedtm_rf_m_30m_s_20060101_20151231_go_epsg.4326.3855_v1.2.tif) | terrain of 2006–2015 inputs | GEDTM30 v1.2, OpenGeoHub Foundation, CC BY 4.0 — https://doi.org/10.5281/zenodo.14900180 |
| [osmose-8300.json](https://osmose.openstreetmap.fr/api/0.3/issues.geojson) | analysed 2026-10-03T01:50:49Z | Osmose QA (osmose.openstreetmap.fr) over © OpenStreetMap contributors (ODbL); traffic signs detected by Mapillary (CC BY-SA 4.0) |
| [osmose-2090.json](https://osmose.openstreetmap.fr/api/0.3/issues.geojson) | analysed 2026-10-03T01:50:49Z | Osmose QA (osmose.openstreetmap.fr) over © OpenStreetMap contributors (ODbL); traffic signs detected by Mapillary (CC BY-SA 4.0) |
| [osmose-1070.json](https://osmose.openstreetmap.fr/api/0.3/issues.geojson) | analysed 2026-10-03T01:50:49Z | Osmose QA (osmose.openstreetmap.fr) over © OpenStreetMap contributors (ODbL); traffic signs detected by Mapillary (CC BY-SA 4.0) |
| [osmose-0.json](https://osmose.openstreetmap.fr/api/0.3/issues.geojson) | analysed 2026-10-03T01:50:49Z | Osmose QA (osmose.openstreetmap.fr) over © OpenStreetMap contributors (ODbL); traffic signs detected by Mapillary (CC BY-SA 4.0) |
| [osmose-4110.json](https://osmose.openstreetmap.fr/api/0.3/issues.geojson) | analysed 2026-10-03T01:50:49Z | Osmose QA (osmose.openstreetmap.fr) over © OpenStreetMap contributors (ODbL); traffic signs detected by Mapillary (CC BY-SA 4.0) |
| [osmose-3180.json](https://osmose.openstreetmap.fr/api/0.3/issues.geojson) | analysed 2026-10-03T01:50:49Z | Osmose QA (osmose.openstreetmap.fr) over © OpenStreetMap contributors (ODbL); traffic signs detected by Mapillary (CC BY-SA 4.0) |
| [osmose-1210.json](https://osmose.openstreetmap.fr/api/0.3/issues.geojson) | analysed 2026-10-03T01:50:49Z | Osmose QA (osmose.openstreetmap.fr) over © OpenStreetMap contributors (ODbL); traffic signs detected by Mapillary (CC BY-SA 4.0) |
| [osmose-3161.json](https://osmose.openstreetmap.fr/api/0.3/issues.geojson) | analysed 2026-10-03T01:50:49Z | Osmose QA (osmose.openstreetmap.fr) over © OpenStreetMap contributors (ODbL); traffic signs detected by Mapillary (CC BY-SA 4.0) |
| [osmose-1260.json](https://osmose.openstreetmap.fr/api/0.3/issues.geojson) | analysed 2026-10-03T01:50:49Z | Osmose QA (osmose.openstreetmap.fr) over © OpenStreetMap contributors (ODbL); traffic signs detected by Mapillary (CC BY-SA 4.0) |
| [osmose-2140.json](https://osmose.openstreetmap.fr/api/0.3/issues.geojson) | analysed 2026-10-03T01:50:49Z | Osmose QA (osmose.openstreetmap.fr) over © OpenStreetMap contributors (ODbL); traffic signs detected by Mapillary (CC BY-SA 4.0) |
| [osmose-9014.json](https://osmose.openstreetmap.fr/api/0.3/issues.geojson) | analysed 2026-10-03T01:50:49Z | Osmose QA (osmose.openstreetmap.fr) over © OpenStreetMap contributors (ODbL); traffic signs detected by Mapillary (CC BY-SA 4.0) |
| [osmose-3160.json](https://osmose.openstreetmap.fr/api/0.3/issues.geojson) | analysed 2026-10-03T01:50:49Z | Osmose QA (osmose.openstreetmap.fr) over © OpenStreetMap contributors (ODbL); traffic signs detected by Mapillary (CC BY-SA 4.0) |
| [osmose-1270.json](https://osmose.openstreetmap.fr/api/0.3/issues.geojson) | analysed 2026-10-03T01:50:49Z | Osmose QA (osmose.openstreetmap.fr) over © OpenStreetMap contributors (ODbL); traffic signs detected by Mapillary (CC BY-SA 4.0) |
| [osmose-2130.json](https://osmose.openstreetmap.fr/api/0.3/issues.geojson) | analysed 2026-10-03T01:50:49Z | Osmose QA (osmose.openstreetmap.fr) over © OpenStreetMap contributors (ODbL); traffic signs detected by Mapillary (CC BY-SA 4.0) |
| [osmose-3220.json](https://osmose.openstreetmap.fr/api/0.3/issues.geojson) | analysed 2026-10-03T01:50:49Z | Osmose QA (osmose.openstreetmap.fr) over © OpenStreetMap contributors (ODbL); traffic signs detected by Mapillary (CC BY-SA 4.0) |
| [osmose-3032.json](https://osmose.openstreetmap.fr/api/0.3/issues.geojson) | analysed 2026-10-03T01:50:49Z | Osmose QA (osmose.openstreetmap.fr) over © OpenStreetMap contributors (ODbL); traffic signs detected by Mapillary (CC BY-SA 4.0) |
| [osmose-4030.json](https://osmose.openstreetmap.fr/api/0.3/issues.geojson) | analysed 2026-10-03T01:50:49Z | Osmose QA (osmose.openstreetmap.fr) over © OpenStreetMap contributors (ODbL); traffic signs detected by Mapillary (CC BY-SA 4.0) |
| [osmose-3091.json](https://osmose.openstreetmap.fr/api/0.3/issues.geojson) | analysed 2026-10-03T01:50:49Z | Osmose QA (osmose.openstreetmap.fr) over © OpenStreetMap contributors (ODbL); traffic signs detected by Mapillary (CC BY-SA 4.0) |
| [osmose-8470.json](https://osmose.openstreetmap.fr/api/0.3/issues.geojson) | analysed 2026-10-03T01:50:49Z | Osmose QA (osmose.openstreetmap.fr) over © OpenStreetMap contributors (ODbL); traffic signs detected by Mapillary (CC BY-SA 4.0) |
| [gtfs.zip](https://files.mobilitydatabase.org/mdb-2946/latest.zip) | calendar in force 20250402 … 20261231 | Odesa City Council transport department GTFS, via Mobility Database (mdb-2946); licence unverified |

Not had this run:

- **mapillary** — no MAPILLARY_TOKEN: Mapillary's detections need a client token

Beyond reach of any source here:

- Odesa City Council's own geodata — signals and their timings, crossings, parking zones, the road register — is not published in a machine-readable form that can be reached: its organisation on data.gov.ua holds no datasets, its own site sits behind a browser check, and the cadastral map has been closed since 2022.
- Where public transport runs live, and so how fast a street is driven, is held by the operator and not published; the timetable is the only service figure.
- Microsoft's building model gives no heights in Ukraine; a building's height is OSM's or none.
- Neither height model is finer than 30 m: a ramp, a kerb or a grade over less than 60 m is not in them.
- A width read off imagery is the paved surface kerb to kerb, parking and gutters in it; how many lanes it is painted for, and how wide each, no open source gives.

## Layers

### zones — 10,802 items, 5,050 KB

Every surface that is a place rather than a road — car parks, fuel, garages, industry, port, schools, markets, squares, parks, water, land use — its outline in 1e-7°, and the survey roads inside it and leading into it.

- **kinds**: residential 2195, green 1637, leisure 1154, parking 1121, nature 896, industrial 774, education 481, agriculture 406, commercial 266, fuel 189, water 173, garages 167, civic 137, parking_space 131, construction 131, health 117, car_wash 107, charging_station 94, pier 91, retail 81, beach 81, square 63, parking_entrance 53, market 48, cemetery 38, religious 33, airport 32, waste_land 29, land 20, bridge 19, military 9, railway 9, allotments 7, bus_station 7, ferry_terminal 4, port 2
- **parking**: unsaid 558, surface 296, derived_from_aisles 108, street_side 90, lane 24, underground 21, multi-storey 13, garage_boxes 9, carports 1, on_kerb 1
- **roadsInZones**: 4045
- **roadsInZonesByKind**: parking 1057, industrial 1020, leisure 305, garages 297, education 269, cemetery 222, airport 154, agriculture 154, market 144, health 137, construction 126, fuel 57, railway 18, bridge 17, port 13, religious 13, car_wash 8, civic 8, square 7, allotments 7, bus_station 6, military 4, pier 2
- **derivedLots**: 108

### districts — 220 items, 261 KB

The town's administrative districts (admin_level 7-10) with their outlines, and its named places at a point each.

- **levels**: admin_level_10 4, admin_level_7 9, admin_level_9 5, city 2, village 39, town 5, hamlet 2, neighbourhood 83, suburb 71

### walk — 7,355 items, 2,165 KB

Every way OSM draws for feet: its kind, for a pavement the road it runs beside, on which side as the road is drawn and how far off, its nodes for joining the network, its line in 1e-7° and its tags.

- **kinds**: footway 3891, sidewalk 1827, path 759, steps 407, crossing 311, pedestrian_street 66, cycleway 36, area_traffic_island 19, area_footway 13, footway_area 11, pedestrian_area 8, island 4, area_pedestrian 2, platform 1
- **kmByKind**: footway 332.3, sidewalk 180, path 114, pedestrian_street 17.5, cycleway 14.5, steps 8.5, crossing 6.3, area_traffic_island 3, footway_area 2.8, area_footway 2.1, pedestrian_area 1.8, area_pedestrian 0.8, platform 0.1, island 0
- **sidewalksByGeometry**: 1294
- **sidewalksWithNoRoad**: 59
- **networkPieces**: 1414
- **largestPieceKm**: 82.3

### buildings — 206,359 items, 48,879 KB

Every building: OSM's outline and parts with levels, height (and where the height came from), use, address, the ways passing under it and its entrances; and every machine-traced footprint OSM does not have.

- **osm**: 61895
- **parts**: 745
- **mlOnly**: 143719
- **mlFootprintsRead**: 189348
- **mlOnOsmOnceAligned**: 2640
- **mlOverlappingOsm**: 671
- **mlOnStreets**: 2121
- **mlShiftPairs**: 23298
- **mlMovedAtMedian**: 1.4 m west, 0.6 m north
- **heightFrom**: none 42048, levels 19380, height 467
- **withAddress**: 21196
- **withPassages**: 1524
- **withEntrances**: 2182
- **uses**: yes 35739, house 9319, apartments 8794, industrial 3326, retail 828, garages 528, garage 397, detached 313, residential 295, office 284, school 254, roof 252, commercial 228, service 182, carport 165, dormitory 129, hospital 111, construction 90, university 88, greenhouse 80, kiosk 52, guardhouse 48, kindergarten 47, church 46, hotel 40
- **areaKm2**: 23.34

### crossings — 2,759 items, 962 KB

Every place a road is crossed on foot or by rail: its node, the crossing way drawn across where there is one — or the camera's sighting of it where OSM maps none — its kind, the road it crosses and where along it, and the nearest junction.

- **types**: pedestrian 1782, rail 287, tram 690
- **pedestrianKinds**: signals 780, marked 428, zebra 224, unknown 199, unmarked 151
- **painted**: yes 860, unsaid 765, no 157
- **sources**: node 2420, node+way 335, way_inferred 4
- **atJunction**: 2428
- **midBlock**: 331

### junctions — 21,512 items, 30,110 KB

Every node of three road arms or more: its arms, its control, what decided it — OSM, a camera's sighting where OSM maps nothing, or the rules — and the evidence, the signals, signs and crossings it holds, and every movement through it with whether a car may make it and from which lanes.

- **control**: unsigned 20709, signals 501, signs 161, roundabout 126, priority_road 14, blinking 1
- **controlFrom**: rules 20709, osm 789, seen 14
- **rules**: right_hand 16772, unpaved_yields 313, adjacent_territory 3624
- **clusters**: 684
- **hintedSignals**: 33
- **movements**: 197736
- **forbidden**: 1761
- **conditional**: 23
- **paintedArms**: 210

### levels — 1,998 items, 319 KB

Every road, railway and way for feet that is not on the ground: its layer and where that came from, the structure it is on, the outlined bridge holding it and the building over it.

- **byClass**: road 1594, rail 140, walk 264
- **structures**: road:building_passage 1368, road:covered 102, walk:building_passage 80, road:bridge 58, walk:bridge 53, walk:tunnel 49, rail:covered 48, walk:covered 40, walk:layer 38, road:tunnel 36, rail:building_passage 32, road:layer 30, rail:bridge 29, rail:layer 26, rail:tunnel 5, walk:viaduct 2, walk:boardwalk 1, walk:indoor 1
- **layers**: -1 129, 0 1662, 1 180, 2 26, 3 1
- **bridgesOnOutlines**: 44
- **underBuildings**: 1607

### overpasses — 393 items, 76 KB

Every place two ways cross with no node between them: where, the two ways and their levels, which passes over, and whether OSM's tags explain it.

- **pairs**: road/rail 182, road/road 71, road/walk 54, rail/road 50, walk/road 36
- **unexplained**: road/walk 16, road/rail 103, road/road 1

### routes — 171 items, 1,067 KB

Every public transport route, OSM's relations and the timetable's routes laid as one: its mode, ref and name, its ways and stops in order, and where the timetable runs it its service each day — trips, first and last, headway.

- **sources**: osm 98, timetable 73
- **modes**: bus 105, tram 37, trolleybus 14, train 11, ferry 4
- **withTimetable**: 134
- **timetableInForce**: 20250402 … 20261231
- **timetableRoutesRunningNoDay**: 18
- **timetableReadOnce**: routes listed twice 2, stops listed twice 1802, trips listed twice 439, calls listed twice 33161, calls at a stop the feed does not list 84

### stops — 2,977 items, 1,409 KB

Every stop, stop position and platform, OSM's and the timetable's laid as one: what it is, its name and modes, the road it stands beside and on which side, the routes calling there, and the timetable stops it is.

- **sources**: osm 2555, timetable 422
- **kinds**: stop_position 1155, platform 1314, station 27, halt 11, tram_stop 1, bus_stop 47, timetable_stop 422
- **withTimetable**: 1442
- **withRoutes**: 2449
- **besideNoRoad**: 100

### tracks — 1,561 items, 845 KB

Every railway and tram track: its kind, the street a tram runs down where it does, its nodes and line.

- **kinds**: rail 634.3, tram 193.6, funicular 0.1, disused 7.5, abandoned 8.9, monorail 5.9
- **tramInStreetKm**: 79.2

### places — 12,703 items, 3,806 KB

Every place a trip may be made to — shop, amenity, office, sight, sport — at one point each, with its kind, name and tags.

- **kinds**: amenity 5880, shop 3435, craft 78, tourism 538, office 461, historic 584, leisure 1665, healthcare 56, club 6
- **top**: amenity=parking 1005, amenity=cafe 573, amenity=pharmacy 510, shop=convenience 416, leisure=pitch 386, leisure=playground 369, leisure=swimming_pool 303, amenity=restaurant 288, historic=memorial 277, shop=supermarket 268, historic=heritage 260, shop=clothes 252, amenity=post_office 227, amenity=bank 224, amenity=school 218, amenity=atm 209, amenity=waste_disposal 191, amenity=fuel 153, amenity=fast_food 151, leisure=park 151, amenity=kindergarten 147, tourism=artwork 142, shop=car_repair 136, tourism=hotel 129, shop=beauty 128, amenity=place_of_worship 128, shop=bakery 124, amenity=toilets 123, amenity=payment_terminal 113, amenity=doctors 110

### furniture — 8,744 items, 1,658 KB

What stands along the streets — trees, lamps, benches, shelters, cabinets, poles, hydrants — each with the road within reach it stands by and on which side as the road is drawn.

- **kinds**: natural=tree 1892, man_made=surveillance 1791, power=tower 1756, amenity=bench 848, amenity=waste_basket 415, man_made=manhole 370, amenity=atm 209, advertising=billboard 190, tourism=artwork 139, amenity=bicycle_parking 137, highway=street_lamp 118, natural=tree_row 91, amenity=recycling 88, amenity=toilets 77, amenity=vending_machine 74, amenity=parcel_locker 74, amenity=shelter 73, amenity=fountain 41, man_made=mast 41, tourism=information 40, man_made=tower 39, man_made=flagpole 31, power=pole 28, natural=rock 26, man_made=chimney 24, emergency=fire_hydrant 24, power=transformer 19, amenity=drinking_water 15, amenity=bicycle_rental 14, leisure=picnic_table 11
- **byARoad**: 5607

### barriers — 5,839 items, 1,083 KB

Every barrier OSM draws as a line — wall, fence, hedge, kerb, guard rail, retaining wall — which a pedestrian or car does not pass but at a gap or gate.

- **kinds**: barrier=wall 3233, barrier=fence 2275, barrier=kerb 174, barrier=retaining_wall 59, barrier=bollard 37, barrier=gate 17, barrier=guard_rail 11, barrier=yes 10, barrier=ditch 4, barrier=hedge 4, barrier=block 3, barrier=chain 3, barrier=border_control 2, barrier=wicket_gate 2, barrier=planter 2, barrier=w 1, barrier=city_wall 1, barrier=step 1

### unbuilt — 42 items, 22 KB

Roads OSM knows are under construction, proposed, disused or abandoned, which a later survey may find built or gone.

- **kinds**: highway=construction 31, highway=proposed 11

### detected — 24,929 items, 3,642 KB

Every stretch of road Microsoft's model traced off imagery over the map: its line, the width it read, the survey roads it was laid along, or what else of OSM's it lies on — a way for feet, a track, a taxiway, a paved lot — or that OSM has nothing there.

- **byStatus**: road 20948, unmapped 1863, mixed 729, walk 474, paved 469, road_across 208, rail 181, aeroway 48, unbuilt 9
- **kmByStatus**: road 2615, unmapped 193.4, mixed 77.9, walk 52.3, paved 36.2, rail 25.2, aeroway 6.5, road_across 5.1, unbuilt 1.6
- **roadsWithWidth**: 12068
- **roadsReadAcrossTwo**: 1964

### unmapped — 1,160 items, 224 KB

Every road the imagery shows that OSM has no way of any kind for: its unmapped stretches joined where they meet, its lines, length and width, and the survey roads its free ends meet.

- **networks**: 1160
- **flagged**: 992
- **km**: 193.4
- **meetingTheSurvey**: 737

### seen — 2,333 items, 542 KB

Every traffic sign, traffic light and road marking a camera saw over the map — Mapillary's own detections and those Osmose relays — what it is, where, which way it faces, when it was seen, the road and junction it stands by, what OSM maps near it for the same thing, and the junction or crossing it was laid as where OSM maps none.

- **bySource**: osmose 2333
- **byKind**: speed_limit 1781, bump 202, living_street 103, max_height 55, one_way 52, max_weight 50, roundabout 37, traffic_light 37, sign 16
- **agreeing**: 
- **heldByJunctions**: 22
- **laidAsCrossings**: 0
- **lastSeenByYear**: 2016 6, 2017 15, 2018 17, 2019 274, 2020 1543, 2021 121

### roads — 21,422 items, 10,411 KB

Every survey road with what the other layers know of it: its role (street, link, driveway, a zone's way), lanes and widths — as tagged, as its lanes make it, as measured off a mapped surface and as read off imagery — building frontage each side, pavements each side, street parking, zone, land use, district, level, the ground's height at each node and the grade, surface, light, speed, trams, routes, crossings and calming.

- **roles**: street 11623, driveway 4877, zone_way 3079, track 1099, alley 590, link 150, busway 4
- **roleKm**: street 2672.1, track 515.5, zone_way 481.2, driveway 226.3, alley 94.7, link 15.8, busway 0.1
- **streetSideKmBySidewalk**: likely 2106.8, unknown 2985.1, separate 106.3, yes 52.5, no 93.6
- **widthMeasured**: 426
- **widthTagged**: 95
- **widthDetected**: 12068
- **streetKmByWidthFrom**: imagery 2063, lanes 548, surface 57, tag 4.1
- **heightsFrom**: dtm-gedtm30.tif
- **withFrontage**: 15382
- **withHeights**: 21422
- **steeperThan8Pct**: 678
- **withTram**: 87
- **withRoutes**: 1984
- **withStreetParking**: 28
- **streetKmByYearEdited**: 2009 0.5, 2011 1.2, 2012 7.3, 2013 12.2, 2014 5.5, 2015 15.1, 2016 26.2, 2017 85.3, 2018 20.8, 2019 223.1, 2020 78.1, 2021 181.1, 2022 118.4, 2023 867.1, 2024 337.9, 2025 358.9, 2026 333.6
- **withCheckDate**: 2

### quality — 7,824 items, 1,528 KB

Where the sources disagree with themselves or each other: flags raised while the layers were laid, every Osmose issue of the items read (with the tags a Mapillary-detected sign would add), and every open OSM note.

- **road_not_in_osm**: 992
- **osmose_3161_316150: {{0.key}}={{0.value}} without {{1.key}}=***: 557
- **osmose_8300_1: Unmapped max speed limit 5**: 463
- **timetable_stop_not_in_osm**: 422
- **address_names_old_street**: 412
- **osmose_8300_7: Unmapped max speed limit 40**: 387
- **address_street_not_drawn**: 292
- **osmose_2140_21412: Missing legacy tag on a public transport stop**: 269
- **osmose_8300_5: Unmapped max speed limit 30**: 268
- **osmose_8300_2: Unmapped max speed limit 10**: 211
- **osmose_1210_5: Unconnected drive-through**: 205
- **osmose_8300_34: Unmapped road bump**: 203
- **lanes_and_detected_width_disagree**: 202
- **osmose_1070_13: Bad intersection with railway**: 175
- **osmose_8300_4: Unmapped max speed limit 20**: 148
- **osmose_3161_2: Missing access way to parking**: 145
- **osmose_2140_21402: Missing network tag on a public_transport relation**: 120
- **osmose_9014_9014019: A bus stop is supposed to be a node**: 110
- **parking_aisles_without_lot**: 108
- **osmose_3220_32201: Overly permissive access**: 107
- **osmose_8300_39: Unmapped living street**: 104
- **osmose_8300_9: Unmapped max speed limit 50**: 101
- **osmose_8470_130: Unmapped cycling infrastructure**: 101
- **osm_note**: 93
- **osmose_4030_900: Tag conflict**: 92
- **osmose_1270_1: Almost junction, join or use noexit tag**: 86
- **timetable_route_not_in_osm**: 68
- **osmose_8300_11: Unmapped max speed limit 70**: 67
- **osmose_8300_10: Unmapped max speed limit 60**: 64
- **road_rail_cross_unjoined**: 61
- **sidewalk_beside_no_road**: 59
- **osmose_8300_20: Unmapped max height limit**: 55
- **osmose_8300_53: Unmapped one-directional roads**: 52
- **osmose_8300_21: Unmapped max weight limit**: 50
- **osmose_2140_21411: Missing public_transport tag on a public transport stop**: 48
- **osmose_2140_21403: Missing operator tag on a public_transport relation**: 48
- **osmose_2090_2: Possible missing highway=traffic_signals nearby**: 47
- **osmose_0_4: Gap between buildings**: 39
- **osmose_8300_32: Unmapped roundabout**: 37
- **osmose_8300_38: Unmapped traffic signals**: 37
- **crossing_way_over_no_road**: 36
- **osmose_3180_1: Restriction relation, wrong number of members**: 36
- **sidewalk_tag_and_drawing_disagree**: 34
- **osmose_1070_2: Tree intersecting building**: 34
- **osmose_1070_8: Highway intersecting highway**: 31
- **osmose_3032_30322: {{0.key}}={{0.value}} together with {{1.key}}={{1.value}}, usually {{1.key}}={{1.value}} is located underneath the {{0.value}}. Tag the {{1.key}} as a separate object.**: 30
- **osmose_8300_12: Unmapped max speed limit 80**: 29
- **osmose_0_2: Large building intersection**: 26
- **height_and_levels_disagree**: 25
- **osmose_0_1: Building intersection**: 25
- **osmose_1070_1: Highway intersecting building**: 24
- **osmose_4110_41106: Long Highway above ground and no bridge**: 24
- **osmose_3160_31604: Conflict between lanes number**: 23
- **osmose_1210_4: Small highway group apart from the main network or with insufficient access upstream**: 22
- **osmose_8300_13: Unmapped max speed limit 90**: 19
- **osmose_8300_51: Unmapped animal crossing**: 16
- **osmose_2130_4: Barrier blocking highway**: 16
- **osmose_3032_303210: Fence with {{1.key}} tag, also add {{2.key}}**: 15
- **osmose_8300_3: Unmapped max speed limit 15**: 13
- **osmose_4110_41103: Highway underground and no tunnel**: 13
- **osmose_3161_3: Inconsistent access of parking**: 13
- **osmose_4110_41105: Highway above ground and no bridge**: 12
- **osmose_8300_8: Unmapped max speed limit 45**: 10
- **osmose_2140_21405: Missing from/to tag on a public_transport route relation**: 10
- **crossing_node_off_road**: 9
- **give_way_without_junction**: 9
- **osmose_1260_1: The track of this route contains gaps**: 9
- **osmose_2090_3: Possible missing traffic_signals:direction tag or crossing on traffic signals**: 8
- **osmose_4110_41104: Long Highway underground and no tunnel**: 8
- **osmose_3160_31605: Invalid usage of *:lanes:(backward|both_ways) on oneway highway**: 8
- **osmose_1070_6: Power object intersecting building**: 7
- **osmose_1260_10: Stop position without platform nor bus stop**: 7
- **osmose_1260_4: Public transport relation route not in route_master relation**: 7
- **osmose_9014_9014009: Missing transportation mode, add a tag route = bus/coach/tram/etc**: 7
- **osmose_3091_3091: Invalid numerical value**: 7
- **signals_without_junction**: 6
- **osmose_8300_15: Unmapped max speed limit 110**: 6
- **timetable_direction_not_on_osm**: 5
- **osmose_3180_4: Restriction relation, bad oneway direction on "from" or "to" member**: 5
- **osmose_1260_2: The stop or platform is too far from the track of this route**: 5
- **crossing_way_not_joined**: 4
- **osmose_1070_7: Power object and highway too close**: 4
- **osmose_1210_3: One way inaccessible or missing parking or parking entrance**: 4
- **width_tag_and_surface_disagree**: 3
- **osmose_8300_14: Unmapped max speed limit 100**: 3
- **osmose_8300_16: Unmapped max speed limit 120**: 3
- **osmose_1070_3: Tree and highway too close**: 3
- **osmose_1260_5: network, operator, ref, colour tag should be the same on route and route_master relations**: 3
- **osmose_1260_11: The stops may not be in the right order**: 3
- **osmose_1260_6: The bus stop is part of a way, it should have public_transport=stop_position tag**: 3
- **osmose_2140_21401: Missing public_transport:version tag on a public_transport route relation**: 3
- **osmose_9014_9014022: The duration is invalid**: 3
- **osmose_3091_30916: Invalid value of charge**: 3
- **osmose_3091_30911: Colour code should start with '#' followed by 3 or 6 digits**: 3
- **crossing_node_and_way_disagree**: 2
- **osmose_1070_5: Highway intersecting large water piece**: 2
- **osmose_1070_9: Highway overlaps**: 2
- **osmose_1210_1: Unconnected cycleway**: 2
- **osmose_3160_31608: Conflict between lanes number of same suffix**: 2
- **road_road_cross_unjoined**: 1
- **road_through_building**: 1
- **osmose_2090_4: Possible missing direction tag on stop or a give way**: 1
- **osmose_1070_16: Commercial object or office and highway too close**: 1
- **osmose_4110_41108: Long Waterway/water underground and no tunnel**: 1
- **osmose_4110_41101: Landuse feature not on ground**: 1
- **osmose_4110_41107: Waterway/water underground and no tunnel**: 1
- **osmose_3180_3: Unconnected restriction relation ways**: 1
- **osmose_3161_31622: parking:[side]:* without parking:[side] value**: 1
- **osmose_1260_12: The platform is not on the right side of the road**: 1
- **osmose_1260_8: The platform is part of a way, it should have the role stop**: 1
- **osmose_3160_31607: Bad turn lanes order**: 1
- **osmose_2130_1: Inconsistent Access**: 1
- **osmose_3091_3092: Suspicious numerical value**: 1
- **osmose_3091_309120: suspicious tag combination**: 1


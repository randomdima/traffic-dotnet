# Town prop plan

The props a town scatters: everything small and static, none larger than a car (4.5 × 1.8 m, a crown 4.5 m across).
Three sets by placement (`PropKind`, GEN-6b), each folder under `assets/world/prop/variants/`:

- **wild/** (kind 0) — open-ground scenery and the wild share of a verge: young trees, conifers, shrubs, weeds and
  wildflowers, rocks and debris. Upright, never turned.
- **planted/** (kind 1) — what a town plants along a walk: street trees, beds, hedges, planters, pots, topiary.
- **street/** (kind 2) — what a town stands beside its cars: bins, bollards, barriers, benches, bikes, cabinets, stalls,
  goods.

**Sizes are true, from above**: a disc is its crown or rim across; a box `W×D` is width (along the street, a turned
look's
x) by depth, its thick bottom edge the side facing the walk. The plan lays each look at its own size; nothing is
stretched. The setting is a Black Sea port city in summer: Soviet-era leftovers beside new street furniture, white
acacia, chestnuts, planes and lilac, painted-tyre flower beds, kvass and watermelons.

Columns: `id`, `folder` (under the kind's folder), `size` (`D` disc metres, `WxD` box, `WxDrR` box with corner radius
R), `turns` (`y` a front to turn by the street), `describe` (what shows from straight above, nothing else).

## sheet trees_large

| id | folder | size | turns | describe |
|---|---|---|---|---|
| oak_young | wild/tree | 4.2 | | young English oak crown, dense lobed dark green leaves in rounded clumps, a few gaps of shadow between them |
| walnut_young | wild/tree | 4.4 | | young walnut crown, broad and open, large pale olive-green compound leaves, airy with dark gaps |
| horse_chestnut_young | wild/tree | 4.4 | | young horse chestnut crown, big hand-shaped leaves in heavy deep green clumps, a few upright white flower candles |
| plane_young | wild/tree | 4.5 | | young plane tree crown, broad maple-like light green leaves, loose layered clumps |
| sycamore_young | wild/tree | 4.0 | | young sycamore maple crown, dense mid green five-lobed leaves, rounded billowing shape |
| willow_young | wild/tree | 4.0 | | young weeping willow crown, fine silvery green narrow leaves in soft streaming tufts radiating from the centre |
| lime_young | wild/tree | 3.8 | | young linden crown, heart-shaped fresh green leaves, smooth even dome with pale yellow-green blossom flecks |
| copper_beech_young | wild/tree | 3.8 | | young copper beech crown, dense deep purple-red leaves with darker shadowed clumps |
| ash_young | wild/tree | 3.8 | | young ash crown, feathery mid green leaflets, open and light, dark shadowed hollows between the sprays |
| robinia_young | wild/tree | 3.6 | | young black locust (wild acacia) crown, small rounded bright yellow-green leaflets, open lacy texture |
| elm_young | wild/tree | 3.6 | | young field elm crown, small rough dark green leaves, irregular lumpy outline |
| field_maple_young | wild/tree | 3.6 | | young field maple crown, small lobed leaves in mid green with a few yellowing ones, compact |

## sheet trees_mid

| id | folder | size | turns | describe |
|---|---|---|---|---|
| hornbeam_young | wild/tree | 3.4 | | young hornbeam crown, pleated oval mid green leaves, dense even dome |
| birch_clump | wild/tree | 3.4 | | three young silver birches growing together, three small light green airy crowns merged, a glimpse of white bark at the middle |
| pine_scots | wild/conifer | 3.2 | | Scots pine crown, blue-green needle tufts in flat irregular clusters, orange-brown branch glimpses between them |
| apple_tree | wild/tree | 3.0 | | old apple tree crown, mid green leaves, scattered small red-green apples |
| alder_young | wild/tree | 3.0 | | young alder crown, round dark glossy green leaves, slightly ragged |
| spruce_norway | wild/conifer | 3.0 | | Norway spruce seen from above, dark green star of drooping layered branches round a pointed centre |
| birch_silver | wild/tree | 2.8 | | young silver birch crown, small fresh light green leaves, light and airy, fine drooping twigs, darker shadow between the twig clusters |
| plum_purple | wild/tree | 2.8 | | cherry plum crown, small dark purple-red leaves, dense and round |
| aspen_young | wild/tree | 2.8 | | young aspen crown, round fluttering light green leaves with silvery undersides showing |
| oak_sapling | wild/tree | 2.6 | | oak sapling, small open crown of lobed dark green leaves |
| rowan | wild/tree | 2.6 | | rowan crown, feathery mid green leaflets, several clusters of orange-red berries |
| dead_snag | wild/tree | 2.6 | | dead tree seen from above, bare grey branches radiating from the trunk, no leaves, a little ivy at the centre |
| fir_silver | wild/conifer | 2.6 | | silver fir seen from above, dense dark green layered branch star, pale tips |
| hawthorn_tree | wild/tree | 2.4 | | small hawthorn tree crown, small dark green leaves dotted with clusters of white blossom |
| yew_round | wild/conifer | 2.4 | | old yew, a dense very dark green rounded mound of fine needles |
| cherry_wild | wild/tree | 2.4 | | wild cherry crown, glossy green leaves with scattered dark red cherries |

## sheet shrubs_large

| id | folder | size | turns | describe |
|---|---|---|---|---|
| hazel_coppice | wild/shrub | 2.4 | | hazel coppice, many stems, broad round soft green leaves in a loose mound |
| elder_flowering | wild/shrub | 2.2 | | elder bush, dark green leaves with flat creamy white flower plates |
| bramble_tangle | wild/shrub | 2.2 | | bramble tangle, arching thorny stems, dark green leaves, a few black and red berries |
| tamarisk | wild/shrub | 2.2 | | tamarisk shrub, feathery grey-green sprays tipped with fluffy pale pink flowers |
| lilac_wild | wild/shrub | 2.0 | | lilac bush, heart-shaped green leaves and many cone-shaped purple-lilac flower clusters |
| pine_young | wild/conifer | 2.0 | | young pine, a small star of fresh green needle tufts |
| sea_buckthorn | wild/shrub | 1.8 | | sea buckthorn shrub, narrow silvery grey-green leaves, dense orange berries along the twigs |
| hawthorn_shrub | wild/shrub | 1.8 | | hawthorn shrub, small lobed dark green leaves, a few dark red haws |
| viburnum | wild/shrub | 1.8 | | guelder rose shrub, maple-like green leaves with clusters of glossy red berries |
| spruce_small | wild/conifer | 1.8 | | small spruce, a tight dark green star of layered branches |
| dog_rose_wild | wild/shrub | 1.6 | | wild dog rose, arching stems, small green leaves, pale pink five-petal flowers |
| gorse_flowering | wild/shrub | 1.6 | | gorse bush, spiky dark green, smothered in bright yellow flowers |
| privet_wild | wild/shrub | 1.6 | | unclipped privet bush, small glossy oval leaves, a few white flower sprays |

## sheet shrubs_growth

| id | folder | size | turns | describe |
|---|---|---|---|---|
| mock_orange | wild/shrub | 1.6 | | mock orange shrub, mid green leaves covered in white four-petal flowers |
| mixed_wildflowers | wild/growth | 1.6 | | patch of mixed summer meadow, tall grass with red, yellow, white and blue wildflowers |
| dry_grass_patch | wild/growth | 1.6 | | patch of sun-dried yellow and straw summer grass, a few green blades |
| blackthorn | wild/shrub | 1.6 | | blackthorn thicket, dense dark twigs, small dark leaves, dusty blue sloes |
| broom_yellow | wild/shrub | 1.4 | | broom bush, thin green whippy stems covered in yellow pea flowers |
| sapling_cluster | wild/shrub | 1.4 | | cluster of self-sown maple and ash saplings, a few small leafy tops |
| reed_clump | wild/growth | 1.4 | | clump of common reed, long narrow grey-green leaves radiating, a few brown plumes |
| nettles_stand | wild/growth | 1.4 | | stand of stinging nettles, toothed dark green leaves in dense tiers |
| cow_parsley_stand | wild/growth | 1.4 | | cow parsley, lacy fern-like leaves with many flat white flower umbels |
| ferns_bracken | wild/growth | 1.4 | | bracken ferns, large fronds radiating from the centre, bright green |
| snowberry | wild/shrub | 1.2 | | snowberry shrub, small round blue-green leaves with white berries |
| spirea_white | wild/shrub | 1.2 | | bridal spirea, arching sprays covered in tiny white flowers |
| feather_grass | wild/growth | 1.2 | | steppe feather grass tussock, fine silvery plumes swirling outward |
| burdock | wild/growth | 1.2 | | burdock plant, huge heart-shaped dark green leaves overlapping, a few purple burrs |
| mugwort_silver | wild/growth | 1.2 | | wormwood clump, feathery silvery grey-green foliage |
| poppies_red | wild/growth | 1.2 | | patch of red field poppies among grass |

## sheet growth_small

| id | folder | size | turns | describe |
|---|---|---|---|---|
| moss_mat | wild/growth | 1.2 | | low mat of bright green moss and short grass, soft lumpy |
| thistles_purple | wild/growth | 1.0 | | spear thistle, spiky grey-green leaves, purple flower heads |
| steppe_grass_clump | wild/growth | 1.0 | | dry steppe grass tussock, fine blond and green blades |
| chicory_blue | wild/growth | 1.0 | | chicory and cornflowers in grass, sky-blue flowers on thin stems |
| chamomile_wild | wild/growth | 1.0 | | wild chamomile patch, many small white daisies with yellow centres on feathery leaves |
| bindweed | wild/growth | 1.0 | | field bindweed creeping over grass, arrow-shaped leaves, white and pale pink trumpet flowers |
| clover_white | wild/growth | 0.9 | | patch of white clover, trefoil leaves and round white flower heads |
| daisies_lawn | wild/growth | 0.9 | | patch of lawn daisies in short grass, tiny white flowers |
| tussock_tall | wild/growth | 0.8 | | tall grass tussock, long arching green blades with seed heads |
| sunflowers_volunteer | wild/growth | 0.8 | | two self-sown sunflowers seen from above, big yellow heads with dark centres over broad leaves |
| horsetail | wild/growth | 0.8 | | clump of field horsetail, fine bright green bottlebrush whorls |
| dandelions_seedheads | wild/growth | 0.7 | | dandelion rosette, jagged leaves, yellow flowers and white seed clocks |

## sheet rocks

| id | folder | size | turns | describe |
|---|---|---|---|---|
| brushwood_pile | wild/rock | 1.6 | | heap of cut brushwood and dry branches, grey-brown twigs, a few dead leaves |
| boulder_granite | wild/rock | 1.4 | | grey granite boulder, rounded with flat facets, speckled, a little lichen |
| rubble_heap | wild/rock | 1.4 | | small heap of broken concrete and brick rubble overgrown with weeds |
| boulder_limestone | wild/rock | 1.2 | | pale cream shell limestone block, porous and pitted, softly rounded edges |
| mossy_rock | wild/rock | 1.2 | | low rock half covered in green moss |
| stone_pile | wild/rock | 1.2 | | loose pile of grey field stones, fist to head size |
| stump_mossy | wild/rock | 1.0 | | old tree stump, rotting grey wood, moss and small fungi round the rim |
| rocks_cluster | wild/rock | 1.0 | | cluster of three grey stones in grass |
| flat_stone | wild/rock | 0.9 | | flat grey slab of stone lying in grass, slightly cracked |
| stump_fresh | wild/rock | 0.8 | | freshly cut tree stump, pale wood face with growth rings, bark rim |
| old_tyre | wild/rock | 0.7 | | old car tyre lying flat in grass, black rubber ring, weeds growing through the middle |

## sheet street_trees

| id | folder | size | turns | describe |
|---|---|---|---|---|
| chestnut_street | planted/tree | 4.4 | | mature street horse chestnut crown, tidy dome of big hand-shaped deep green leaves, white flower candles |
| plane_pollard | planted/tree | 3.6 | | pollarded street plane, a compact crown of fresh light green maple-like leaves growing from knuckled branch ends |
| acacia_white | planted/tree | 3.8 | | white acacia street tree, airy light green leaflets with hanging clusters of white flowers |
| catalpa | planted/tree | 3.6 | | catalpa street tree, very large heart-shaped pale green leaves, a few white flower spikes |
| lime_clipped | planted/tree | 3.4 | | clipped street linden, a neat even dome of heart-shaped green leaves |
| cherry_ornamental | planted/tree | 3.0 | | ornamental cherry in bloom, a round crown of soft pink blossom with a few green leaves |
| maple_globe | planted/tree | 2.8 | | globe maple, a perfectly round tight ball of bright green lobed leaves |
| lime_staked | planted/tree | 2.2 | | newly planted young linden, a small round crown, two wooden support stakes and a ring of bare soil at the centre |
| maple_staked | planted/tree | 2.4 | | newly planted young red maple, a small crown of dark red leaves, a ring of bark mulch |

## sheet beds_hedges

| id | folder | size | turns | describe |
|---|---|---|---|---|
| hedge_hornbeam | planted/hedge | 3.6x1.0r0.15 | y | clipped hornbeam hedge, a flat-topped block of small pleated green leaves, square ends |
| hedge_box_long | planted/hedge | 3.0x0.8r0.15 | y | clipped box hedge, a dense flat block of tiny dark green leaves |
| bed_tulips | planted/bed | 2.6x1.2r0.2 | y | rectangular flower bed of dark soil with rows of red and yellow tulips, a low concrete kerb round it |
| bed_roses_round | planted/bed | 2.4 | | round rose bed, a ring of white-painted stones round dark soil, red and pink rose bushes |
| hedge_privet | planted/hedge | 2.4x1.0r0.2 | y | clipped privet hedge, a block of small glossy green leaves, slightly uneven top |
| bed_mixed_round | planted/bed | 2.0 | | round flower bed, petunias and marigolds in rings of purple, orange and white |
| bed_lavender | planted/bed | 2.0x0.8r0.2 | y | strip of lavender, silvery mounds with purple flower spikes, gravel between |
| raised_bed_brick | planted/bed | 1.8x1.0 | y | raised bed in a low red brick wall, dark soil with herbs and marigolds |
| bed_marigolds | planted/bed | 1.6x0.8r0.1 | y | small bed of orange and yellow marigolds edged with a row of half-buried bricks |
| bed_cannas | planted/bed | 1.6 | | round bed of red canna lilies, big broad green and bronze leaves, red flowers |
| juniper_spreading | planted/bed | 1.6 | | spreading creeping juniper, a low blue-green mat of feathery sprays |
| hedge_box_short | planted/hedge | 1.6x0.6r0.12 | y | short clipped box hedge, a dense block of tiny dark green leaves |

## sheet planters

| id | folder | size | turns | describe |
|---|---|---|---|---|
| planter_corten_grass | planted/planter | 2.2x0.7 | y | long rusty corten steel trough with tall ornamental grasses |
| planter_concrete_tulips | planted/planter | 2.0x0.8r0.05 | y | long grey precast concrete trough of tulips and white alyssum |
| planter_wood_herbs | planted/planter | 1.6x0.6 | y | wooden slatted trough, rosemary, sage and trailing nasturtiums |
| flower_pyramid | planted/planter | 1.4 | | round three-tier concrete flower stand, rings of red geraniums stepping up to the middle |
| planter_concrete_round | planted/planter | 1.2 | | round grey concrete bowl planter, red geraniums and trailing white flowers |
| planter_concrete_square | planted/planter | 1.2x1.2r0.05 | y | square concrete planter with a rough-cast rim, clipped box ball and pink begonias |
| planter_hexagon_wood | planted/planter | 1.2 | | hexagonal wooden planter, a small clipped shrub and blue lobelia |
| planter_concrete_shrub | planted/planter | 1.0 | | round precast concrete planter with a low spreading green shrub |
| planter_wood_square | planted/planter | 1.0x1.0 | y | square wooden box planter, a rounded flowering hydrangea |
| stone_urn | planted/planter | 1.0 | | classical stone park urn seen from above, a wide round lip, pink and white petunias spilling over |
| tyre_planter | planted/planter | 0.9 | | old tyre painted white and sky blue, cut in a zigzag crown, filled with orange marigolds |
| barrel_planter | planted/planter | 0.8 | | half wooden barrel with iron hoops, purple and white petunias |

## sheet planted_small

| id | folder | size | turns | describe |
|---|---|---|---|---|
| topiary_ball_large | planted/pot | 1.4 | | large clipped box ball, perfectly round, dense tiny dark green leaves |
| pots_mixed_group | planted/pot | 1.2 | | group of four mixed pots, terracotta and glazed blue, geraniums, a fern and a small palm |
| hydrangea_blue | planted/pot | 1.2 | | hydrangea bush, big round blue-mauve flower heads among broad leaves |
| pots_terracotta | planted/pot | 1.0 | | three terracotta pots of different sizes, red geraniums and a herb |
| rose_bush_red | planted/pot | 1.0 | | rose bush, dark green leaves and many deep red roses |
| peony_pink | planted/pot | 1.0 | | peony bush, deeply cut leaves and big frilly pink flowers |
| grass_ornamental | planted/pot | 1.0 | | ornamental grass clump, fine arching striped green and cream blades |
| topiary_ball | planted/pot | 0.9 | | clipped box ball in a square grey pot |
| lavender_clump | planted/pot | 0.9 | | lavender clump, a silvery dome with purple spikes |
| pot_olive | planted/pot | 0.8 | | large terracotta pot with a small olive tree, silvery leaves |
| topiary_cone | planted/pot | 0.8 | | clipped cone of yew seen from above, a round dark green point at the middle, in a black pot |
| hosta_clump | planted/pot | 0.8 | | hosta, broad blue-green ribbed leaves in a rosette |
| tulips_cluster | planted/pot | 0.8 | | small clump of pink and white tulips with broad leaves |
| boxwood_spiral | planted/pot | 0.7 | | spiral clipped box in a dark glazed pot, rings of dark green |

## sheet street_tiny

| id | folder | size | turns | describe |
|---|---|---|---|---|
| bollard_concrete_block | street/barrier | 0.6x0.6r0.05 | | square precast concrete bollard block, chamfered top edges |
| litter_urn_concrete | street/waste | 0.6 | | old Soviet concrete litter urn, a thick round grey rim, dark inside with litter |
| bin_bag | street/waste | 0.6 | | one tied black rubbish bag, shiny creases |
| drinking_fountain | street/utility | 0.6 | | small cast iron drinking fountain, round grey basin with a small spout |
| bollard_concrete_ball | street/barrier | 0.5 | | round concrete ball bollard, light grey, a little grime |
| litter_bin_green | street/waste | 0.5 | | round green steel litter bin with an open top, a black bag lining inside |
| litter_bin_black | street/waste | 0.5 | | round black cast litter bin with a domed lid and a side opening, gold band |
| litter_bin_steel | street/waste | 0.5x0.5r0.08 | y | square stainless steel litter bin with a flat lid, a slot on the front |
| postbox_pillar | street/utility | 0.5x0.4r0.1 | y | blue steel post box, its rounded lid seen from above, a letter slot at the front |
| electric_box_small | street/utility | 0.5x0.3 | y | small grey steel electric meter box on a short post |
| traffic_cone_single | street/barrier | 0.4 | | orange traffic cone seen from above, a square black base, orange cone rings with a white band, the tip at the middle |
| fire_hydrant | street/utility | 0.4 | | red pillar fire hydrant seen from above, a round domed cap and two outlet caps on the sides |
| ashtray_bin | street/waste | 0.35 | | small round steel ashtray bin, a perforated top |
| bollard_cast_black | street/barrier | 0.3 | | black cast iron bollard, a round domed top with a ring |
| bollard_steel | street/barrier | 0.25 | | brushed steel post bollard, a flat round top |
| bollard_striped | street/barrier | 0.25 | | round steel bollard painted red and white, a red top |

## sheet street_waste

| id | folder | size | turns | describe |
|---|---|---|---|---|
| wheelie_bins_row | street/waste | 1.9x0.8r0.05 | y | three wheelie bins side by side, green, blue and grey lids |
| bank_paper | street/waste | 1.6x1.4r0.1 | y | blue paper recycling bank, a flat sloped top with a posting slot at the front |
| dumpster_green | street/waste | 1.4x1.1r0.05 | y | green 1100 litre steel waste container, two closed plastic lids |
| dumpster_open | street/waste | 1.4x1.1r0.05 | y | grey 1100 litre waste container, one lid open showing bags and boxes heaped inside |
| glass_igloo | street/waste | 1.4 | | green glass recycling igloo, a dome with two round bottle holes |
| euro_container | street/waste | 1.3x1.0r0.05 | y | galvanised grey steel waste container, open top, full of mixed rubbish |
| bin_bags_pile | street/waste | 1.2 | | heap of black and blue tied rubbish bags |
| litter_bin_dual | street/waste | 0.9x0.45r0.1 | y | twin litter bin on one post, two round openings, one yellow one grey |
| wheelie_green | street/waste | 0.6x0.75r0.05 | y | green 240 litre wheelie bin, its lid seen from above, hinge at the back, handle at the front |
| wheelie_blue | street/waste | 0.6x0.75r0.05 | y | blue 240 litre wheelie bin, its lid seen from above, hinge at the back |
| wheelie_grey | street/waste | 0.6x0.75r0.05 | y | dark grey 240 litre wheelie bin, its lid seen from above, hinge at the back |
| wheelie_yellow | street/waste | 0.6x0.75r0.05 | y | yellow 240 litre wheelie bin, its lid seen from above, hinge at the back |

## sheet street_works

| id | folder | size | turns | describe |
|---|---|---|---|---|
| barrier_red_white | street/barrier | 2.0x0.5 | y | plastic road barrier, a long board striped red and white on two feet |
| jersey_block | street/barrier | 2.0x0.6r0.05 | y | precast concrete jersey barrier, a narrow flat top along its length, weathered grey |
| sand_pile | street/goods | 1.8 | | heap of yellow building sand, footprints and a spade stuck in it |
| tarp_pile | street/goods | 1.8x1.2r0.1 | y | pile of materials under a blue tarpaulin, roped down, creased |
| wheelbarrow | street/goods | 0.6x1.4r0.1 | y | builder's wheelbarrow, a grey steel tray with cement crust, the wheel at the front |
| water_barrier | street/barrier | 1.2x0.5r0.1 | y | orange plastic water-filled barrier, a ribbed top |
| sandbags | street/barrier | 1.2x0.6r0.1 | y | stack of beige sandbags in two rows |
| cones_three | street/barrier | 1.2 | | three orange traffic cones in a small group |
| cable_drum | street/goods | 1.2 | | wooden cable drum lying flat, a round plank face with a central hole and black cable wound |
| gas_cylinders | street/goods | 1.2x0.6 | y | steel mesh cage holding four red gas cylinders, their round valve tops |
| brick_pallet | street/goods | 1.1x0.9 | y | pallet of red bricks, wrapped in clear film |
| grit_bin | street/utility | 0.9x0.6r0.1 | y | yellow plastic grit bin with a sloped lid |

## sheet street_seats

| id | folder | size | turns | describe |
|---|---|---|---|---|
| parasol_cafe | street/seat | 2.4 | | open square café parasol seen from above, cream canvas with eight seams meeting at the centre |
| picnic_table | street/seat | 1.8x1.6 | y | wooden picnic table with two attached benches, planks running along it |
| sandbox | street/seat | 2.0x2.0 | y | children's sandbox, a square wooden frame around pale sand, a red bucket |
| bench_concrete | street/seat | 2.0x0.6 | y | Soviet park bench, wooden slats on two grey concrete ends |
| bench_slatted_green | street/seat | 1.8x0.6 | y | park bench of green painted wooden slats with a back rail along the back edge, cast iron ends |
| bench_metal | street/seat | 1.8x0.6 | y | modern steel bench, a perforated grey metal seat and back |
| bench_backless | street/seat | 1.6x0.45 | y | backless wooden slat bench on two black steel legs |
| bike_rack_bikes | street/seat | 2.0x1.8 | y | row of steel hoop bike stands with three parked bicycles of different colours |
| bike_rack_empty | street/seat | 2.0x0.5 | y | row of four empty galvanised steel hoop bike stands |
| cafe_table_set | street/seat | 1.4 | | small round café table with two folding chairs |
| chess_table | street/seat | 1.2 | | concrete park table with a chessboard top and four round stools |
| spring_rider | street/seat | 0.9x0.5r0.1 | y | playground spring rider shaped like a horse, red and yellow, on a round base |

## sheet street_stalls

| id | folder | size | turns | describe |
|---|---|---|---|---|
| skip_yellow_rubble | street/goods | 3.6x1.8r0.1 | y | yellow builder's skip full of broken plaster, timber and rubble |
| kvass_barrel | street/utility | 2.4x1.2r0.4 | y | yellow kvass barrel trailer, a long rounded tank with a small tap end at the front |
| transformer_box | street/utility | 2.4x1.8r0.05 | y | small grey electrical transformer kiosk, a flat roof with a vent ridge |
| fruit_stall | street/goods | 1.8x1.0 | y | trestle table of fruit crates, apples, apricots and tomatoes |
| bicycle_parked | street/seat | 1.8x0.6 | y | parked bicycle on its stand, a blue frame, the front wheel to the right |
| motor_scooter | street/seat | 1.8x0.7r0.2 | y | parked motor scooter, a cream body and a black seat, the front wheel to the right |
| flower_stall | street/goods | 1.6x0.8 | y | street flower seller's stand, buckets of roses, peonies and chrysanthemums on a low table |
| advert_column | street/utility | 1.2 | | round advertising column seen from above, a dark green domed cap |
| kick_scooter | street/seat | 1.2x0.5r0.1 | y | rental electric kick scooter on its stand, a lime green deck, the handlebar to the right |
| water_vending | street/utility | 1.0x0.8r0.05 | y | drinking water vending kiosk, a white and blue box with a flat roof |
| utility_cabinet_grey | street/utility | 1.0x0.4 | y | grey steel telecom street cabinet, flat lid |
| utility_cabinet_green | street/utility | 0.8x0.35 | y | dark green steel utility cabinet, flat lid |

## sheet street_goods

| id | folder | size | turns | describe |
|---|---|---|---|---|
| skip_orange_wood | street/goods | 3.2x1.7r0.1 | y | orange builder's skip full of old timber, doors and a broken chair |
| firewood_stack | street/goods | 1.6x0.6 | y | stacked split firewood, log ends in a row |
| watermelon_pile | street/goods | 1.4 | | heap of striped green watermelons on straw, one cut open red |
| pallet_sacks | street/goods | 1.2x1.0 | y | pallet stacked with white cement sacks |
| pallets_stacked | street/goods | 1.2x0.8 | y | stack of wooden pallets, the top deck boards seen from above |
| pallet_empty | street/goods | 1.2x0.8 | y | single weathered wooden pallet, grey boards with gaps |
| crates_wooden | street/goods | 1.2x0.8 | y | four wooden fruit crates, two stacked, empty |
| beer_kegs | street/goods | 1.0 | | four steel beer kegs standing together, round tops with valves |
| crates_milk | street/goods | 1.0x0.7 | y | stacked blue and red plastic bottle crates |
| boxes_cardboard | street/goods | 1.0x0.8 | y | pile of flattened and taped cardboard boxes |
| tyre_stack | street/goods | 0.8 | | stack of old car tyres, the top tyre's ring and tread |
| paint_buckets | street/goods | 0.6 | | three white plastic paint buckets with lids, paint drips |

## shares

How often a look is drawn within its kind, relative to 1 (GEN-6e `share`): by folder, and by id over its folder's.
Common street things — bollards, litter bins, wheelie bins — are drawn often; works, stalls and skips seldom; low
growth often on open ground, junk seldom.

| key | share |
|---|---|
| wild/tree | 1 |
| wild/conifer | 0.8 |
| wild/shrub | 1 |
| wild/growth | 2 |
| wild/rock | 0.6 |
| planted/pot | 0.8 |
| street/barrier | 1.5 |
| street/waste | 1.2 |
| street/utility | 0.8 |
| street/goods | 0.5 |
| dead_snag | 0.3 |
| old_tyre | 0.3 |
| rubble_heap | 0.4 |
| brushwood_pile | 0.5 |
| bollard_steel | 2 |
| bollard_cast_black | 2 |
| bollard_striped | 1.5 |
| bollard_concrete_ball | 1.5 |
| bollard_concrete_block | 1 |
| traffic_cone_single | 0.8 |
| cones_three | 0.4 |
| barrier_red_white | 0.4 |
| water_barrier | 0.4 |
| jersey_block | 0.4 |
| sandbags | 0.3 |
| litter_bin_green | 2 |
| litter_bin_black | 2 |
| litter_urn_concrete | 1.5 |
| wheelie_green | 1.5 |
| wheelie_blue | 1.5 |
| wheelie_grey | 1.5 |
| wheelie_yellow | 1 |
| bin_bag | 0.8 |
| skip_yellow_rubble | 0.2 |
| skip_orange_wood | 0.2 |
| transformer_box | 0.2 |
| kvass_barrel | 0.2 |
| water_vending | 0.3 |
| advert_column | 0.3 |
| fruit_stall | 0.3 |
| flower_stall | 0.3 |
| watermelon_pile | 0.3 |
| sand_pile | 0.3 |
| tarp_pile | 0.3 |
| cable_drum | 0.3 |
| gas_cylinders | 0.2 |
| sandbox | 0.3 |
| spring_rider | 0.3 |
| parasol_cafe | 0.3 |
| cafe_table_set | 0.4 |
| chess_table | 0.3 |
| picnic_table | 0.4 |
| bike_rack_empty | 0.8 |
| bicycle_parked | 0.8 |
| motor_scooter | 0.6 |

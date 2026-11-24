# Diablo’s volcanic chamber

The red gate and the GM command `[go Diablo` arrive on the northwest basalt landing at **6941, 3932 (0)** in Trammel. The bridge leads to Diablo’s platform at **6976, 3932 (0)**. Lava banks and four causeways divide the platform from an outer circuit with four line-of-sight breaks. These views show the entrance and fighting floor at standard ClassicUO zoom. [Entrance](arena-entrance.jpg) · [Diablo, Rhazek and eight skeletons](arena-preview.jpg)

## Artwork

ClassicUO draws the [registered chamber painting](volcanic-chamber.png) once beneath world objects, characters and effects. Its fixed camera projects each tile axis at 22 pixels horizontally and vertically. The source texture uses half that resolution. The original UO Bloody Pentagram consists of 34 components. Its assembled 211 × 169 pixels occupy 844 × 676 pixels on the platform at standard zoom. [Approved concept](proposals/volcanic-chamber.png)

The client build embeds the registered PNG. To regenerate it, place a local 211 × 169 transparent export of the assembled symbol at `art/world/diablo-ritual/uo-bloody-pentagram.png`, then run `python3 tools/register_diablo_chamber.py` with Pillow installed. The tool registers the [ImageGen painting](proposals/volcanic-chamber-base.png) against the bridge banks, platform lips, shelves and wall feet, then adds the symbol.

## Movement and encounter geometry

The server supplies the surfaces used for picking and walking, the lava blockers and the sight-blocking rock shelves. The client uses the same terrain items for movement and positions the painting from their world coordinates. Lava blockers also light the nearby stone and creatures. The tile-based floor assets and their existing section IDs support saved arenas and older clients. [Cave builder](../../../src/Sanctuary.Content/DiabloCave.cs) · [Collision and section data](../../../data/Sanctuary/diablo-ritual.json)

The client draws four small lighting overlays with the chamber painting. Each follows the registered orange channels and catches the edge of the neighbouring basalt. Their phases run independently. After changing the registered painting, run `python3 tools/build_diablo_lava_light.py` to regenerate the four masks. [In-game lighting preview](lava-light.gif)

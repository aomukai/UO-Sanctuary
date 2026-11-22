# Additional NPC portrait atlases

## Portraits for new NPCs

Generate a fresh painting when adding a new NPC with dialogue. Save it as
`portrait-npc-<lowercase enum name>.png` and give the speaker a new
`NpcPortrait` value. Prefer a frontal view with direct eye contact. A subtle
head turn works when the eyes still meet the player's gaze. Reject off-screen
glances. Reuse a portrait only when the speaker is the same
character, such as Iolo at different stages of his story. Ordinary vendor
windows use the Buy and Sell tabs. For a new trainer site, paint a new portrait, add its cell
to the trainer sheets and register the site in
[`trainer-portraits.json`](../../data/Sanctuary/Balance/trainer-portraits.json).
The market catalog checks that every trainer has a distinct portrait ID. Trainer
atlas files use six columns and six rows. Number sheets from zero and append a
sheet by updating `TrainerPortraitSheets` in `PortraitProtocol.cs`.

## Portrait audit

The built-in ImageGen tool painted the new named portraits and trainer candidates.
The shipped files are the standalone `portrait-npc-*.png` paintings and
`portrait-trainers-00.png` through `portrait-trainers-12.png`. Trainer cells
are assigned by permanent market site ID. The first sheet holds the thirteen
New Haven trainers. The remaining sheets cover both market catalogs.

**Shared generation prompt**

> Make a game sprite atlas of independently painted fantasy role-playing game portraits in a precise edge-to-edge grid of equal square cells. Every person is having a conversation with the player, and both eyes meet the camera lens. The irises are visible and looking right at the observer. No eyes looking left, right, up, down, to a distant object, sideways out of frame, or closed. A few subjects can turn their heads subtly, at most fifteen degrees, or relax one shoulder, and still turn their eyes back toward the lens. Show two eyes with readable highlights. Vary shoulder direction, attitude and expressions substantially between neighbors. Face is the focus with close head-and-shoulders cropping. Each face fully inside its cell with breathing room and no overlapping or cut-off parts. No padding outside the grid, borders, gutter, words, labels, lines or frames. Detailed warm hand-painted 1990s CRPG style, practical medieval trade clothes and clean near-dark burnt umber background, soft directional light. Original faces with distinct anatomy, real ages and expressions.

The named batch used a 3×3 grid in this order.

| Row | Left | Center | Right |
| --- | --- | --- | --- |
| 1 | Garran, stocky short-haired miner with soot on his cheek | Bess, sturdy freckled miner with brown hair and plain work clothes | Leofric, square-jawed watch veteran with a short salt-and-pepper beard and ringmail |
| 2 | Aldric, weathered redhead with ginger stubble and a flat nose | Tamsin, guard with tight dark curls and high cheekbones | Edda, long-faced older guard with silver hair and a lopsided smile |
| 3 | Hugh, young sandy-haired guard with a narrow chin | Elara, freckled scholar with long dark hair and plum trim | Gareth, grizzled dark-haired watch captain in a blue cloak and plate |

The New Haven 4×4 batch used the order smith, tailor, bowyer, carpenter,
alchemist, cook, cartographer, scribe, tinker, imbuer, miner, lumberjack,
and fisher. The three extra studies at the end were a weaver, a currier,
and a carpenter. The wider trainer pool used 8×8 sheets of men or women and
6×6 sheets with alternating rows of men and women. The briefs covered capital
workshops, coastal villages, woodland and mountain trades, bookish trades,
frontier artisans, and island settlements. Varied ages, face shapes, complexions,
hair, temperament, and practical fabrics distinguished the subjects.

**Existing character edits**

> Make every visible iris look directly into the camera as if the character were speaking in person. A slight head tilt or subtle three-quarter turn is fine when the eyes meet the lens clearly. Preserve the exact same individuals and equal cell positions. Preserve their proportions, ages, eye colors, individual expressions, distinctive hair and original equipment. Keep the hand-painted CRPG style, warm amber light, dark brown backdrops and existing expressiveness. Preserve the pixies' ears, wings and costume colors and the ratman's muzzle, whiskers and rounded ears. Keep the pixel grid free of text or framing.

The edits updated Elin, Geoffrey, Lord British and Iolo in `portrait-npcs.png`,
Duncan in `portrait-npc-watch.png`, Wolfgang in `portrait-wolfgang.png`,
and Linnet, Alder, Sorrel and Skrik in `portrait-npc-despise.png`.

## Original expansion atlases

Generated with the built-in ImageGen tool. Each atlas maps cells in reading order, from the top left. The shared part of the prompt below preceded each sheet's subject brief. The style reference was `portrait-npcs.png`.

## Shared prompt

Use case: stylized-concept. Create a single square production portrait atlas for original medieval Britannia NPC dialogue. The supplied NPC atlas is a reference ONLY for the existing hand-painted, subtly pixel-textured late-1990s isometric RPG style, for the very tight or mid-tight head-and-shoulders crop, and for the warm brown-black low-detail backs. Every face must be new and clearly distinguishable from all reference faces. Strong portrait lighting with amber rim highlights, distinct warm and cool cloth accents, realistic imperfect faces, varied age, complexion, facial structure, expression and camera turns. Facial features and costume shapes must remain legible as a small 100px game portrait. Layout: EXACT 2-column by 2-row grid, full-bleed quadrants, four equally sized square crops, boundaries at exact 50% vertical and horizontal, no gaps, dividers, frames, lettering, logos, hands or weapons covering the face, no figures crossing between panels. Put one character in each quadrant in the exact order specified below. Keep all four faces away from the quadrant edges and near the top-middle of their respective crops, eyes clearly visible, similar framing and scale in all four.

## portrait-npc-watch.png

Top left: Duncan, male watchtower guard in his early fifties, long narrow weathered ruddy face, light eyes, graying sandy horseshoe moustache and receding close-cropped hair, open steel bascinet with a plain rust-red padded collar. Wearily alert, facing slightly left. Top right: Mara, female watchtower guard around forty, dark olive skin, long straight nose, strong arched brows, black hair with a silver streak under an open, distinctive short brim steel bascinet and a dull teal leather collar. Wry, steady gaze, facing the viewer. Bottom left: a young male ranger with dark brown skin, short tight black curls, lean cheekbones and a moss-green wool cowl pulled back, a little quiver detail behind the shoulder. Quietly watchful in three-quarter view. Bottom right: a mature female elven ranger with pale tan skin, high cheekbones, visible pointed ear, straight dark auburn bob under a weathered leaf-green mantle and a faint old scar above one brow. Focused, looking slightly to the right. Guard armour should look like simple practical ringmail and steel, no medals or noble heraldry.

## portrait-npc-outlaws.png

Top left: a stocky older male road bandit with a broken nose, deeply lined pale skin, shaved head, faint brown stubble and an old patchwork leather and shearling vest, crooked insolent half-smile. Top right: a tall lean female bandit leader with brown skin, hollow angular cheeks, tall braided dark hair tied behind an ochre headband, and red-brown rough cloth, expression focused and judging. Bottom left: a narrow-faced male city thief with olive skin, short black curls and a thin moustache, low slate-blue hood framing the eyes, small sideways grin and a plain brass clasp. Bottom right: a younger female thief with light freckled skin, short cropped copper hair and a crooked front tooth visible in a restrained smile, charcoal scarf and a gray-green leather jacket; furtive look to the left. Clothes are patched and lived-in, with separate silhouettes and proportions per character.

## portrait-npc-fighters.png

Top left: broad older male northern barbarian with copper-tan skin leathery from years in the wind, salt-and-pepper braided beard, shaved sides and long topknot, muted deep-blue wool and brown fur, eyes skeptical rather than angry. Top right: solidly built middle-aged female barbarian with pale wind-reddened skin, a long straw-blonde braid, fur-lined muted red mantle, and a broad, crooked grin. Bottom left: disciplined male warrior from a coastal town, dark skin, broad upper cheeks, closely trimmed greying beard, scar on lip, simple polished plate gorget and a faded burgundy cloak. Bottom right: calm younger female sword fighter with East Asian features, black bangs over a shorn temple, broad rounded face and a mustard cloth under leather-and-iron armour. Slightly upturned, confident gaze. Original grounded medieval-fantasy armour with varied silhouette and age, no giant fantasy pauldrons.

## portrait-npc-scholars.png

Top left: older male mage with brown skin, long gaunt face and gray-black receding wavy hair, tired bright eyes, indigo wool robe and a simple pendant, no hat. Top right: middle-aged elven woman mage with ash-brown skin, visible pointed ears, a swept back silver-black braid and one raised eyebrow, plum robe with subdued geometric gold brocade. Bottom left: retired male field cleric with pale freckled skin, closely cut reddish-gray beard, broad nose, brown eyes and a plain cream linen collar over a brick-red worn habit, expression gentle and alert. Bottom right: older female temple cleric with dark umber skin, quiet deep set eyes under an ivory and sage head wrap, distinctive high forehead and a modest round bronze clasp. Human and elven faces are grounded in character with readable lines and expressions. No modern or real-world religious symbols.

## portrait-npc-harlan.png

Made with built-in ImageGen. The guard was named Harlan in the game.

```text
Create one square dialogue portrait for Sanctuary, an original old-school fantasy RPG. Warden Edric, an older weathered roadside guard and veteran fighter in his late fifties, has multiple distinct healed scars across his brow, cheek and chin, close-cropped grey hair and a greying short beard. He looks directly at the player with alert, tired, level eyes. Near straight-on shoulders-up crop, face centred and taking up most of the square so the eyes and scars read clearly at tiny dialogue size. Worn steel mail and a muted rust-brown leather surcoat, warm amber light, subdued dark olive-brown watchtower interior background. Painterly hand-painted late-1990s fantasy portrait with natural proportions and tactile brushwork, grounded medieval look. No text, frame, watermark, helmet or gruesome wounds.
```

## portrait-npc-despise.png

Built-in ImageGen used `portrait-npc-watch.png` as its style reference for this sheet.

```text
Use case: stylized-concept. Draw a square production dialogue portrait atlas for the original Sanctuary dark medieval fantasy RPG. Reference image is exclusively for the existing painterly late 1990s RPG style, earthy amber directional lighting, subtle pixel texture, warm dark brown backdrop, head-and-shoulders crop. Every face is new. Layout must be exactly 2×2 with equal full-bleed square quadrants, at the precise 50% boundaries, one large head and shoulders per quadrant, no gutters, dividers, frames, lettering, or figures spilling across quadrants. Readable eyes and faces in a 150px UI crop. Pixies are adult tiny winged forest spirits with subtle long pointed ears, organic moth or dragonfly wings softly visible low behind the shoulders, vivid unusual features, earthy fabric and forest ornament, adult mature facial proportions. Top left: clear-eyed adult woman pixie with copper-brown skin, dusty rose hair in a messy knot, gray-violet translucent wings and a yellow-gold mantle, wary and protective. Top right: weathered mature man pixie, umber complexion, clean-shaven, sage wisps of swept hair, narrow angular face and amber translucent wings, patient, teal thistle clasp. Bottom left: older woman pixie with pale freckled skin, slit brows, dark lavender hair, a moss-green wrap and blue-veined wings, serious and kind. Bottom right: friendly adult ratman, unmistakably anthropomorphic rat with long rat muzzle and prominent rounded ragged ears, wiry brown-and-gray fur, dark amber eyes and visible whiskers, plain frayed russet leather scarf, reserved and worried, composed and intelligent. Keep close portrait framing and eye-level composition varied across subjects, realistic carved facial structure, substantially different silhouettes and hair, no weapons, text, watermark or borders.
```

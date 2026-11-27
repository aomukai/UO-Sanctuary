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

### Fjellheim Thing guards

Built-in ImageGen painted [Sten](portrait-npc-sten.png) and [Ketil](portrait-npc-ketil.png) individually.

> Paint one square dialogue portrait for Sanctuary, in grounded late-1990s fantasy RPG oil and gouache. One adult Ostman guard at the entrance to a snowy Norse assembly hall. Tight head-and-shoulders crop, both eyes meet the player's gaze, subdued brown timber backdrop, soft amber sidelight, natural features, readable at 100 pixels. Worn ringmail, fur cape and wool. No lettering, frame, helmets or modern details. Sten, an older broad-faced man with cool grey eyes, cropped iron-grey hair and a square grey beard. His expression is steady, watchful, unyielding.

> Paint one square dialogue portrait for Sanctuary, in grounded late-1990s fantasy RPG oil and gouache. One adult Ostman guard at the entrance to a snowy Norse assembly hall. Tight head-and-shoulders crop, both eyes meet the player's gaze, subdued brown timber backdrop, soft amber sidelight, natural features, readable at 100 pixels. Worn ringmail, fur cape and wool. No lettering, frame, helmets or modern details. Ketil, a lean man in his thirties with a long freckled face, green eyes, wavy auburn hair, a close russet beard, and a faded blue wool collar. Attentive, candid expression.

### Shame’s pirate captains

Built-in ImageGen painted two original square dialogue portraits with direct eye contact, a tight head-and-shoulders crop, a dark cave backdrop, and cool spectral light around the silhouette.

- `portrait-npc-shameghostcaptain.png` depicts the castaway captain in a faded teal coat and headscarf, with a salt-and-pepper beard and a crooked grin.
- `portrait-npc-shamefleetcaptain.png` depicts the fleet captain in a weathered charcoal hat and burgundy coat, with hollow cheeks and a defiant stare.


### Jens Won

The built-in ImageGen tool used `portrait-npc-harlan.png` as the painting-style reference. The shipped portrait is `portrait-npc-jenswon.png`.

> Use case: stylized-concept. Create one new square full-bleed dialogue portrait for Sanctuary, a grounded medieval dark fantasy CRPG. Input image is a STYLE and crop reference only: retain its tactile hand-painted oil/gouache brushwork, slightly grainy late-1990s CRPG portrait finish, human proportions, earthy warm shadows, detailed direct eye contact, and tight shoulders-up scale. Do not depict the reference guard or any armour. Subject: Jens Won, a medieval shovel merchant and an affectionate satirical medieval reimagining of Jensen Huang, the recognizable contemporary Taiwanese-American public figure. Capture Jensen Huang's recognizable swept-back silver-and-black hair, rounded distinct eyeglass frames (medieval iron spectacles), clean shaven face, recognizable expressive cheeks and warm confident slightly mischievous smile. His signature dark silhouette is rendered as a practical black leather medieval merchant's doublet over dark linen with a simple clasp, muted forest green thread at the collar. Face the player, with both eyes visibly meeting the viewer's gaze even if the shoulders are turned. Tight centered head and shoulders, entire hairline inside frame, clear readable silhouette at 100 pixels. Amber sidelight, muted umber graveyard-road stone backdrop, soft low-detail. One person, one portrait. No text, frame, modern branding, logos, modern zipper, watermark, shovel in the crop, glamour photo or caricature distortion.

### Named portraits and trainers

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

## Heartwood trainer portraits

Built-in ImageGen generated these individually with `portrait-npc-nightwind.png` as the style reference.
Caelira (710, row 4 column 5) and Orren (711, row 4 column 6) are on `portrait-trainers-12.png`.

```text
Use case: stylized-concept. Create one new square, full-bleed head-and-shoulders character portrait for Sanctuary’s dialogue window. Use the existing Nightwind image only for the painterly late-1990s fantasy RPG style, earthy warm directional light, surface texture, and tight crop. The character is a distinct original Heartwood elf. Both fully visible eyes look directly at the viewer. Center the face, keep the hairline inside frame, and keep the silhouette and expression readable at 150px. No border, text, weapon, watermark, or photorealistic finish.

Caelira: a mature elven bowcraft trainer with tawny brown skin, long ash-silver hair tied back, large pointed ears, broad cheekbones, level amber eyes, and a quick, knowing half-smile. A moss-blue linen collar and simple leather bowyer’s apron. Shadowed wooden workshop behind her.

Orren: a middle-aged elven carpentry trainer with walnut-brown skin, narrow face and a prominent nose, dark cropped curls, pointed ears and a warm patient gaze. A muted umber apron and flax linen shirt. The low-detail background is a warmly lit timber workshop.
```

## Wind trainer portraits

The thirteen Trammel trainers use portraits 737–749 in `portrait-trainers-13.png`, in order from smith through fisher. Built-in ImageGen painted them as a 4×4 sheet.

> Create a tight head-and-shoulders portrait atlas of adult gargoyle craftsmen and scholars living in a mountain cavern city. Give them angular ears, stone-like copper, terracotta, ochre or slate complexions, and varied horns, faces, ages and expressions. Both eyes meet the player’s gaze. Use practical cloth and leather, warm golden side lighting, shadowed workshops and a textured late-1990s RPG oil and gouache style. Draw sixteen independent square portraits in a precise 4×4 grid without margins, dividers or lettering. Keep horn tips and jawlines within each cell so the faces read at dialogue size.

## Island ferry skippers

Built-in ImageGen painted original square head-and-shoulders portraits with direct eye contact, weathered medieval clothing, and late-1990s RPG oil and gouache texture.

- `portrait-npc-fireislandskipper.png` shows Nessa Vale, a middle-aged skipper with a windblown dark bob and a quiet smile. She wears a worn leather vest over sea-blue linen, with warm lantern light and a volcanic coastline behind her.
- `portrait-npc-daggerisleskipper.png` shows Halden Rook, an older skipper with wavy grey hair, a salt-and-pepper beard, and a dark wool cap. He wears a heavy grey-green cloak with a shearling collar at a cold northern wharf.

## portrait-npc-despise.png

Built-in ImageGen used `portrait-npc-watch.png` as its style reference for this sheet.

```text
Use case: stylized-concept. Draw a square production dialogue portrait atlas for the original Sanctuary dark medieval fantasy RPG. Reference image is exclusively for the existing painterly late 1990s RPG style, earthy amber directional lighting, subtle pixel texture, warm dark brown backdrop, head-and-shoulders crop. Every face is new. Layout must be exactly 2×2 with equal full-bleed square quadrants, at the precise 50% boundaries, one large head and shoulders per quadrant, no gutters, dividers, frames, lettering, or figures spilling across quadrants. Readable eyes and faces in a 150px UI crop. Pixies are adult tiny winged forest spirits with subtle long pointed ears, organic moth or dragonfly wings softly visible low behind the shoulders, vivid unusual features, earthy fabric and forest ornament, adult mature facial proportions. Top left: clear-eyed adult woman pixie with copper-brown skin, dusty rose hair in a messy knot, gray-violet translucent wings and a yellow-gold mantle, wary and protective. Top right: weathered mature man pixie, umber complexion, clean-shaven, sage wisps of swept hair, narrow angular face and amber translucent wings, patient, teal thistle clasp. Bottom left: older woman pixie with pale freckled skin, slit brows, dark lavender hair, a moss-green wrap and blue-veined wings, serious and kind. Bottom right: friendly adult ratman, unmistakably anthropomorphic rat with long rat muzzle and prominent rounded ragged ears, wiry brown-and-gray fur, dark amber eyes and visible whiskers, plain frayed russet leather scarf, reserved and worried, composed and intelligent. Keep close portrait framing and eye-level composition varied across subjects, realistic carved facial structure, substantially different silhouettes and hair, no weapons, text, watermark or borders.
```

## Britannia campaign portraits

Built-in ImageGen generated each painting individually. The style reference was the original Geoffrey portrait cropped from `portrait-npcs.png`. The three Geoffrey scenes also use it as their character reference.

The shared part of each prompt was:

> Create a single square head-and-shoulders painting for Sanctuary, a grounded medieval dark fantasy RPG dialogue window. Both eyes look at the viewer. Tight centered shoulders-up crop, face readable when reduced to 150px, amber sidelight, deep low-detail background, late 1990s painterly CRPG style, no frame, text, watermark, weapons, or contemporary items.

### `portrait-npc-geoffreydishevelled.png`

> Use the reference to preserve this exact captain's recognizable facial structure, scar, skin tone, rusty red cloak and worn steel helmet or chain mail. The city guard captain has been working nights. His helmet is off, revealing tousled dark brown hair, and he wears his chain mail. Unshaven jaw, tense brow and tired but urgent expression, a little road dust, the same man at the same age.

### `portrait-npc-geoffreyexhausted.png`

> Use the reference to preserve this exact captain's recognizable facial structure, scar, skin tone, rusty red cloak and worn steel helmet or chain mail. The city guard captain is prematurely aged by the crisis. No helmet, grey streaks through his untidy hair, deeper lines and sunken, tired eyes, scuffed chain mail and a faded red cloak. He is quietly exhausted and looks straight into the viewer's eyes.

### `portrait-npc-geoffreyrelieved.png`

> Use the reference to preserve this exact captain's recognizable facial structure, scar, skin tone, rusty red cloak and worn steel helmet or chain mail. Same helmet, armor, red cloak, scar and face as reference. He gives the player a broad genuine relieved smile for the first time, crinkling around both eyes. Warm light on his face, recognizably the same man.

### `portrait-npc-britainharbourmaster.png`

> The input is a style reference only. Paint a distinct original face and clothing. Elowen Marr, a weathered middle-aged woman running the Britain docks. Tawny skin, a strong broad face, sun lines, straight dark hair with silver temples tied under a navy linen scarf, cream shirt and dark teal seafarer's vest. Calm practical regard. Shadowy timber harbour office.

### `portrait-npc-blackthorn.png`

> The input is a style reference only. Paint a distinct original face and clothing. A grave, aristocratic dark-haired medieval regent, around fifty years old. Olive skin, angular face, short neatly cut dark beard with silver through the chin, black wool court coat with subtle burgundy piping, plain garnet clasp. Measured concern and restrained fatigue. Dim stone stronghold.

### `portrait-npc-dupre.png`

> The input is a style reference only. Paint a distinct original face and clothing. An affable veteran knight with light freckled skin, wavy light-brown hair, a close russet beard, warm grey eyes and a strong rounded face. Scuffed breastplate over a cream gambeson, plain muted blue cloak, open earnest expression. Warm lamplit wood-and-plaster tavern.

### `portrait-npc-shamino.png`

> The input is a style reference only. Paint a distinct original face. Shamino, a seasoned, rangy wilderness scout in his forties with pale grey-green eyes, ruddy complexion, high cheekbones, tied-back chestnut hair graying at the temples, and a short rough beard. He wears a dark sage-green wool hood down on his shoulders, weathered leather collar and small bronze leaf clasp. Quiet curiosity and confidence, as though he has just noticed a trail that the others overlooked. Warm dim stone keep in the background.

### Destard's dragon court

Vaelith and Rhazek were generated with the built-in ImageGen tool using [Jaana’s portrait](portrait-jaana.png) as the painting-style reference. The guards and servant used [Vaelith’s portrait](portrait-npc-destardqueen.png) as their painting-style reference. All five look directly at the player.

**Shared prompt.** Square individual dialogue portrait for Sanctuary, stylized-concept. True non-human European dragon head and neck, dangerous, powerful and weathered. Eyes meet the viewer directly. Tight face portrait with the major features inside the center 80 percent of the square, readable at 100 px. Hand-painted textured late-1990s fantasy RPG portrait like the reference in brushwork and small-size clarity only. Warm amber raking light against a dark simple cavern background. No border, text or other character.

- **Vaelith.** Ancient female dragon queen, long regal narrow angular reptilian face, swept-back imposing horns, direct appraising gaze. Restrained copper-red scale tones and elegant crownlike anatomy. [Queen portrait](portrait-npc-destardqueen.png)
- **Rhazek.** Ancient male dragon consort, broad square reptilian jaw, battered asymmetrical horns with one broken tip, old visible facial scars, direct disdainful scowl. Charcoal-bronze scales. [Consort portrait](portrait-npc-destardconsort.png)
- **Western royal guard.** Stern rust-brown dragon, blunt muzzle, slate brow, outward horns and an iron-and-bronze gorget. [Western guard portrait](portrait-npc-destardguardwest.png)
- **Northern royal guard.** Angular slate-green dragon, upright ivory horns and the same iron-and-bronze gorget. [Northern guard portrait](portrait-npc-destardguardnorth.png)
- **Royal servant.** Older pale-bronze dragon, low horns, grey-green eyes and a burgundy cord. Weary and attentive. [Servant portrait](portrait-npc-destardservant.png)


### Archbishop Lazarus

A severe, aging human archbishop in dark wine-red vestments with tarnished gold embroidery. Deep-set eyes meet the Avatar’s gaze. Warm light falls across his face in a dim cathedral undercroft. The portrait accompanies his greeting at the sacrificial altar below Deceit. [Lazarus portrait](portrait-npc-lazarus.png)

### Celeste

The built-in ImageGen tool painted [Celeste in Covetous](portrait-npc-celeste.png) and [Celeste with her gear](portrait-npc-celestearmored.png). The first portrait is the identity reference for the second. The armored portrait starts with the gear-recovery dialogue.

> Use case: stylized-concept. Create one original square full-bleed dialogue portrait of Celeste, an adult woman paladin held captive in a medieval dungeon, for Sanctuary’s grounded dark fantasy RPG. She has dark auburn hair drawn back in an untidy, practical braid, a weathered lightly freckled face and steady gray eyes with a tired, resolute expression. Her armor has been taken. Dress her in a plain worn gray linen gambeson with a muted blue collar. Both eyes fully visible, looking directly into the player's eyes, slight turn of shoulders allowed. Tight centered head and shoulders, entire hairline in frame, face readable at 150 pixels. Hand-painted late-1990s CRPG oil and gouache brushwork, natural proportions, warm amber sidelight, and dark low-detail brown dungeon stone. No border, text, weapon, shield, watermark, blood or photorealism.

> Use case: identity-preserve. Paint the post-recovery version of this exact square Sanctuary dialogue portrait. Preserve Celeste’s recognizable face, gray eyes meeting the player, lightly freckled complexion, auburn braid, calm resolute expression, head-and-shoulders scale, painterly late-1990s CRPG brushwork, amber sidelight and dim dungeon stone. Replace the prison gambeson with her complete practical medieval paladin kit, with a visible fitted steel ringmail hauberk, mail at the shoulders, a restrained steel gorget and muted blue textile accents. A coif can lie down on her shoulders so the face and auburn hair remain recognizable. Show the upper edge of her father’s worn Order Shield low in the crop, with a restrained blue and silver heraldic emblem, with clear unobstructed eyes. Square full bleed. No frame, text, watermark or modern elements.

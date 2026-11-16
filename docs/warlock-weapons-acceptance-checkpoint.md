# Warlock weapon, Hex and Ward acceptance work

Reference: **D2R Reign of the Warlock 3.3.93847**, approved Sanctuary deviations first. Charms and runewords are excluded. This is the second Warlock group; the accepted [demon/foundation group](warlock-demons-acceptance-checkpoint.md) covers the first seven implemented routes.

The [driver](../tests/check_warlock_weapons_acceptance.py) composes existing controls for nine routes: Hex: Bane, Cleave, Echoing Strike, Hex: Purge, Blade Warp, Psychic Ward, Eldritch Blast, Hex: Siphon and Mirrored Blades. It retains the existing shared Magic Arrow weapon/missile regression, rather than claiming that regression is a new Warlock route.

Declared scope: **21 families, seven connected asynchronous families, 24 current input commands, four cached reference files, 240 expected markers, 93 expected connections and three saves/two reloads**. Cleave's right-input ground flag and original pre-save pending-action control remain. The seven asynchronous families retain their separate setup/result waits. Expected totals are not passing results until the actual job succeeds.

The candidate copies the accepted demon group's unchanged C1/I15 artifacts and runtime data. `artifact-reuse.json` binds the parent receipt and native-input hashes; source snapshot 1 binds current C#/project/data. No source/build/gameplay/kernel change or new reference compilation occurs. Existing socket pacing preserves native security.

The original weapon/Hex group replacements, Bane mastery/event behavior, Purge reaction, Ward absorption/stun, Blade Warp adaptation, Cleave fan/payment, Echo outward/return and independent hit delay, Mirrored multi-weapon contacts and once-only mana/ammunition, and Eldritch periodic Hex/recovery rules remain their approved component contracts. Original actors, items, wounds/resources, captured packets/RNG, buffers, pending-action cancellation and no replay remain asserted. Existing inspected Hex states already use the established hold interval; natural tests keep their own timing.

The ten Sigil/Chaos routes remain next. Blood Oath, Blood Boil, Engorge and Consume retain their frozen proposals without inferred approval or a repeated question. Unsupported current-engine, native/item/recipient/terrain/PvP and graphical-client boundaries remain explicit. Final effects/audio and tree/hotkey icons remain separate identified placeholders. Installed runtime/saves/configuration/client protection is still required; no deployment or artwork generation is included.

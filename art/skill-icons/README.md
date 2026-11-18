# Sanctuary skill icons

The talent tree, mouse binding menu, quick assignment roster and hotbar share one graphic ID per skill. The 44-pixel tiles use the sepia ink, parchment and simple frames of the UO spellbooks. ClassicUO scales the tiles to fit binding slots.

`manifest.json` maps talents either to native UO graphic IDs or to cells in the ImageGen source atlases. The native artwork comes from the player's installed client data. The client build packages only Sanctuary's custom gumps.

After editing the manifest or replacing a source atlas, run the following from the repository root. The first command requires Pillow.

```sh
python3 tools/build_skill_icons.py
./dev build client
./dev build content
```

`python3 tools/build_skill_icons.py --check` verifies the compiled tiles and shared ID map. The [ImageGen prompts](imagegen-prompts.md) record how the source atlases were made.

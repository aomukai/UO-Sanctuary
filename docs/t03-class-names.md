# T03 — cosmetic class names

Started 19 October 2026 (JST). Scope: use the [confirmed sex-specific display names](class-talents-and-trades.md#class-names-and-character-sex--design-update-19-october-2026) for the existing seven identities, without changing wire/save IDs, talents, XP, attributes, equipment rules or action timing.

Acceptance checks:

- Use one shared name mapping for creation choices/appearance summaries, paperdoll progression, talent headings and other player-facing class labels.
- Before appearance sex is selected, show both aliases; update the appearance summary when sex changes and preserve it across Back/reselection.
- Check both sexes for all seven real creation paths, level-up, talent allocation, saved progression and reload. Legacy class IDs and talent keys remain unchanged.
- Exercise the actual client creation controls and paperdoll/talent labels; keep native edits reproducible as a new client patch.
- Preserve the live installation, accounts, saves and client preferences. No deployment is included. The planned wardrobe UI does not yet exist; it must use the same shared mapping when its card is implemented.

Status: **implemented and verified; not deployed**.

## Result and boundaries

[The shared mapping](../shared/DiabloClass.cs) supplies `DisplayName(class, female)` and paired `SelectionName(class)` captions. IDs remain **101–107**, with the same seven enum identities and unchanged talent IDs. Male/female labels are Gargarean/Amazon, Sorcerer/Sorceress and Druid/Druidess; Necromancer, Paladin, Barbarian and Assassin remain unisex.

The client picker shows both aliases because appearance comes next. The selected summary follows the native Male/Female controls and remains correct after Back/reselection. The server uses the same helper for initial/level-up/reloaded paperdoll titles, talent headings and equipped weapon-speed tooltips. Paperdoll line wrapping now recognizes all aliases rather than parsing only enum names. The four client edits are reproduced by [patch 0020](../patches/ClassicUO/0020-sex-specific-class-display-names.patch).

No new class, class-specific sex restriction, character body rule, earned title, wardrobe transaction, protocol version or save field was added. The existing native sex flag remains the source of the display variant. The planned wardrobe has no UI yet; its class rows must use `DiabloClasses.DisplayName(identity, owner.Female)` under T15/T40. Wardrobe selection/progression is not claimed implemented here.

## Verification

The [server fixture](../tests/Sanctuary.Integration/ClassNameChecks.cs) passed **14 real character-creation event paths** (seven classes × both sexes). It verifies initial titles, unchanged wire identity, earned XP/level, talent ownership/keys, spent attributes and point budgets, worn item identity, identical stats/basic-attack timing between the two sexes, weapon-speed labels and the actual compiled talent-gump strings. Those profiles/ranks/allocations/stats/timing/names survived a native version-2 save/reload. Existing creation/gear/origin checks and version-1/version-0 profile migration also passed over four boots.

The [actual-client exercise](../tests/check_class_names_ui.py) clicked all seven class choices, both native sex controls and Back/reselection. Screenshots of all **14 summaries** and preserved selections were visually checked, along with the paired picker captions. Two complete GUI world-entry runs submitted **male Sorcerer** and **female Druidess** through the normal creation packet. The server verified the submitted ID/sex/title and open talent gump; paperdoll two-line layout and matching talent headings were visually checked. Each GUI run also completed four disposable save/shutdown/restart boots. The GUI exercise did not create fourteen online characters or test a wardrobe. These are automated client observations, not new player feedback or a broad combat-balance play pass.

Builds/checks:

- Content and integration builds passed with **zero warnings/errors**.
- The client build passed with **59 existing upstream warnings**, including the retained ImageSharp advisories; no dependency update was attempted.
- All **12 Python checks** passed. The current T02 audit was refreshed for the new content/source hashes: exact affix pools, effect codes and material observations remain unchanged. Its 24 queries/1,536 validated seeded rolls passed again.
- Fresh/reverse/repeated patch reproduction passed: **13 ModernUO patches / 29 source paths**, **20 ClassicUO patches / 35 source paths**.
- Documentation link/card/whitespace checks and `git diff --check` passed.

From the repository root, the non-staging build commands are:

```bash
./dev build content
.tools/dotnet/dotnet build upstream/ClassicUO/src/ClassicUO.Client/ClassicUO.Client.csproj -c Debug --nologo -p:PublishAot=false
.tools/dotnet/dotnet build tests/Sanctuary.Integration/Sanctuary.Integration.csproj -c Debug -o .local/integration --nologo
```

The actual client build was run with the ClassicUO checkout as its working directory. For server coverage:

```bash
SANCTUARY_TEST_SERVER="$PWD/upstream/ModernUO/Distribution" \
SANCTUARY_TEST_CREATION=1 SANCTUARY_TEST_CLASS_NAMES=1 \
SANCTUARY_TEST_FIRST_LOOP=1 SANCTUARY_FIRST_LOOP_ROOT="$PWD" \
SANCTUARY_FIRST_LOOP_AUDIT="$PWD/tools/reference/first-loop-affix-audit.json" \
python3 -u tests/check_britain.py
python3 tools/build_first_loop_catalogue.py --source-dir .local/research/foundation
python3 -m unittest discover -s tests -p 'test_*.py' -v
```

For each GUI run, use `SANCTUARY_TEST_SERVER="$PWD/upstream/ModernUO/Distribution" SANCTUARY_TEST_CREATION=1 SANCTUARY_TEST_UI=1 SANCTUARY_TEST_CLASS_NAMES_UI=1 python3 -u tests/check_britain.py`; add `SANCTUARY_TEST_CLASS_NAMES_FEMALE=1` for the Druidess run. The default test client is the freshly built upstream output, with installed artwork/native libraries and temporary settings/profiles. Run harnesses sequentially; retain their shared logs after each run.

Supporting evidence is under `.local/logs/t03-class-names-20261019/`: build logs, `server-summary.log`/`server.log`, male/female UI summaries/server/client logs, `python-tests.log`, `patch-reproduction.log`, before/after preservation manifests, and the screenshots including `creation-name-contact-sheet.png`, `t03-class-choices.png`, `t03-paperdoll-*.png` and `t03-talents-*.png`.

## Installation and completion record

The built content SHA-256 is `cf3fd620721683889338c348d5f89034cc6c51bd70ed2d28a62cc28f39a03279`; the built client `cuo.dll` is `16a2578eff83fa0e34cb9e4cc22f7d7f04c399c9ee6c83cdc0626a77f18b8a2c`. These outputs are **not the installed comparison binaries**. A later deployment should install the matching content/client pair through the roadmap's save/backup/stop/install/restart workflow, preserving native libraries and client preferences. Never install the integration assembly.

Of **203 existing source/data/runtime/protected files** recorded at entry, only the four intended production source/shared files changed: `DiabloClass.cs`, `CharacterProfiles.cs`, `TalentGump.cs` and the weapon-tooltip label in `ActionTiming.cs`. All installed binaries, saved `.bin` payloads, configuration and client settings/profile files stayed byte-identical. The live server remained PID **348733**, and the desktop client was left closed. Native autosave index timestamps are outside this payload comparison; no live console mutation or installation was performed.

T01 is the verified prerequisite; the confirmed class-name table supplies the only design choice needed. Changes comprise those four source/shared files, four reproducible client edits/new patch, the class-name fixtures/UI harness switches, T02 provenance refresh and documentation. Character save version **2** and native serialization are unchanged; no migration is required. Existing class titles derive their cosmetic aliases on load. A rollback uses the prior matching content/client binaries and their existing title derivation; no new save format is introduced.

T03 is complete for all current presentation surfaces. The future wardrobe must adopt the shared helper when implemented. The next recommended card is **T04 — persisted item state for the bounded cohort**.

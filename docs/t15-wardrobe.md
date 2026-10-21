# T15 — public craft switching

T40 now extends this original craft-only slice to all seven classes, real equipment storage/retrieval and class summon parking. See [the complete wardrobe transaction](t40-wardrobe-transactions.md). The T15 scope and validation evidence below describe the original slice.

T15 completes the first earned wardrobe loop. Either ceremony grants permanent character-owned access to free craft switching at all22 registered public wardrobes. Double-click a wardrobe after recognition, or use the recognition screen's wardrobe button. The selector preselects the current class and craft. Native radio groups keep one of each selected; the server rejects missing, duplicate, extra or unknown selections. Only the current combat class is selectable. Independent class histories, other class activation and class equipment/summon storage remain T39/T40.

Selecting another craft activates its native tool/action/gain/training guards immediately. All native skill values, caps, locks and aggregate cap remain intact, including inactive crafts and the three always-active gathering skills. Cartography is one of the ten crafts. An eligible character whose first craft remains unconfirmed can confirm the preselected Alchemy or choose another craft through the wardrobe. An unchanged confirmed selection is a no-op. Every successful submission closes the selector. There is no fee or new switching cooldown.

The server validates the same character/connection, one-use session, active class/level/XP snapshot, profession identity and physical source again at submission. Public sources must be the registered saved wardrobe at its own hall; remote, moved, forged and carried furniture sources fail. The recipient's already-placed wardrobe in an owned house uses the same guard; T42 still owns actual housing purchase/placement.

Access requires an alive, unfrozen, unparalyzed character with a backpack and no held item. Combat/action detection is shared with T14 respec: no native combatant, native cast or pending target; no class combat target, cast target, held input, queued ability, unexpired action deadline or hit recovery; no fighting live follower; no aggression as attacker or defender in the last10seconds. Native crafting must finish (`CraftSystem` action lock), and native skill/spell deadlines must expire. The operation does not cancel actions or clear any deadline.

A craft-only change writes only the confirmed active profession and invalidates pending first-choice/trainer quotes. Old native crafting tickets cannot complete after switching away and back, even if the recipe, materials and source are otherwise still valid. Existing trained skills and all character class/stat/talent/binding state remain untouched. Equipped items and summons stay in place. Life/mana/stamina are not refilled, effects are not cleansed and timers are not reset.

The second ceremony still unlocks only active-class respec for5,000gold. Its confirmation is separate from Swap. Successful respec closes recognition, selector and confirmation and invalidates every pending wardrobe session. Reopening is required before any later switch. There is no inactive-template reset or combined reset-and-switch path.

Persistence reuses `SanctuaryProfessions` version0; no migration rewrites skills or creates additional class profiles. The existing profession release flag also controls wardrobe switching. The installed runtime already enables the agreed profession/crafting features; T15 provides their earned public switching route.

## Verification

Server integration checks cover both first-ceremony routes, unrecognized/dead/frozen/paralyzed/remote/forged/portable sources, all ten active craft choices, all22 public wardrobes, unchanged selection, initial Alchemy confirmation, pending target/skill/spell/craft/attack/recovery, fighting followers, attacker/defender aggression, class/gathering selection rejection, stale/reconnect sessions and paid-respec invalidation. Switches preserve native skills/caps/locks, class level/XP/allocations, raw attributes, current pools, gear, summons, action/skill/spell deadlines and backpack/bank gold. Stale trainer/crafting requests fail without spending resources after a switch roundtrip. The saved active craft, training and equipment survive four boots.

Actual ClassicUO checks exercise the preselected selector, unchanged selection, malformed and replaced responses, moving-away rejection, an Alchemy→Tinkering→Alchemy roundtrip, the second ceremony and its paid reset. Screenshots are retained locally as `t15-*.png`.

Status: implemented, tested and deployed20 October2026 (JST). The second live boot remains running; the client is closed.

## Deployment evidence

The source and installed four-boot cumulative suites pass wardrobe switching, recognition/respec, death gold, repairs, town markets, reward lives, effective gear, tool/equipment crafting and material processing. Project content and integration builds have zero warnings/errors. All16 Python checks pass; the authoring audit retains24 pools and1536 sampled rolls with the new content artifact hash. T15 requires no native engine/client changes or new patch; the existing24 ModernUO/22 ClassicUO patch stacks remain intact.

The live world was saved and stopped cleanly before a full server/client backup at `.local/backups/pre-t15-20261020-031851`. All60 protected payloads, including recognition persistence, were byte-identical across installation. The matching tested content is installed, with only UOContent.dll and Sanctuary.Content.dll registered in production. Engine/logger/client binaries are unchanged, and no integration assembly or fixtures were installed.

Both live boots/save pass with zero added service objects:22 civic offices and476 town merchants retain their existing identities. Pre/staged/post-live copied-world comparisons preserve both characters, allocations/raw attributes/native skills/caps/locks/recognition, gear/materials and485 gold-stack identities/amounts/parents/origins. All34 non-save configuration/client/assembly-list payloads, including13 client preference files, keep their hashes. Full rollback and artifact/preservation records are in `.local/logs/deploy-t15/deployment.json`; source/client evidence is in `.local/logs/t15/`. Source changes remain local for the next requested Git checkpoint. T16 is next.

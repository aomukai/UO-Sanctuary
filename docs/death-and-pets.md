# Progression, death and skeleton guardians

The first combat progression loop is playable: graveyard kills award experience, levels grant points, the Class window spends skill points, the expanded status window spends stat points, and implemented abilities can be assigned to either mouse button.

## Play the loop

1. Kill graveyard skeletons (34 base XP) or zombies (33 base XP), using the [early D2 Normal baseline](early-normal-monsters.md). The lich is out of the test spawn until world building. Level 2 requires 500 cumulative XP. Revived enemies never award a second set of rewards. A guardian's kills credit its owner. Party sharing and monster/player level XP scaling are future work.
2. Open **Class**, select **Summoning Spells → Raise Skeleton**, and click **Learn**. Each level after the first grants one skill point and five stat points. Click a stat in the left column of the expanded status window to spend a stat point.
3. Click **Mouse skills** in Class (or use `[Bindings`). Assign Basic attack, Teeth, Raise Skeleton, or None to Left or Right. Unimplemented skills cannot be assigned. Teeth requires a learned rank or an equipped Necromancer dagger. Assignments are saved independently of temporary equipment availability.
4. Cast Raise Skeleton near a monster corpse. A usable corpse under the cursor takes priority; otherwise the server chooses the nearest usable corpse within eight tiles. The cast uses the class FCR timeline and validates the same body again when the cast finishes. Failed casts do not spend mana or consume a body.
5. Open **Class → Pets**, use `[Pets`, or double-click your guardian to inspect its current life, level, damage, attack rating, defense, resistances, attack interval and movement interval. Refresh updates the values.

For a hotkey, make a ClassicUO macro with **Say** text `[Cast RaiseSkeleton` and assign the desired key. The client turns that exact macro into a skill request, including the hovered corpse when present; it does not broadcast speech. Drag the macro's name from the Macros options into the world to create ClassicUO's native macro button, which can be grouped with other shortcut buttons. Teeth and basic attacks also accept Say macros `[Cast Teeth` and `[Cast BasicAttack`. A shortcut button uses nearest-corpse targeting when the cursor is over the UI.

Learning and assignment are server validated. Holding a mouse button cannot bypass casting speed. A level-up restores life, mana and stamina, and displays the earned point counts. Cumulative experience and all spent/unspent progression survive saves; there is no death XP loss. The original character profile remains format 2; mouse bindings use their own versioned `SanctuaryBindings` persistence file.

## Approved facet scaling

Persistent skeletons, skeletal mages and clay/blood/fire golems now rescale to their owner’s designated facet difficulty while preserving current life percentage, including parked summons. Nightmare/Hell facet assignments are still to be designated, so shipped facets retain Normal. Other summon adapters remain pending. See [the shared policy and verification](summon-facet-difficulty.md).

## Guardians

Skeletons use the native **Guard me** standing order, following the owner, engaging attackers and returning after combat. Spoken pet commands and command menus are disabled. They cannot attack players, friendly NPCs or controlled pets. Ordinary left-clicking the ground still moves; hold Shift to use a left-bound skill in place.

Class players, their guardians and other class-player pets cannot damage one another through the combat actions, physical/spell damage handler, or direct player/guardian damage paths. Teeth ignores friendly pets when finding its first hostile ray intersection.

A rank-one skeleton raised by a level-two caster has 21 life, 1–2 physical damage, 32 attack rating and 32 defense. The project keeps the caster's level at raising. Skill ranks are captured when raised; learning more ranks requires raising new skeletons to benefit.

The legacy Diablo II expansion Normal formulas use `r = Raise Skeleton rank`, `m = Skeleton Mastery rank`, and `b = max(0, r - 3)`:

- Life: `floor((21 + 8*m) * (1 + 0.5*b))`. Mastery is multiplied with base life. Ranks 20/20 give 1,719 life, versus the former incorrect 359.
- Attack rating: `5 + Normal MonLvl L-TH[level] + 15*(r+m)`. Defense: `5 + 6*level + 15*(r+m)`. Level-based additions and Mastery's attack/defense bonus were previously missing.
- Damage: add `max(0, min(r,16)-8) + 2*max(0,r-16) + 2*m` to base 1–2, then multiply by `1 + 0.07*b`, rounding down. Ranks 20/20 give 124–127 damage.
- Passive recovery: `125/256` life per second (about 0.488), including while fighting. Fractional healing accumulates during active play. Full health, logout and restart do not bank healing. Native UO healing stays disabled.

The [Arreat Summit](https://classic.battle.net/diablo2exp/skills/necromancer-summoning.shtml) supplies the skill overview, limits and mana costs, but its displayed totals omit parts of the actual summon calculations. Formula order and monster-level additions follow [D2MOO's legacy summon implementation](https://github.com/ThePhrozenKeep/D2MOO/blob/master/source/D2Game/src/SKILLS/SkillNec.cpp); recovery follows its [monster initialization](https://github.com/ThePhrozenKeep/D2MOO/blob/master/source/D2Game/src/MONSTER/Monster.cpp) and [per-frame regeneration](https://github.com/ThePhrozenKeep/D2MOO/blob/master/source/D2Game/src/MONSTER/MonsterMode.cpp). Base values and skill parameters were checked against extracted vanilla 1.13 [MonStats](https://github.com/fabd/diablo2/blob/master/code/d2_113_data/MonStats.txt), [MonLvl](https://github.com/fabd/diablo2/blob/master/code/d2_113_data/MonLvl.txt), and [Skills](https://github.com/fabd/diablo2/blob/master/code/d2_113_data/Skills.txt) tables. Movement and attack periods remain project balance values. Existing guardians recalculate derived maximum life and damage on loading while preserving saved current life. Summon Resist and other summoning effects remain inactive.

The action interval (720 ms), walking interval (240 ms/tile), and graveyard XP rewards are initial project balance values. Resistances currently start at zero. Higher-rank damage follows the published table, including its repeated final entry, pending comparison with game data. These values are independent of UO taming and weapon skills. Native war stance remains an internal NPC/AI detail.

At the current skill limit, a successful cast replaces the oldest skeleton. It costs mana and consumes the selected corpse normally, allowing injured or outdated skeletons to be replaced without attacking them. Skeletons vanish on their own death or their owner's death. They provide no loot, XP or reusable corpse.

Minions log out with their owner, like bonded pets, and return beside the owner on login with the same identity, current life, level and casting-time stats. They resume **Guard me** without another corpse, mana charge or healing. They also survive server saves/restarts. While offline they are removed from the playable world, with pending attacks cancelled and their native ownership retained; they do not fight or wait at the logout location. Repeated disconnect/logout notifications cannot duplicate them. If nearby floor is blocked, a minion waits safely and retries when its owner moves. They do not use the stable interface or taming loyalty/bonding rules.

`PersistentMinions` supplies this lifecycle for class pets, minions and summons through `IPersistentMinion`; future Valkyrie and Druid creatures can use the same path and provide their own action cancellation. Native auto-stabling and summon expiry skip only these opted-in entities. The existing guardian serialization layout is unchanged, including loading guardians saved by the earlier build. Other UO pets and timed summons keep their native behavior.

A consumed monster corpse is unavailable to both player summoning and lich resurrection. Empty consumed bodies disappear immediately. Unclaimed loot keeps its actual corpse container until looted or normally decayed; consuming a body never deletes equipment inside it. Repeatedly resurrected wild monsters can be consumed normally but never become a new source of rewards.

Allies now follow the [occupied-tile movement rule](allied-movement.md): pets and owners can pass through one another, only the newcomer becomes movement-only, and tile holders never yield automatically. A passing pet unable to move for five seconds regroups safely beside its summoner without healing.

## Death and recovery

Seren, the free, invulnerable healer at **1380, 1504, Felucca**, stands west of the Britain graveyard. Walk a ghost within four tiles to receive the normal resurrection prompt. Class characters return with full life, including resurrection routes that normally apply a partial-life scalar. Existing non-class characters retain their original resurrection behavior.

Death destroys **20% of backpack-carried gold at every level**, rounded down once across all stacks, including nested bags and protected gold. Bank gold and older corpses are excluded. A saved corpse claim prevents duplicate charges, including after resurrection, reconnect or restart. Remaining carried gold follows normal recovery, and there is no XP penalty. See [T13](t13-death-gold.md) for rounding, stack handling and verification.

After resurrection, single-click your own corpse within two tiles. Original equipment is re-equipped where requirements and free equipment slots permit, then remaining contents transfer into your backpack. Weight and capacity restrictions leave excess items in the corpse and allow another attempt later. New equipment already worn is not replaced. The temporary resurrection robe is removed after original equipment is restored.

[T44 corpse escrow](t44-corpse-escrow.md) preserves player corpses for24 real hours, including logout and downtime. Afterward, remaining belongings move into private owner-character escrow, with a separate claim per death and no additional expiry initially. Say **recover** to a banker, or use **[Recover** within3tiles, to review contents and pay from bank. The fee is10% of aggregate value, minimum100gold per corpse, using the confirmed resale/purchase/fallback rules. Whole-claim recovery retains real items and nested bags; a full bank or insufficient funds leaves everything safe. Closing or deferring never forfeits anything. Only a separate explicit confirmation of **Surrender belongings** permanently deletes that selected claim.

Only the actual non-gold items supplied at character creation receive automatic Blessed death protection. Acquired equipment remains ordinary unless separately insured through [T43](t43-insurance.md); insured gear stays in the backpack on the covered death. Protected starting equipment stays in your backpack on death and is eligible for re-equipping when you recover your corpse. Class characters do not use UO's Young death relocation/protection.

For the existing local character, original starting item identities were recovered from an early saved-world backup. A private, ignored `Configuration/sanctuary-starter-items.json` migration applies the same protection to those exact surviving items, guarded by owner identity/class. After saving and independently verifying protection, the local migration was archived as `sanctuary-starter-items.applied.json`. No later acquired gear is classified by item type, creation timestamps or inventory contents.

## Verification

The disposable integration suite checks earned kill XP/level rewards, point budgets, summon casting windup and mana, corpse priority/consumption, guardian stats/orders, owner kill credit, friendly-fire rejection, repeated logout/login, injured minion save/reload, map changes and saved character bindings/progression. Exact-zero lethal hits retain kill credit and rewards. Death checks cover all seven classes, full life, unchanged legacy resurrection, carried/banked gold, equipment recovery, leftovers and retry safety. The actual-client suite exercises ghost movement/resurrection, single-click corpse recovery, learning, assignment, mouse casting, hotkey casting, the pet stats window and an actual client disconnect/relogin with the same injured guardian.


The local deployment was backed up at `.local/backups/pre-progression-pets-20261017-230144/`. Full combat/loot UI regression, pet UI, expanded status spending, corpse recovery/resurrection, save/reload/migration, picking masks and patch-series checks passed. Mii's class, level, XP, ranks and attribute allocations were preserved; all six original starting items were verified Blessed after a live save without reapplying the migration. Existing configuration and client profile files retained their hashes. The live server starts cleanly and the desktop client was left closed.

The minion persistence update was backed up at `.local/backups/pre-persistent-minions-20261017-234856/`. Repeated logout/login, retained life/stats/ownership, restart and map-change checks passed, alongside an actual client disconnect/relogin and the summoning UI regression. Logs are `.local/logs/persistent-minions-check.log` and `.local/logs/persistent-minions-ui-check.log`; the live server runs the tested content and UOContent assemblies.

Short-lived Assassin traps follow the approved Sanctuary policy: changing facets leaves them in their original facet until expiry. Their retained base stats still rescale to the owner’s current facet difficulty. While disconnected they stop fighting and moving, but their lifetime continues; login does not restart lifetime or replay missed attacks. Existing long-lived summon persistence remains unchanged. Blade Sentinel’s adapter is tracked separately from its still-disabled learned input in [lifecycle evidence](../tools/reference/blade-travel-native-evidence.json).

# Original Shadow Fury clock checkpoint

2026-11-08 JST. **208/240 handlers; Assassin28/30; no class complete.** The original Fury clock is qualified. Full cast/state/rewind/emission/AI integration remains gated.

## Authoritative result

Pinned D2R Reign of the Warlock **3.3.93847** `monstats` slot2 assigns both Shadow species `seq_swbladefury`. Their initializer retains **mode14**, overriding the generic skill metadata's mode7/`SC`. The actual monster sequence has17 points,all SC poses,and its only action at point9. Current Fury `seqtrans=A1` and absent/zero `UseAttackRate` select the retained Other-rate branch. Sequence speed stays256; displayed animation speed is separate.

The existing complete NPC initializer/rate/stop helpers now produce **action marker10 and unrewound end18**. `ShadowFuryClock` reuses `AssassinSentrySequence` for that projection,checking actual owned mode and original dual-claw binding. It does not emit missiles,roll RNG,spend resources or enable an action. Neither player SQ23 nor ordinary SC17 timing substitutes for the initialized original.

This result matters for the next integration: the unrewound sequence ends before the initial **21-frame Inferno lease**. The timed list,active bit,skill deadline and action lifetime must be composed separately; clearing all channel state when the action ends would lose valid re-entry context. The effects of do48's event rewind on the actual sequence callback/event host still need a complete composed check.

## Verification and diagnostic

Accepted **Content2 / Integration2 / snapshot3 / native-run3** in `.local/class-skills/shadow-fury-action`. Production inputs match Content2's prebuild snapshot; only the integration fixture was renamed before Integration2. Builds have zero warnings/errors.

**21 native markers,three boots,two actual reloads,three saves.** Both original species preserve mode14 and project10/18 despite own IAS,FCR and attack-rate perturbations. Clock calls leave RNG,world allocation,mana,missiles and current action untouched. Original bodies/items/resources/wounds/seeds survive reload. Retained own Fury travelling receiver,player Fury and full own Flight checks pass. Learned Shadows remain disabled. Installed build/configuration,saves and historical client artifacts pass the existing preservation check;tests use disposable `/dev/shm` servers.

**12 new complete source NPC clock controls** use the actual original sequence. Existing initializer/rate/advance proofs are authenticated and reused. Another30 full ordinary-SC initial/rewind comparisons are retained explicitly as comparisons,not the original Fury action.16 critical comparisons isolate the older signed-roll ambiguity from an explicitly modified unsigned proposal. Both source harnesses pass ASan/UBSan;old compiler matrices were not rerun.

The first source probe expected an old generated harness that was absent;only its preamble was reconstructed from the retained tool,using exact pinned routine bodies. Native run1 found an unnecessary nonexistent Flight fixture path. Native run2 then rejected the ordinary-SC prototype on the real original mode14 bodies. That prototype was removed;the existing action,animation and executor files match the parent commit exactly. Failed inputs/logs/prototype sources remain under the stage for diagnosis. No prototype was deployed or admitted to AI.

- [Original source clock evidence](../tools/reference/shadow-fury-clock-source-evidence.json)
- [Comparison/critical evidence](../tools/reference/shadow-fury-action-source-evidence.json)
- [Native/preservation receipt](../tools/reference/shadow-fury-clock-native-evidence.json)
- Native runner: `SANCTUARY_TEST_SERVER=/media/aomukai/FILES/Sanctuary/.local/server python3 tests/check_shadow_fury_clock.py`
- Receipt: `python3 tools/record_shadow_fury_clock.py --check`

## Remaining decisions and work

The older `MISSILE_HasBonusStats` signs32-bit rolls before `%100`;negative remainders can pass a zero-percent chance. Current executable behavior is unverified. [Unsigned proposal](shadow-missile-critical-proposal.md) is concrete and tested but **unapproved and unconnected to gameplay**. Its helper has only a test caller. This is separate from the already approved free NPC Fury mana exception.

Next compose actual mode14 callback/do48/event rewind/sequence advancement and retained lease/re-entry. Then qualify own critical chance collection,world/private projectile seed allocation and automatic AI. Continue independent work while the critical question remains open. Blade Sentinel,modifier42death and learned Shadow admission follow. The existing Shock Web localdelay15 question remains nonblocking;current executable/PvP/caster-removal acceptance remains separate.

No new presentation is emitted by the clock. Effects/audio and talent/hotkey icons remain in their separate inventories,with the prior Fury/Shadow placeholders unchanged.

# Complete native Lich Revive world rule — proposed

Reference: D2R Reign of the Warlock **3.3.93847**, with Sanctuary’s approved deviations taking precedence. This proposal maps the existing native `Sanctuary.GraveyardLich` into Revive. It does not claim that this UO caster is a literal D2 species or replace its spells with D2 spells; that global monster-conversion work remains separate.

## Eligibility and identity

Use `unraveler1` solely as an explicit AI-eligibility analogue, matching the existing Lich reward/difficulty comparison. Reuse that row’s existing Revive species and switch-AI rules plus the actual captured native quality/uninterruptible flags. Keep the native corpse key, original mobile serial, runtime type, base life and before-death descriptor. Do not add a synthetic species row, infer eligibility from artwork, admit subclasses by inheritance or backfill older corpses without original state. Unknown elite/attachment/controller contexts and unsupported item/Sunder contexts retain their current refusals.

Retain and activate the actual dead creature, preserving its type, AI and living abilities. Leave dropped loot in the corpse and preserve qualified combat effects independently, using the existing frozen-equipment adapters. Successful activation detaches the inactive lease before consuming the corpse; later loot cleanup cannot delete the active original. Failed planning or activation leaves the old corpse/resources intact through the shared rollback path.

## Casting, stats and facets

Use the existing Revive begin/release clock, table mana cost, once-only payment, target validation and approved persistent roster limits. Capture effective Revive/Mastery ranks and original corpse source life/seed through the current source-life/combat planner. Restore the captured native mana/stamina, not fresh full pools. Native Liches keep their existing no-health-regeneration behavior, represented explicitly as rate zero rather than borrowing a D2 monster’s regeneration.

Use the existing authored Normal level 5/base 90, Nightmare level 36/base 1,102 and Hell level 69/base 9,118. On facet changes use the corresponding existing native combat variant and source-life projection, preserving current life percentage. Birth ranks and source history remain snapshots; changing gear does not rewrite them. No new life curve or living-monster rebalance is introduced.

## Living abilities and helpers

Keep the current native firebolt and corpse-raising routines. Blind/fear melee falls through to the existing native weapon swing, avoiding the Lich’s absent D2 melee-animation data and the old null `NormalStats` dereference. This is a UO host mapping, not a verified D2 animation. Native spell/raise startup and cooldown/load behavior remain the existing host behavior, pending the separate monster-spell conversion.

Raised Skeleton/Zombie helpers belong to the same player and retain a separate link to the actual raising Lich. Reuse the existing nearby six-raised-unit limit and native helper stats. Helpers cannot recursively raise more helpers. Shared corpse access/loot rights apply, and consumed corpses retain protected loot.

Adopt the prepared dependent-helper mapping: removing/killing/replacing the raising Lich removes its helpers. This differs from wild helpers, whose existing independence remains unchanged. Reuse pet allegiance, safe-area/class/offline parking, difficulty variants and saved fractional wounds. Parking gives no healing/reset; existing timed effects retain their own expiry. No expiration is added to the active Revive or its helpers. Owner death/loss uses existing roster cleanup. Existing native missile ownership continues to govern in-flight firebolts; no independent projectile lifetime is inferred here.

## Persistence and verification boundary

Reuse original-actor, corpse, source-life/combat, baseline, minion and raised-helper persistence. No old serializer version is changed. Save both inactive original corpses and activated originals with their actual identities, loot and fractional wounds; offline actors remain parked. Ordinary AI action clocks retain native startup/load behavior rather than gaining invented D2 timer serialization.

The [checkpoint](native-lich-revive-world-checkpoint.md) supplies scoped build, live ability, facet, wound, loot and reload evidence. Learned native Lich Revive stays gated by `NativeReviveRules.Approved` until this complete mapping is adopted. Prepared tests use an explicit service argument, not a replacement skill handler. Native Revive player-input acceptance follows adoption; existing mapped Revives remain playable. Final effects/audio and icons stay in their separate asset registers.

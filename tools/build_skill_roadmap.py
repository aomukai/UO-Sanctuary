#!/usr/bin/env python3
"""Describe actual registered skill handlers without treating a handler as full verification."""
import argparse
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
BALANCE = ROOT/'data/Sanctuary/Balance'
ORDER = ['Necromancer','Sorceress','Paladin','Barbarian','Amazon','Assassin','Druid','Warlock']
BLOCKERS = {
    'sorceress.inferno':'SQ6 timing and launched flames are prepared, but modern repeated-payment/re-entry cadence remains unverified. startmana6 is a threshold; legacy start/do/rewind and rank1 debit paths cannot safely establish the D2R channel loop. Keep talent gated pending trace/capture; do not charge six mana as a substitute.',
    'necromancer.irongolem':'Requires authoritative metal-item eligibility, ownership/consumption, complete inherited item effects/procs, and persistent item identity.',
    'necromancer.revive':'Requires saved corpse species/combat reconstruction, original special abilities, excluded-species rules and the source 180-second lifetime; do not substitute a generic skeleton.'
}
CAVEATS = {
    'barbarian.howl':'Reuses the64-ray controller with pinned4mana, rank velocity, source MaxVel clamp/deceleration disable, stored rank/live caster-level gate, LastCollide and no Terror refresh. Shared curse metadata preserves initial escape/cleared caster override/later threat selection, idle/close combat, move intent and expiry. Supported Skeleton/Zombie/Megademon source AIs only; native source pathing/scan/leash adapters, Attract/curse-resistance failure transients, other callback species, modern SC/FCR/geometry/owner/client remain explicit limits. Ranks81+ remain gated for uncertain signed32 source overflow. Focused/full native verification recorded separately; no class certificate.',
    'barbarian.battlecommand':'Immediate self state and shared64-ray warcry implement11mana, nonstacking allskills1, stored cast rank/live hard duration and saved LastCollide/skill-local NextHit gates. Effective ranks update learned/granted skills without changing hard synergies; native passives/resources refresh, summon creation snapshots stay fixed and pet caps remove oldest saved creation order on expiry. Focused native three-boot/two-reload checks pass, including new/old summon snapshots, FIFO cap removal, passive resource transitions and resumed saved rays. Combined and actual-client acceptance remain separate. Historical pet order falls back to serial; modern SC/FCR/geometry/NHD caster/cast scope, NPC own skills and full owner/item/client behavior remain unverified.',
    'necromancer.teeth':'Existing fan/missiles function, but fixed60-degree spread remains provisional. Pinned Teeth and Amazon Multiple Shot both use server do8; extract/verify the shared source fan before reusing it for Multiple Shot. Keep skill-specific missile, damage, hit-delay, piercing and proc rules separate. Native/client/modern fan equivalence remains open.',
    'barbarian.battleorders':'Self state and64 travelling rays implement pinned7mana after self stats, stored effective rank/live hard duration, replacement and proportional FP256 current pools. NPC resource hooks preserve saved seeds/loading pools, Vigor joins additively, and summon creation fractions/HP regeneration have dedicated consumers. LastCollide initializes to caster; saved skill-local NextHit4-frame gates and rays resume across reloads. Focused player/pet/gear/zero-pool/expiry/lifecycle and two reloads pass. Exact modern NHD caster/cast scope, consume-failure edge, SC/frame/geometry/neutral/owner/client and approved whole-resource projection remain explicit limits; no class certificate.',
    'barbarian.shout':'Self state plus64 travelling decelerating rays, source6-mana do-event payment, stored effective rank/live hard duration synergies, repeated aligned recipient hits and weaker replacement are implemented. Cooperative players/pets receive additive defense, with Berserk retaining final zero defense. Dedicated native moving-ally, real gear, resource/interruption, lifecycle and two reloads exercise the bounded port. Source ring/hit/stat/acceleration routines compiled separately. D2 subtile-to-UO tiles, collision footprints/terrain/neutral NPC alignment, exact SC event/FCR, mid-windup mana-loss ordering, modern scheduling/snapshots and actual-client equivalence remain unverified; effects/icons are independent placeholders.',
    'barbarian.berserk':'Prepared interruptible A1/IAS attack, linear AR, hard Howl/Battle Orders ED, one4-mana start payment and full physical-to-magic conversion after critical/before resistance. Timed final zero-defense override starts on hits/misses, persists through cancelled swings, replaces expiry on recast and saves independently. Do2 recreates an expired state only through the remaining animation. Connected natural timed/temporary expiry, real gear/physical-consumer controls, death/respec/history and two reloads have dedicated coverage. Modern snapshots/slow/modes/item/proc/owner/client, native magic-resistant species, low-mana fallback, automatic ground targeting and dual wield remain open.',
    'barbarian.concentrate':'Prepared ordinary melee hit/AR/hard Bash/Battle Orders damage and Berserk physical-to-magic split after critical/before resistance, with one2-mana start payment. Action-long defense starts at preparation, refreshes at impact and lasts through recovery; voluntary movement/get-hit/Stop/rebinding do not interrupt. Connected natural-loop, named gear, misses/immunity, death/history/respec and two reloads have dedicated coverage. Legacy mode/state/conversion blocks compiled separately. Low-mana fallback, empty-ground acquisition, exact modern snapshots/queues/item events/procs/owner/client and dual wield remain open; no modern equivalence certificate.',
    'barbarian.bash':'Prepared melee hit/AR/hard synergies, critical/mitigation snapshots, one2-mana start payment and source-mapped timed knockback implemented. Source calc2 adds to the physical-event field after allocation leaves the stored life total unchanged; the legacy health consumer commits that stored total. Current D2R stale-total behavior is unverified. Input/cancellation/real gear/immune/miss/native control and two reloads have dedicated coverage. Unknown/special control species and converted throwing melee are gated; low-mana AttackNoMana, empty-ground acquisition, PvP and exact modern events/procs/leech/owner/client remain open.',
    'barbarian.stun':'Prepared melee hit/AR/hard Bash damage, one2-mana start payment and stored tiered hard-War-Cry duration implemented. Shared source control supplies species/boss/unique/velocity/cap rules and native movement/attack gates. Input/cancellation/real gear/immune/miss/control natural expiry and two reloads have dedicated coverage. Unknown native control metadata and converted throwing melee are gated; low-mana AttackNoMana, empty-ground acquisition, PvP and modern snapshots/events/procs/owner/client remain open.',
    'barbarian.swordmastery':'Pinned item-type inheritance and effective-rank melee AR/damage/critical are implemented. Strongest eligible bonus is selected; percentages join existing pools; mastery critical precedes Deadly Strike and doubles physical once. Real converted one/two-hand weapons, named soft/local/offweapon/deadly/flat-element gear and two reloads are covered by the dedicated fixture. Legacy selector/critical blocks are compiled separately. Dual wield, melee use of converted throwing items, modern snapshots/modes/owner/proc/client and material conversion acceptance remain open; Throwing Mastery is separate.',
    'barbarian.axemastery':'Pinned item-type inheritance and effective-rank melee AR/damage/critical are implemented. Strongest eligible bonus is selected; percentages join existing pools; mastery critical precedes Deadly Strike and doubles physical once. Real converted one/two-hand weapons, named soft/local/offweapon/deadly/flat-element gear and two reloads are covered by the dedicated fixture. Legacy selector/critical blocks are compiled separately. Dual wield, melee use of converted throwing items, modern snapshots/modes/owner/proc/client and material conversion acceptance remain open; Throwing Mastery is separate.',
    'barbarian.macemastery':'Pinned item-type inheritance and effective-rank melee AR/damage/critical are implemented. Strongest eligible bonus is selected; percentages join existing pools; mastery critical precedes Deadly Strike and doubles physical once. Real converted one/two-hand weapons, named soft/local/offweapon/deadly/flat-element gear and two reloads are covered by the dedicated fixture. Legacy selector/critical blocks are compiled separately. Dual wield, melee use of converted throwing items, modern snapshots/modes/owner/proc/client and material conversion acceptance remain open; Throwing Mastery is separate.',
    'barbarian.polearmmastery':'Pinned item-type inheritance and effective-rank melee AR/damage/critical are implemented. Strongest eligible bonus is selected; percentages join existing pools; mastery critical precedes Deadly Strike and doubles physical once. Real converted one/two-hand weapons, named soft/local/offweapon/deadly/flat-element gear and two reloads are covered by the dedicated fixture. Legacy selector/critical blocks are compiled separately. Dual wield, melee use of converted throwing items, modern snapshots/modes/owner/proc/client and material conversion acceptance remain open; Throwing Mastery is separate.',
    'barbarian.spearmastery':'Pinned item-type inheritance and effective-rank melee AR/damage/critical are implemented. Strongest eligible bonus is selected; percentages join existing pools; mastery critical precedes Deadly Strike and doubles physical once. Real converted one/two-hand weapons, named soft/local/offweapon/deadly/flat-element gear and two reloads are covered by the dedicated fixture. Legacy selector/critical blocks are compiled separately. Dual wield, melee use of converted throwing items, modern snapshots/modes/owner/proc/client and material conversion acceptance remain open; Throwing Mastery is separate.',
    'barbarian.increasedstamina':'Pinned live effective-rank maximum-stamina percentage, additive with Vigor and without free refill, is implemented. Real gear, natural native recovery after training, respec/history/death and two-reload checks cover the supported path. Exact modern stat collation/FP and item/owner/client acceptance remain open.',
    'barbarian.ironskin':'Pinned live effective-rank defense percentage is additive in the shared defense pool. Native gear/Dexterity defense, running suppression, respec/history/death and two-reload checks cover the supported path. Exact modern mode/event/owner/client comparison remains open.',
    'barbarian.increasedspeed':'Pinned diminishing skill velocity is added alongside Vigor to native walking/running, separate from item FRW diminishing. Accepted steps, gear/Vigor composition, respec/history/death and two-reload checks cover the supported path. Existing Sanctuary movement clocks are retained; modern continuous/mode/client equivalence remains open.',
    'barbarian.naturalresistance':'Pinned diminishing four-element resistance enters the raw pool before cap/pierce, with no physical/magic/maximum-resistance grant. Actual typed native damage, real soft-rank/resistance gear, respec/history/death and two-reload checks cover the supported path. Exact modern stat/item/owner/client comparison remains open.',
    'paladin.sanctuary':'Free delayed undead magic/proximity pulse and source-mode knockback, supplier-only undead damage/flat AR and separate positive physical-resistance clamp staged. Legacy supplier-stat mana threshold is retained provisionally; knockback works at zero mana. Broader undead bypass flag, low-mana supplier stats, exact geometry/mode flags/post-KB recovery, special sand-leaper/owner/item/client behavior remain explicit acceptance work. No class/item gate cleared.',
    'paladin.holyfreeze':'Supplier cold weapon stats and shared pulse, source-mapped Prime-Evil/ColdEffect gates, separate capped movement/attack/other slow state and twenty-percent independent shatter marker staged. Target slow expiry does not silently clear that separate marker. Active shatter deaths retain loot but exclude corpse skills. Native UO other-animation mode consumers, modern lethal-pulse/death ordering, full species/state interactions, proximity geometry/rounding and gear/owner/client acceptance remain open; unmapped native targets are excluded explicitly.',
    'paladin.conversion':'Available melee attack with one4-mana start payment, prepared hit/base weapon packet, event-time conversion roll, no weapon damage on successful conversion, species-preserving allegiance/AI/protection/reward/expiry/persistence. Focused input/hit/miss/payment/success/cancel and two-reload coverage. Higher-level life restoration, unmapped species/imp attachment and bard interaction have explicit target gates. Complete stat-list removal, modern snapshots/proc/curse/durability/owner/gear/client behavior remain acceptance work; no item gates cleared.',
    'paladin.charge':'Travel/arrival/one-impact/recovery phases, source velocity baseline100, additive skill damage/AR, hard Might/Vigor synergies, one9-mana start payment, close basic-attack fallback and accepted native steps staged. Native input/miss/obstruction/owner-transition/two-reload and connected-owner ground phases covered. Mapped standard-monster knockback now uses the shared source mode/path controller; unknown species and special sand-leaper control stay gated. Item knockback remains suppressed. Modern collision/range/body/FP projection, fallback payment, full gear/proc/curse/durability/owner/client acceptance remain open; no item gates cleared.',
    'paladin.vengeance':'Melee weapon timing/range, source additive skill attack rating, separate shared fixed-point weapon-base elemental roll, mastery percentage rounding, one event payment and typed defense/absorption delivery staged. Native damage/miss/block, actual soft/mastery gear, two-handed base/reach and two reloads covered. Distinct active item_normaldamage is excluded from the elemental roll only; real Stone Crusher10/30 differential/upper-endpoint/native physical-and-elemental/reload checks pass. Converted throwing weapons need a melee base adapter; exact modern start/do snapshots, mode/event/owner/gear/client acceptance remain open. No charged/oskill/item gates cleared.',
    'paladin.sacrifice':'Melee action/input, additive skill damage and attack rating, FP256 endpoint roll, prepared-physical/target-life-capped rank-dependent self cost and self-before-target commit staged. Native zero mana, immunity, miss/block, Energy Shield, real absorb/soft-rank gear, whole-life debit/self death and two reloads covered. Modern start/do snapshots, converted throwing adapter and full curse/item/leech/LifeTap/retaliation/mode/owner/client acceptance remain open; no item gates cleared.',
    'paladin.holyshield':'Right-only shield-gated timed cast, source fixed-point mana, stored block/rank, live hard Defiance defense, shield-dependent consumers and faster block rate staged. Native cast/equipment/expiry/lifecycle and two reloads covered. Stored-rank Smite damage now has its separate staged consumer. Modern mode/weapon-swap/owner/gear/client acceptance remain open.',
    'paladin.zeal':'Source-counted timed melee series, one successful-start mana payment, live attack rating/damage/hard Sacrifice synergy, cyclic target identities, retargeting and uninterruptible movement/recovery lifecycle staged. Source scheduler/selection probes and native input/gear/late-frame/diagonal/resource/two-reload coverage; Approved low-mana ordinary-attack deviation retains bindings and resumes held Zeal at two mana; pinned D2R was observed to stop instead. Native zero/fractional-mana/input/return-to-Zeal coverage; modern timing/geometry and full item/curse/proc/durability/owner/client acceptance remain open. Active inputs are transient across logout/restart; no item gates cleared.',
    'paladin.smite':'Pinned69 shield bases, stored-rank Holy Shield damage, additive shield Strength/off-weapon/skill percentages, physical-only hit without ordinary crit/leech/elements, distinct S1 timing and2-mana event payment staged. Mapped-species stun plus separate shared timed source knockback replace the old instant one-step displacement; native movement/attack gates, natural motion/recovery/expiry and two stun reloads covered. Active item_normaldamage now augments shield endpoints before percentages, with real Stone Crusher rolls and two-reload coverage. Unknown native control metadata, modern modes/timing, gated Astreon/Redeemer, absent Grief runeword acquisition and full proc/LifeTap/durability/geometry/client acceptance remain open; no item gates cleared.',
    'paladin.conviction':'Hostile source-ranked leases, supplier protection, base-immunity snapshot deltas, independent Lower Resist combination, defense-percent addition and class/legacy elite priority staged. Native damage, signed source defense, radius, equal/weaker rank behavior and two reloads covered. Original native base-stat mapping, shared self/enemy state edge cases, modern sunder/PvP/hireling/filter/owner/gear/client acceptance remain open.',
    'paladin.thorns':'Source free delayed recipient aura, attack-attempt flat physical damage and prepared-physical percentage return staged. Native misses, typed/fractional/zero/ranged/spell cases, life-capped before-incoming-hit ordering, immunity, rank20 and two reloads covered. Legacy player/hireling percent reduction and NativeBaseHire projection are explicit; full gear/attempt ordering/owner/modes/PvP/client acceptance remains open.',
    'paladin.blessedhammer':'Cast-time hard synergies/Concentration,77 source waypoints, Q12/FP16 stepping, five-update acceleration, piercing most-recent-collider history, ordinary magic immunity and separate species bonuses staged. Native source goldens, resources and two reloads covered. Fog float LUT is reconstructed explicitly; exact modern path/cell/body/terrain/order/RNG/SC, full gear/owner and actual-client behavior remain acceptance work.',
    'paladin.fistoftheheavens':'Selected-target delayed lightning strike, original-position capped secondary burst, fresh child synergy and separate cooldown staged. Secondary Holy3 bypasses undead/demon resistance; no NHD with NextHit0, no ally/pet healing with parent flags0. Converted/dead target burst and saved corpse identity covered. Cursor-nearest automatic acquisition in the click-facing half-circle and held-ground identity retention are implemented from pinned-game user checks. Equal-distance ties, own-tile cursor and perpendicular boundary candidates stay gated; exact range/metric/cursor snapshots, complete removed-unit lifetime, timing/secondary order/primary-origin collisions and owner/gear/client acceptance remain open.',
    'paladin.holybolt':'Timed ally/hostile cast, source fixed-point mana/damage/healing, piercing persisted flight, current Prayer/original missile rank healing, undead positive-resistance/MDR/absorb bypass and demon damage staged. Native pipeline, immune undead and poisoned ally/pet checks pass. Exact modern SC event/collision/unit-size/filter/owner/gear/client behavior remains acceptance work.',
    'paladin.holyfire':'Supplier-only whole weapon stats, shared fractional soft-hit pulse, delayed cadence and released2.4 proximity rule staged. Explicit linear tile/melee projection is provisional: modern distance metric, unit sizes and rounding, full mastery/ranged/native/owner/gear/filter/client acceptance remain open.',
    'paladin.holyshock':'Supplier-only minimum-one weapon stat, lightning synergy, shared fractional soft-hit pulse, delayed cadence and released2.4 proximity rule staged. Explicit linear tile/melee projection is provisional: modern distance metric, unit sizes and rounding, full mastery/ranged/native/owner/gear/filter/client acceptance remain open.',
    'paladin.concentration':'Physical enhanced damage and20-percent interruptible class-action recovery guard staged, with source cast-time Blessed Hammer bonus delivery staged. Modern mode-transition/gear/owner/client paths remain acceptance work; NPC mode code has no equivalent protection check.',
    'paladin.fanaticism':'Skill attack rate separate from gear IAS, attack rating and caster/recipient physical bonuses staged in player/native pet/class summon consumers. Strict legacy positive-mana self override and equal-rank replacement are explicit, not modern-certified; full timing/dual-wield/pet/owner/item/client acceptance remains open.',
    'paladin.vigor':'Selected aura with source stamina/recovery/velocity values, player/pet resource and movement consumers staged. Native baseline/tile-clock projection is retained; absolute D2R mode-dependent recovery, run/walk scaling, full pet modifier composition, gear/owner/client acceptance remain open.',
    'paladin.redemption':'Delayed owner-only per-corpse recovery and one-use consumption staged with pinned corpseSel rules, captured death-time source identity and approved corpse access/loot protection. Unmapped legacy/native species are excluded; modern RNG/search/species/owner/client behavior remains acceptance work.',
    'paladin.resistfire':'Selected periodic resistance aura and allocated-point active/inactive maximum resistance staged. Effective rank drives ordinary resistance; active state suppresses the passive. Same-aura rank replacement, independent aura addition and player cap after pierce are covered. Modern filters/gear-owner transitions, full pet/native paths and actual-client acceptance remain open.',
    'paladin.resistcold':'Selected periodic resistance aura and allocated-point active/inactive maximum resistance staged. Effective rank drives ordinary resistance; active state suppresses the passive. Same-aura rank replacement, independent aura addition and player cap after pierce are covered. Modern filters/gear-owner transitions, full pet/native paths and actual-client acceptance remain open.',
    'paladin.resistlightning':'Selected periodic resistance aura and allocated-point active/inactive maximum resistance staged. Effective rank drives ordinary resistance; active state suppresses the passive. Same-aura rank replacement, independent aura addition and player cap after pierce are covered. Modern filters/gear-owner transitions, full pet/native paths and actual-client acceptance remain open.',
    'paladin.salvation':'Periodic fire/cold/lightning resistance without poison or maximum-resistance bonuses staged. Adds to different elemental resistance auras. Modern filter/owner/item paths and actual-client acceptance remain open.',
    'paladin.prayer':'Delayed periodic direct healing, effective rank, useful-pulse-only fractional mana payment, natural recovery suppression and independent approved flat recovery staged. Modern callback/stacking, Blood Mana, owner/native timer transitions and client acceptance remain open.',
    'paladin.cleansing':'Delayed periodic remaining poison/curable-curse duration reduction with source state flags and effective Prayer healing staged. Unrelated native UO poison/curses/shrines lack adapters; modern timing/filter/stacking and actual-client checks remain open.',
    'paladin.meditation':'Delayed player-only mana-recovery bonus additive with Warmth/gear and effective Prayer healing staged. Sanctuary native regeneration baseline is retained; absolute D2R max-mana recovery and native first-tick retiming remain acceptance questions, alongside gear/owner/client paths.',
    'paladin.might':'Selected-right-slot aura, immediate self/ally refresh, source periodic leases and physical-only player/skeleton/golem/native-pet bonuses staged. Exact modern aura filter/linger, town-room projection, all pet/item combinations and actual-client acceptance remain open.',
    'paladin.defiance':'Selected-right-slot aura and source defense-percent leases staged, additive with cold-armor defense. Exact modern aura filter/linger, town-room projection, all native defense paths and actual-client acceptance remain open.',
    'paladin.blessedaim':'Selected-right-slot attack-rating aura and allocated-point passive staged. Active aura suppresses the passive; same aura does not sum. Exact modern aura filter/linger, town-room projection, all pet/item combinations and actual-client acceptance remain open.',
    'sorceress.chainlightning':'Travelling segments, finite source hit counts, cyclic identity targeting, fresh child damage at the original cast rank, per-cast four-frame next-hit delay, resources and persistence implemented. Cast scope follows official 2.7 notes and firsthand original testing; compare the pinned modern build. Exact Size2/FP16 paths, target flags, gear timing, owner transitions and actual-client acceptance remain open.',
    'sorceress.hydra':'Three stationary untargetable heads, six-group cap, source rank/damage/mastery/pierce snapshots, legacy animation/AI timing, travelling bolts and persistence implemented. Exact modern startup/target search/terrain fallback, immunity piercing, owner-removal order, Slow Missiles/CanDestroy and client acceptance remain open.',
    'sorceress.frozenorb':'Source rotating trail emissions, sixteen terminal curved shards, damage/chill, per-shard collision and local delay implemented. Overdue source frames are now processed before natural expiry; a controlled two-frame delayed callback preserves remaining travel emissions and terminal shards. Exact D2R initial-step/cell/path quantization, footprint, unit ordering, lag and owner transitions require comparison.',
    'sorceress.blizzard':'Source elapsed-frame sampling, independent storms, source-rank snapshots, fresh per-fall synergies, short-lived LastCollide shards, cold damage/chill and local delay implemented. Exact D2R Size2 footprint/unit-search order, terrain mask, initial activation, lag and owner transitions require comparison; current tile projection is provisional.',
    'sorceress.meteor':'Source delayed area impact, shared impact roll, distinct residual-fire damage/synergy, eighteen source offsets, rank-scaled flame lifetime and independent local delay implemented. Exact D2R terrain/Size2 overlap, timing, special gear paths, owner lifecycle and actual-client comparison remain open.',
    'sorceress.blaze':'Dedicated fixtures cover idle exclusion, accepted movement, source aura/speed/patch lifetime, fractional fire, expiration and saved patches. Exact subtile footprint, lag/view limits, full-heal fractional projection, town/owner-death lifecycle and client comparison remain open.',
    'sorceress.firewall':'Dedicated fixtures cover compiler-verified source synergy, perpendicular growth, damage-rate MDR scaling, fractional damage and independent local delay/expiry. Exact terrain/footprint, lag/view limits, town/owner-death lifecycle and client comparison remain open.',
    'sorceress.chargedbolt':'Source bolt count/cap, fixed seed/waypoint vectors, capped lifetime, one-time mana and independent non-piercing overlaps have dedicated fixtures. Two-subtile tile projection, continuous collision/velocity, Slow Missiles and actual-client/D2R path comparisons remain open.',
    'sorceress.enchant':'Dedicated fixtures cover ally/pet and held targeting, attack rating, cast snapshots, weaker/equal/stronger replacement, class/native/summon weapon fire, ranged wielder mastery, expiry/death and reload. Actual-client targeting/glow, special weapon paths and charged/oskill delivery remain open; no weapon item is modified.',
    'sorceress.lightning':'Distinct source SQ sequence, travelling pierce and target exclusions have native coverage. Exact Size=2 footprint, LastCollide overlap cases, Slow Missiles and actual-client comparison remain open.',
    'sorceress.thunderstorm':'Source duration/interval, cast-rank snapshot, one-time mana cost, serial-order target cycling and resumed strikes through both reloads have native coverage. Shared-world radius mapping, full target eligibility and D2R gear timing still need acceptance.',
    'necromancer.bonewall':'Destructible timed source-stat segments, growth and collision implemented. Native terrain, damage, lifetime and reload checks; exact growth missile range/cadence, inherited level, difficulty and live-client geometry still require D2R comparison.',
    'necromancer.boneprison':'Twelve destructible source anchors with provisional raster connectors enclose the target. Native gap/destruction/lifetime/reload checks; exact footprint, incomplete terrain, projectile exceptions and difficulty scaling remain.',
    'necromancer.teeth':'Numerics covered; fan geometry, per-cast next-hit delay, moving-target collisions and final client presentation still need play verification.',
    'necromancer.bonespear':'Native travelling/piercing and damage tiers covered; moving-target/obstacle/friendly edge cases remain.',
    'necromancer.bonespirit':'Homing/retarget code present; retarget cadence, lost targets and all collision cases need dedicated native checks.',
    'necromancer.poisonexplosion':'Source cloud lifetime retained; exact child-cloud footprint/hit cadence and overlapping clouds require D2R behavior comparison.',
    'necromancer.poisondagger':'Dagger targeting and source poison present; equipment poison duration/rate combination and drain interactions need further verification.',
    'necromancer.corpseexplosion':'Corpse/RIP guards covered; complete base-life/level/difficulty/elite fixtures and saved orphan-corpse handling remain.',
    'necromancer.skeletalmage':'Summoning, independent count and persistence covered; all four projectile elements, poison caps and moving-target travel remain to verify.',
    'necromancer.bloodgolem':'Native target drain, 30/70 healing allocation, both overflow directions and recipient caps covered. Return-damage/incoming-damage events, difficulty bases and D2R comparison remain.',
    'necromancer.firegolem':'Native source passive selection and replacement death burst covered. Aura scaling, absorb order, combat death and difficulty bases still need dedicated fixtures.',
    'necromancer.attract':'Non-overwrite rule covered; natural AI target acquisition, expiration and interaction with Confuse require world play tests.',
    'necromancer.confuse':'AI consumer present; allegiance/credit/expiration behavior needs dedicated world verification.',
    'necromancer.dimvision':'Native chase preservation, obstruction stop, adjacent acquisition, special suppression, expiry and saved motion covered. Caster/species behavior and live-client comparison remain.',
    'necromancer.terror':'Unsupported level gate removed. Source cast origin and flee-to-blind motion retained; ordinary-curse/flee coexistence and boss/species exceptions need D2R comparison.',
    'necromancer.ironmaiden':'Native melee reflection and ordinary ranged/elemental exclusions covered; source event order, reflected damage and special missile exceptions remain.',
    'sorceress.staticfield':'Native current-life fraction and mastery exclusion covered; Nightmare/Hell floors and mixed mitigation need dedicated fixtures.',
    'sorceress.frozenarmor':'Native exclusive armor, source defense/duration/synergy, physical-melee freeze and reload covered; full cold immunity/species and client comparisons remain.',
    'sorceress.shiverarmor':'Native attempted-melee retaliation including misses, chill instead of freeze, replacement and reload covered; all special attack/event cases remain to compare.',
    'sorceress.chillingarmor':'Source ReturnFire filtering and a travelling counter-bolt from a native arrow covered. Other missile/collision exceptions, cast/impact modifier timing and live-client comparison remain.',
    'sorceress.energyshield':'Native pre-resistance absorption, hard-Telekinesis efficiency, fractional mana, depletion, channel order, poison exclusion, actual-life-loss conversion and reload covered. Special drain fields and native ignore-armor paths remain unsupported; broader mixed mitigation and gear timing need comparison.',
    'sorceress.telekinesis':'Hostile-monster damage and source 35% terrain-checked knockback present. Remote item/object/portal/waypoint interactions and forced hit-recovery semantics are not implemented; no generic interaction is substituted.',
    'sorceress.teleport':'Pre-existing working Sanctuary Teleport; its approved range/scroll/tome and timing rules remain authoritative.'
}

NATIVE_PARTIAL = {
    'barbarian.bash','barbarian.stun','barbarian.concentrate','barbarian.berserk','barbarian.shout','barbarian.battleorders','barbarian.battlecommand','barbarian.howl',
    'barbarian.swordmastery',
    'barbarian.axemastery',
    'barbarian.macemastery',
    'barbarian.polearmmastery',
    'barbarian.spearmastery',
    'barbarian.increasedstamina',
    'barbarian.ironskin',
    'barbarian.increasedspeed',
    'barbarian.naturalresistance',
    'paladin.sanctuary',
    'paladin.holyfreeze',
    'paladin.conversion',
    'paladin.charge',
    'paladin.vengeance',
    'paladin.sacrifice',
    'paladin.smite',
    'paladin.zeal',
    'paladin.holyshield',
    'paladin.fistoftheheavens',
    'paladin.holybolt',
    'paladin.holyfire','paladin.holyshock',
    'paladin.concentration','paladin.fanaticism',
    'paladin.vigor','paladin.redemption',
    'paladin.resistfire','paladin.resistcold','paladin.resistlightning','paladin.salvation',
    'paladin.might','paladin.defiance','paladin.blessedaim','paladin.prayer','paladin.cleansing','paladin.meditation',
    'necromancer.teeth','necromancer.bonespear','necromancer.bonearmor','necromancer.amplifydamage',
    'necromancer.decrepify','necromancer.lowerresist','necromancer.lifetap','necromancer.attract',
    'necromancer.skeletalmage','necromancer.claygolem','necromancer.golemmastery',
    'necromancer.bloodgolem','necromancer.firegolem','necromancer.dimvision','necromancer.confuse','necromancer.ironmaiden',
    'necromancer.bonewall','necromancer.boneprison',
    'sorceress.staticfield','sorceress.warmth','sorceress.firemastery','sorceress.lightningmastery','sorceress.coldmastery',
    'sorceress.frozenarmor','sorceress.shiverarmor','sorceress.chillingarmor','sorceress.energyshield','sorceress.telekinesis',
    'sorceress.lightning','sorceress.chainlightning','sorceress.thunderstorm','sorceress.enchant','sorceress.chargedbolt','sorceress.blaze','sorceress.firewall','sorceress.meteor','sorceress.blizzard','sorceress.frozenorb','sorceress.hydra'
}
LEGACY = {'necromancer.raiseskeleton','necromancer.skeletonmastery','sorceress.teleport'}
PENDING = {
    'sorceress.inferno':'Pinned synergy expression is balanced and verified. Still needs its distinct SQ channelling sequence, starting/interval mana and flame collision/damage.',
}


def build():
    skills=json.loads((BALANCE/'class-skills.json').read_text())
    protocol=(ROOT/'shared/ClassAbilities.cs').read_text()
    symbols=dict(re.findall(r'new\(CombatAbility\.(\w+), "([^"]+)"',protocol))
    implemented=set()
    for character in ['Necromancer','Sorceress','Paladin','Barbarian']:
        text=(ROOT/f'src/Sanctuary.Content/{character}Combat.cs').read_text()
        expression=re.search(r'public static bool Implemented\(CombatAbility \w+\) => (.*?);',text,re.S).group(1)
        if 'NecromancerEffects.IsCurse' in expression:
            effect=(ROOT/'src/Sanctuary.Content/NecromancerEffects.cs').read_text()
            expression += ' ' + re.search(r'public static bool IsCurse\(CombatAbility \w+\) => (.*?);',effect,re.S).group(1)
        implemented.update(symbols[s] for s in re.findall(r'CombatAbility\.(\w+)',expression))
    result=[]
    for row in skills:
        talent=row['talentId']; present=talent in implemented
        result.append(dict(talentId=talent,name=row['name'],characterClass=row['characterClass'],tree=row['tree'],sourceId=row['sourceId'],
            implementation='handler-present' if present else 'blocked' if talent in BLOCKERS else 'pending',
            verification='partial-native-coverage' if talent in NATIVE_PARTIAL else 'legacy-checks; rerun-needed' if talent in LEGACY else 'build-only' if present else 'not-started',
            complete=False,remaining=BLOCKERS.get(talent,CAVEATS.get(talent,PENDING.get(talent,'Needs dedicated rank/gear, target, resource, lifecycle and actual-client acceptance checks.')))))
    return dict(schemaVersion=1,referenceVersion='3.3.93847',classOrder=ORDER,
        completionRule='A source catalogue or handler is not class completion. All talents need dedicated mechanics/lifecycle checks; approved Sanctuary deviations override source. Missing final art is tracked separately.',
        completedClasses=[],skills=result)


def render(data):
    lines=['# Class skill roadmap','',
        'Mechanics reference: **D2R 3.3.93847, Reign of the Warlock**. See [reference and deviations](skill-mechanical-reference.md). '
        'The user authorized work past individually blocked skills. Missing final art does not block mechanics. '
        '**No class is marked complete at this checkpoint.** Handler counts are implementation coverage, not parity or full verification.','',
        '| Order | Class | Handlers present | Individually blocked | Pending | Class completion |','| --- | --- | ---: | ---: | ---: | --- |']
    for index,character in enumerate(ORDER,1):
        rows=[r for r in data['skills'] if r['characterClass']==character]
        count=lambda status:sum(r['implementation']==status for r in rows)
        lines.append(f'| {index} | {character} | {count("handler-present")} / 30 | {count("blocked")} | {count("pending")} | Open |')
    lines += ['', 'Later-class order is a dependency-based working assumption; only Necromancer-first was required. '
              'Warlock/Witch identity, trees and growth are staged; Warlock skill mechanics have not been implemented.','',
              'Use [the current checkpoint](class-skills-resume-checkpoint.md) for verification evidence, unanswered questions and the next executable work. '
              'Use the independent [effect](skill-effects.md) and [icon](skill-icons.md) registers for the asset phase.','',
              'Shared direct-damage checkpoint: integer FP256 channel reduction/absorb, fractional native damage commitment and '
              'the source less-than-one-life lethal boundary pass native checks and two save/reload cycles. '
              'See [numeric evidence](../tools/reference/damage-rounding-engine-evidence.json). '
              'The resource-load correction also passes the full suite and a nineteen-actor eight-class/legacy reload matrix; '
              'the existing title byte now accepts Warlock\'s eighth achievement bit without inventing its cosmetic prefix. '
              'Negative-MDR overflow and modern event/mode behavior remain open. '
              'Sacrifice prepared-hit/self-cost mechanics are staged with [source/native evidence](../tools/reference/sacrifice-engine-evidence.json); '
              'Smite shield/S1/stun, Zeal timed series and Charge travel/impact phases are staged. Charge has168 compiled source cases plus native phase/resource/input/reload and connected-owner ground-motion coverage; standard mapped-monster knockback now uses the shared source mode/path controller; special species and complete geometry/mode acceptance remain open. Conversion now has its attack/allegiance/AI/reward/expiry paths; higher-level life restoration and unsupported target cases remain explicit gates. Continue unaffected roadmap work while retaining individual limits. '
              'No class completion is claimed. The user-authorized local test installation is updated; current verification and remaining acceptance work are in class-skills-checkpoint-2026-10-23.md.','']
    lines += ['Implementation rule: inspect existing handlers and shared mechanics before adding a new controller. Reuse verified geometry, movement, timing and resource consumers where the source agrees, keeping skill-specific damage, collision and targeting rules separate. Pinned Teeth and Multiple Shot both use server do8; verify/extract that common fan from the existing provisional Teeth implementation when reaching Amazon. Battle Command already reuses the Shout/Battle Orders travelling-ray and proportional-resource paths.','']
    lines += ['Howl source preparation now has1169 bounded compiled cases in [source evidence](../tools/reference/howl-engine-evidence.json). Its native handler uses a separate source AI payload in the shared curse lane: Terror includes initial escape, later threat selection/close combat and a default30-distance idle boundary. Earlier ordinary-Terror/item-fear variants still need their own acceptance before adopting this payload; the native Howl consumer is verified separately.','']
    for character in ORDER:
        lines += ['## '+character,'','| Skill | Source ID | Implementation | Remaining acceptance work or blocker |','| --- | ---: | --- | --- |']
        for row in data['skills']:
            if row['characterClass']!=character:continue
            lines.append(f"| {row['name']} | {row['sourceId']} | {row['implementation']} | {row['remaining']} |")
        lines.append('')
    return '\n'.join(lines)


def assets(kind):
    rows=json.loads((BALANCE/('skill-'+kind+'.json')).read_text())
    lines=['# Skill '+kind,'',
        'This register is independent of mechanical completion. No final assets have been generated or installed in this pass. '
        'Keep the original prompt/source image, transparency, scale/directional frames, timing, client mapping and actual-client verification with each eventual asset.','']
    if kind=='icons':
        lines += ['Text labels are explicitly marked placeholders in the talent/skill panels and hotkey tooltips. '
                  'Each talent needs a unique icon; passives do not need hotkey icons.','',
                  '| Class | Skill | Talent tree icon | Hotkey icon |','| --- | --- | --- | --- |']
        lines += [f"| {r['characterClass']} | {r['name']} | {r['talentIcon']} | {r['hotkeyIcon']} |" for r in rows]
    else:
        lines += ['Existing UO bone/magic-arrow, cloud, curse-marker and summon bodies are temporary placeholders. '
                  'Names, damage type and mechanics are authoritative independently of their placeholder appearance. '
                  'Effect concepts for unimplemented skills are a source-derived backlog, not an implementation claim.','',
                  '| Class | Skill | Effect work | Source cues |','| --- | --- | --- | --- |']
        lines += [f"| {r['characterClass']} | {r['name']} | {r['effects']} | {', '.join(str(v) for v in r['sourceEffects'].values()) or ('Passive; no active effect' if r['effects']=='not-applicable' else 'No source effect cue; final presentation pending')} |" for r in rows]
        lines += ['', 'Smite currently uses ordinary UO weapon animation/sound and native get-hit animation20 as identified placeholders; final shield impact, stun and knockback presentation are pending. '
                  'Zeal currently uses ordinary UO weapon animation/sound for its timed series and approved low-mana ordinary attacks; final series accents and impacts are pending. '
                  'Both still need talent-tree and hotkey icons in the independent icon register.']
    return '\n'.join(lines)+'\n'


def main():
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');args=p.parse_args();data=build()
    products={BALANCE/'class-skill-status.json':json.dumps(data,indent=2)+'\n',ROOT/'docs/class-skill-roadmap.md':render(data),
              ROOT/'docs/skill-icons.md':assets('icons'),ROOT/'docs/skill-effects.md':assets('effects')}
    for path,text in products.items():
        if args.check:
            if path.read_text()!=text:raise SystemExit('Stale skill roadmap: '+str(path))
        else:path.write_text(text)
    print('Class handler coverage, blockers, effects and icons recorded separately.')


if __name__=='__main__':main()

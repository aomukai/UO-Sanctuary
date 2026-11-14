# Goatman ordered NPC contact binding

Reference: owned D2R Reign of the Warlock **3.3.93847**, with the approved [Goatman action rule](goatman-npc-action-proposal.md). Implemented and focused-native verified. Summon Goatman is not yet admitted as a learned skill.

The actual captured Goatman body now supplies unenhanced physical endpoints plus its captured damage percentage through the existing unarmed-NPC interface. Its saved enhanced display endpoints are never enhanced again. Ordinary basic attacks reuse the existing unarmed allocator and ordered contact pipeline. Its four internal NPC skills use their own locked ranks, hit/accuracy values, Stun/Berserk conversion and the previously qualified intrinsic-critical order, then the same native HP/event/return receiver.

Captured melee-only Crushing Blow registers the existing stat136/function16 event, including its original five-percent base and hard Death Mark bonus. No duplicate direct damage or item event is introduced; the known domeleedamage aura does not gain a missile proc. Actor RNG uses the existing persisted zero-item stream; the controlling player’s current item affixes, weapon/mastery stats, live skill ranks and poison/pierce gear are not inherited. Current Enchant and weapon-element auras remain actual effects on the demon. Unsupported equipped-item, extra magic-channel and nonzero duplicate-sunder contexts refuse explicitly.

The prepared world-service binding uses existing safety/hostility, source reach/footprints, LOS, recipient capability checks and ordered NPC damage. It requires an online living owner for automatic action delivery and stops when that context is inactive. Stun/Berserk allocate at their existing start boundary; Berserk begins its timed self-state and refreshes through the shared release helper. Frenzy/Cleave allocate on release under the approved action adaptation. This binds those leaves; it does not replace the source GenericPet AI.

The action constructor now schedules its animation at local frame0 and offsets the resulting release/end events into the world clock. This removes the fixed-point animation epoch’s roughly four-day absolute-frame limit. Exhausted integer timelines refuse before mutating body/RNG state. No animation duration, rate curve, input payment or charge rule changes.

## Remaining work

The automatic GenericPet root/entry/search/follow/path/mode/event host, durable world clock/rearm and complete learned summon/cap transaction remain unfinished. Walking is qualified separately; other movement/animation mode selection still needs actual source context. Prepared contact fixtures cannot certify the complete host. Native uninterruptible/unique-return and unknown special-recipient contexts retain the existing explicit refusals.

Effects/audio and icons remain separate. The Gargoyle body is reused; no new body artwork is needed. Installed build/configuration/saves and native/client binaries remain protected.

## Verification checkpoint

Twenty-one native markers pass across three authenticated sessions, three disposable boots, two reloads and three saves. Actual captured Goatman bodies deliver Stun/Berserk/Frenzy/Cleave and basic attacks through ordinary native damage. Controls verify captured melee Crushing Blow and exact one-roll consumption, one HP notification, no allocation-time health change, no duplicate release, Stun on survivors, Berserk timed startup/magic conversion, misses, release-time range rejection and no missile Crushing Blow. Real owner level/stat/gear/hard-point changes leave birth offense and captured proc chance unchanged.

The prepared online world action uses those real services, becomes inactive when the owner disconnects and accepts frame20000000 through a local animation epoch. Exhausted clocks refuse before projecting saved Frenzy expiry or consuming RNG. Original parked wounded demons preserve exact body JSON and their next combat random roll across reloads. Existing body/Frenzy/action and original item/resource fixtures pass in the same cohort. These checks establish the listed bindings; they do not certify the missing automatic host, learned cast or every special receiver.

Accepted artifacts: content5/integration6, source snapshot7, native run4. Earlier runs are retained. Two fixture failures were caused by helmet Strength and Sanctuary’s level60 Exceptional floor; the fixture spends/levels its owner normally. Console acknowledgment now precedes immediate connected setup. The first combat run passed all four skill contacts and exposed an inappropriate Revive-only admission call in the basic adapter; it now calls the generic unarmed allocator and keeps that Revive guard intact. A shared preflight also rejects exhausted frames before expiry projection, and duplicate contacts cannot alter Berserk’s self-state.

Receipt: `tools/reference/goatman-npc-contact-native-evidence.json`. Native/client binaries were reused;106 installed artifacts,38 saves,20 configuration files and43 client-patch files match their protected reference. No installed files were replaced. No new timer, persistence ID or save version was introduced.

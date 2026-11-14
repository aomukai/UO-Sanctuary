# Goatman persistent NPC action binding

Reference: D2R Reign of the Warlock **3.3.93847**, with the approved [NPC action rule](goatman-npc-action-proposal.md). Status: implemented and focused-native verified. Learned Summon Goatman and automatic GenericPet AI remain unfinished.

`GoatmanBodyState` now optionally retains Frenzy rank, charges and an absolute UTC expiry inside the existing serialized string. Old saves omit the field and retain their exact JSON representation. This adds no persistence ID, save version or timer. Reads and parking never refresh expiry; expired records provide no bonus. Invalid ranks/counts/timezones are rejected before replacing the body state.

`GoatmanNpcBodyActions.OpenPrepared` derives the NPC skill/rank from the original summon snapshot, restores its prior charges into the shared Frenzy writer and captures its actual attack clock. A successful contact writes the updated charge state/deadline back to that same body. Gear changes and respecs do not rewrite its original rank/passives. UTC/source-frame conversion preserves exact saved subframe deadlines unless a new successful hit legitimately refreshes them.

Body attack timing combines the captured birth attack rate, shared current buffs/slows and its own Frenzy once. Movement rate feeds the existing source motion kernel using the owned Goatman velocity8 and `GMWL2HS` animation. This currently binds walking; other movement-mode selection still belongs to the host. Other-animation rate uses the existing15–175 clamp only when the caller qualifies a mode that uses it; no universal mode flag is invented, and movement does not add the same other-animation bonus again.

## Remaining host work

This is a concrete persistent-body/action binding, not automatic summon admission. The host still needs the actual GenericPet source AI/path/clock/event adapters, movement transactions, ordered contact allocation and events, death/rearm behavior, summon cap and learned cast. Existing prepared Stun/Berserk and approved Frenzy/Cleave must all be wired before the summon counts as implemented. The new test world still declares its contact/eligibility leaves explicitly.

Final effects/audio and icons stay in the existing separate Summon Goatman asset registers. Installed build, native/client binaries, configuration and saves are protected.

## Verification

Eighteen native markers pass across three disposable boots, two reloads and three saves. The original Warlock/Witch demons retain exact saved JSON, charge counts, UTC deadlines,257/256-life wounds and owner resources. Tests cover successive real body actions, snapshot preservation after gear/rank changes, one-time speed aggregation, the source walking kernel, caller-qualified other-animation rates, subframe expiry, misses, fresh charges after expiry, parking and atomic invalid-state refusal. Legacy absent-field bodies and the approved action controller run in the same focused cohort. This verifies these bindings; it does not certify a complete playable summon or the missing production world services.

Accepted artifacts: content2/integration3, source snapshot3, native run2. The first fixture incorrectly searched all JSON for “Frenzy,” which matched the existing nested “FrenzyRank” supplier. It now checks the top-level optional property. Its failed log/source/binaries are retained; production serialization required no correction. The separate asset-description correction was included in the final build/input snapshot.

Receipt: `tools/reference/goatman-npc-body-action-native-evidence.json`. Native/client binaries were reused;106 installed artifacts,38 saves,20 configuration files and43 client-patch files match the protected reference. No installed files were replaced.

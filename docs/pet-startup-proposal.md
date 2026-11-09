# Proposed normal-demon AI startup

**Status: awaiting user approval.** Prepared and tested on 2026-11-09 JST. Mechanical reference: **D2R Reign of the Warlock 3.3.93847**. This is an explicit Sanctuary design proposal, not a claim that the current D2R constructor has been recovered. Coverage remains **213/240 handlers, Warlock 4/30; no class certified**.

The linked current summon callback do56 is protected. Its control producer and startup values remain unknown. The normal AI entry, descriptor selection, root, preprocessing, post-target stage and special callbacks have separate current-source qualifications. This proposal defines the missing initial control values for **new Goatman, Tainted and Defiler summons only**, so work can proceed without assigning meaning to inaccessible code.

| New-demon field | Proposed value and behavior |
| --- | --- |
| AI state | 0: use the ordinary class descriptor |
| Default think function | Read the actual compiled descriptor; these three species resolve to GenericPet row 148/type 0/`4b60c0` |
| Raw header word +10 | 0 |
| Raw override index +14 | 2: no special override selected at creation |
| AI flags +38 | 0 |
| Early-follow target +3c/+40 | ID −1, kind 1; no early-follow target |
| Raw linked field +90 | 0 |
| Remembered skill / repeat / target-bias words | −1 / 0 / 0: no previously selected skill and fresh counters |
| Pending AI queue | Empty, with executing false |
| Stored target command | Target 0/type 0: no initial player/enemy command |
| Owner | Explicit native world context, player registry kind 0 and that owner's serial |

The code verifies the actual three species and descriptor before creating a draft, and rejects missing owner/world identities or other species. Separate review runtimes have independent mutable queues and targets. The tuple −1/0/0 also appears in a separately verified scratch initializer; that does **not** prove it is the current summon constructor's value. The starting tuple and other initial values above are proposed choices. The known source establishes how these values are consumed, not that D2R creates them this way.

The proposed application is limited to new births. It does not reset surviving pets, recalculate their birth snapshots, heal wounds, refresh temporary effects or manufacture control state for older saves. Existing known controllers retain their values; existing unknown controllers remain unknown. Approval would cover these startup values only. It would not approve an automatic clock, event-chain selection, world geometry, target search, action execution, native death mapping, changes to pet caps or a save migration. Later status/AI changes must still use their qualified or separately approved rules.

`GenericPetStartupProposal.Approved` is false, and its admission guard refuses. The review-only factory has no production birth or load caller. Tests explicitly install its draft into **additional disposable original demon actors**. Learned summons and automatic AI remain gated. Production rollout and persistent live-header bindings are subsequent work; the review manifest is not a production header-save format.

**Verified review checkpoint:** accepted **Content 2 / Integration 2 / snapshot 3 / native 2** passed **15 checks over three boots, two reloads and three saves**, with zero build warnings/errors. The additional Goatman, Tainted and Defiler actors use their actual class metadata, native owner registry, registered initializer, full prepared AI entry and existing normal root/combat policy. Their empty first decision does not request a remembered skill, invent a target/attack or invoke a special override. The source world/path/search/follow inputs remain explicit review fixtures. The tests preserve birth text, fractional wounds, mana, registered commands and control words through reload. The normal pre-existing original-demon cohort also passes unchanged. Refusals cover the pending approval gate, invalid identities/species, and mutation isolation; drafting alone leaves unknown controller state untouched.

The initial Content review made the empty stored command explicit and used the existing null-selection finish path. Initial review files and Content 1 are retained under `pre-review-1`. The first Integration attempt had a record-initializer punctuation typo; the corrected test is the only difference between snapshots 2 and 3, so Content 2 was reused. The first native launcher found the missing Integration DLL and exited before server startup. Exact diagnostics are retained under `compile-diagnostics-1`. The first actual server cohort is native 2 and passes. No reference code was reread and no old large offline matrices were rerun for this proposal.

[Native review evidence](../tools/reference/pet-startup-proposal-native-evidence.json) authenticates the inputs, scope and installation preservation. The installed build, configuration, saves and all 43 current client patches remain untouched. This control proposal needs no new effect/audio/icon assets; the separate skill asset backlogs remain open.

**Decision requested:** adopt these initial values as an explicit Sanctuary adaptation for newly created normal demons, while preserving existing save/controller state. If approved, bind the startup factory to a qualified new-birth path and continue the remaining world/controller work. If not approved, retain the gate and keep the current constructor gap open.

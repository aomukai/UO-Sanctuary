# Necromancer AI curse preparation

Reference: **D2R Reign of the Warlock 3.3.93847**; approved Sanctuary deviations take precedence. This is source preparation, not native implementation or class acceptance.

`python3 tools/probe_necromancer_ai_curse.py --check` verifies [12,000 complete do30/receiver/factory contexts](../tools/reference/necromancer-ai-curse-source-evidence.json) under ASan/UBSan. It reuses the hash-checked accepted Amplify Damage receiver scaffold without running its producer or overwriting its artifacts. New complete bodies are checked against the pinned legacy source tree. The current skill/state tables pin the inputs below.

| Skill | Mana | Source radius | Base duration in 25-fps frames | State / special AI |
| --- | --- | --- | --- | --- |
| Dim Vision | 9 | `4 + (rank − 1)` | `175 + 50 × (rank − 1)` | 23 / 10 |
| Terror | 7 | 4 | `200 + 25 × (rank − 1)` | 56 / 11 |

Both use do30, filter2 and no primary aura stat. Do30 divides their base duration by the difficulty's nonzero `AiCurseDivisor` before the shared curse factory applies curse resistance. A missing difficulty record or zero divisor leaves base duration unchanged. Independent immutable [d2data JSON](https://raw.githubusercontent.com/blizzhackers/d2data/fa1286949266498b294c40c2942521a7c0d7207a/json/difficultylevels.json) and [D2R-Excel 3.3.93847 TSV](https://raw.githubusercontent.com/pinkufairy/D2R-Excel/1f16064e09b97e3e65abd6943662207cff00b07f/difficultylevels.txt) extractions now corroborate **Normal 1 / Nightmare 2 / Hell 4**, with both URLs and file hashes in the receipt. The missing/zero-record cases remain synthetic service controls. Table corroboration does not establish current-executable AI behavior or native facet integration.

The receiver checks monster/non-neutral status and an AI-permission service before ordinary eligibility and curse creation. Only a successful factory result dispatches the special AI. Equal-rank refresh dispatches again while retaining the original curse creator; weaker same-skill rank, full curse resistance, Attract, neutral alignment and denied AI permission do not dispatch. No resistance/damage stat is manufactured for either skill.

This application contract differs from the older Howl helper, which refuses an already-Terror state and requests transient AI switching without checking the curse factory's returned pointer. Reuse the common Terror **tick** evidence and world services, not Howl's application contract. The native `SourceTerror` currently emits a Howl-tagged payload with stored curse rank 1; ordinary Necromancer Terror still falls back to unconditional motion away from its original cast point. Neither establishes the learned Terror behavior above.

Implementation work remains: connect learned casting to the shared rank/caster receiver; apply the corroborated difficulty divisors through the configured facet policy; preserve separate curse and transient-AI state transitions; extend save validation for a genuine Necromancer Terror payload; verify eligibility and special-state initialization/removal, natural movement and reapplication; retain Howl/Taunt/Dim Vision/Cloak interactions and real input/save/reload checks. Current `SourceTerror.Supported` intentionally covers only mapped Skeleton/Zombie/Megademon AIs with traced exit behavior; unsupported species must remain named rather than gaining a guessed AI conversion.

The harness declares AI permission, dispatch, formula evaluation, radial traversal, alignment, stat storage and event/clock services. It does not execute the complete eligibility/init/tick/world engine or the current executable. Exact radius mapping, species/flags, room filtering, PvP and graphical-client behavior remain separate. Confuse and Attract have distinct do61/do59 entry points and are not certified by this do30 preparation. No runtime data, installed assembly, save schema, handler count or art asset changes are made here.

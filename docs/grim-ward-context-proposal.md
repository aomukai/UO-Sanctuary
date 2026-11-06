# Proposed Grim Ward context rule

Status: **approved by the user**. The exact answer is saved in [the decision record](../tools/reference/grim-ward-context-decision.json). This is a Sanctuary adaptation, not verified D2R executable behavior. Learned casting still requires implementation and verification.

Reference: D2R Reign of the Warlock **3.3.93847**. The existing lifecycle, curse operation and actual-recipient pulse components remain the implementation basis. The unresolved question is how the modern body missile obtains its owner's hard Find Potion points and initializes fear.

Approved rule: capture effective Grim Ward rank and hard Find Potion points when the paid cast releases. Every pulse uses that captured context, with the ward's own position as the area center. The ward remains the curse creator; affected monsters initially flee from the casting Barbarian through the shared Terror AI, then use that AI's ordinary threat reacquisition. Equipment changes and respec do not rewrite an already-created ward's packet. This extends the approved summon/trap snapshot preference explicitly to this stationary skill missile.

Keep the pinned numeric fields: 4 mana on successful start, soft/selectable corpse, three source size variants, 5-frame small or 8-frame medium/large startup, 1,000-frame active body, pulses at elapsed 0, 6, …, 996, pinned radius/slow and physical resistance reduction including hard Find Potion's 5 percentage points per rank. Reuse the shared curse duration, replacement, immunity, attack-rate and movement consumers. `other_animrate` remains stored with its unsupported nonattack consumers identified; it must not be applied indiscriminately to casting, block or hit recovery.

World adapters still need implementation and tests: source-compatible free-cell placement, corpse reservation while retaining existing loot access, child insertion/clock, original saved state and owner lifecycle. Existing approved stationary-trap policy supplies staying in the original facet, no combat while the owner is offline/elsewhere and lifetime continuing. Their exact D2R counterparts remain unverified. No new art is required to implement these mechanics: start/body/end/fear/audio placeholders and talent/hotkey icons stay separately identified.

The prior source and native receipts retain their narrower explicit-context scope. The approval resolves the two context choices. It does not retrospectively certify the old engine as current D2R or mark Barbarian complete.

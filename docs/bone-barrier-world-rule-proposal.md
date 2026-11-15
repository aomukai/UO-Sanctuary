# Bone Wall and Bone Prison: complete barrier-world adaptation

Reference: **D2R Reign of the Warlock 3.3.93847**, with approved Sanctuary deviations first. **Approved by the user on 2026-11-16 JST.** [Exact decision](../tools/reference/bone-barrier-world-rule-approval.json). The body below is the approved proposal; learned acceptance is recorded separately. This proposal defines the unresolved native world, linked-death and removal behavior together. It does not claim to recover protected current D2R owner flags or modifier44.

## Keep the existing action and birth contracts

Retain the existing learned SC/FCR action, unpaid windup, cancellation and once-only release settlement, captured rank/synergy/difficulty/resource recipes and current owner-based allegiance. Retain the already-approved two-tile cursor acquisition for Bone Prison, including its locked primary and target-loss behavior. Wall allocation failure and Prison zero/partial allocation keep their existing distinct settlement rules. Existing approved facet scaling preserves life percentage without recapturing summon stats.

Adopt the existing native placement and geometry as an explicit Sanctuary world mapping: exact space first, then the shared bounded nearby placement search; growing Wall children keep their no-retry route. Wall uses its two saved missile-maker arms and existing child budget. Prison uses the existing twelve source-ordered anchors and cyclic raster connectors that close native tile gaps. Existing terrain, height, town, target and room/LOS adapters remain in force. This chooses the tested UO mapping rather than claiming exact D2 room/collision coordinates or a recovered low-level spawn initializer.

## Linked collapse and collision

Treat each cast as one formation. **Destroying any segment, or the first member reaching its AI death event, starts collapse for every surviving segment in that formation.** This deliberately resolves the old native asymmetry where destroying a child opened only its own gap but destroying the root removed its nearby children. It is a Sanctuary choice informed by the older KillMinions composition, not proof of current D2R behavior.

At collapse entry, publish the same captured formation identity, entry time, completion time and cause on every member before changing collision or presentation. Immediately release all collision tiles; members become nonliving, non-damageable and ineligible for projectile interception. Stop growth and regeneration. Repeated hits or expiry callbacks cannot restart or extend collapse. There is no death damage, ordinary loot, corpse, combat proc or new reward from this transition.

Use the owned BW death animation's **15 frames at speed256** with the existing sentry completion calculation: **600 milliseconds**. Keep the original entities during that interval for collapse presentation. The first existing 40ms timer callback at or after the shared completion deadline removes the whole formation. Removal follows explicit links, including children beyond the former sixteen-tile cleanup scan. Do not transfer ownership to a successor or emulate the older unsafe flag/pool branches.

Normal lifetime entry still uses the accepted source-derived think schedule: first observation25, fifteen-frame idles, strict deadline600 comparison; an uninterrupted schedule requests death at610. Collapse starts at the actual observed callback time, not a manufactured earlier frame. Each segment retains its existing captured birth clock; the first death request collapses the formation.

Administrative root deletion and active invalid creator/map cleanup retain immediate cleanup rather than manufacturing combat death or a new animation; an already pending collapse keeps its original finite deadline. Explicit child links prevent root cleanup from leaving a distant child. Existing owner logout/facet/death dispatch remains the shared world route; this proposal adds no new parked roster, healing service or owner proxy.

## Saving and compatibility

New saved subtypes opt into this rule. Existing plain, prepared and thinking barrier layouts and their historical behavior remain readable and unchanged; there is no serializer-version bump or replacement of an already-saved actor. New Wall children inherit the formation's retirement-capable type.

Persist the exact UTC formation entry/completion/cause alongside existing identity, owner/root links, birth/AI clocks, resources and maker state. Reject malformed/non-UTC retirement metadata before mutating state. Pending collapse must not recreate collision, resume growth/healing or receive a fresh deadline after loading. Downtime counts toward expiry; an overdue collapse finishes on the next ordinary timer callback without attacks or catch-up growth. No extra timer or global persistence profile is introduced.

## Verification and assets

The prepared candidate uses a separately selected allocator; the ordinary learned gate stays unchanged until approval. Verification combines retained barrier birth/cast/maker controls with actual candidate public input, hostile projectile interception before and after collapse, real child death, whole-formation and distant-child cleanup, idempotence, exact completion boundaries and saved typed formations. The [checkpoint](bone-barrier-retirement-checkpoint.md) records final build/run hashes and limits.

Saved in-progress collapse fixtures use explicitly inspected future timestamps so an inspection is not confused with winning a race against a600ms save window. Natural public-input controls separately test actual death/expiry and timed removal. A separate save hook writes actual newly entered600ms collapses immediately before world saving; post-load checks verify the original metadata and automatic overdue removal. Historical byte upgrades, unheld save concurrency and graphical rendering remain unverified; those limits are not presented as passing tests.

Current animation/tile/cast markers are clearly identified native placeholders. Final Wall/Prison growth, hit/collapse/removal and sound effects remain in [effects](skill-effects.md); talent and hotkey icons remain in [icons](skill-icons.md). No artwork, client patch, new live reference read or installed deployment is part of this candidate.

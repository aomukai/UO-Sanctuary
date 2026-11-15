# Complete Miasma Chain world rule

**Approved by the user on2026-11-15 JST.** [Exact decision](../tools/reference/miasma-chain-world-rule-approval.json). The body below is the original approved proposal;implementation/acceptance status is recorded separately.

**Original proposal.** This is one complete Sanctuary adaptation for the current unavailable do43/do45/hit41/do41 world callbacks and startup. It preserves the pinned **D2R Reign of the Warlock3.3.93847** data and approved common mechanics;it does not claim those callbacks were recovered. No newer3.4process or CASC data is substituted. Learned Chain remains gated until approval and separate real-input verification.

## Reference and reuse

The owned graph is **711maker →712moving/attached link →713cloud maker →696magic cloud**. The current launch request producer and Chain-specific numeric inputs already have independent evidence. Registered skillcalc54`len`reads `auralencalc`,giving the link125frames;this is distinct from elemental duration. The cap formula is `min(5+effective rank/4,10)`.

[Blizzard's released3.2notes](https://news.blizzard.com/en-us/article/24261478/diablo-ii-resurrected-ladder-season-14-has-concluded) establish the removed casting delay,5–10chain limit,half-second cloud stamps,one-second recipient delay within each chain,independence across chains,and fixes for extra clouds and Blade Warp breaks. They do not establish our native attachment,first stamp,area geometry or cleanup choices below.

Reuse the accepted Miasma cloud scanner,typed magic/potion mitigation,recipient leases,source vectors/cell stepping,SC action and saved ownership services. Chain differs from Bolt:its cloud696has CanSlow/GetHit enabled,and its cloud damage is applied along a link. Blade Sentinel supplies useful ownership/path patterns;its weapon fraction,AI,reversal and lifetime are not imported.

## Action,capture and resource rule

Use the owned human Warlock SC16/action8 animation with the shared FCR clock. The older normal-CAST caller prioritizes FCR before attack-rate handling;the current Warlock caller remains unavailable. Therefore this explicitly chooses SC/FCR despite UseAttackRate1 and does not invent an SQ12sequence from the unused sequence-number field.

Accept a trained living Warlock/Witch in an allowed form,active connection,valid facet and unsafe combat area. Unarmed casting works. Accept hostile supported NPC targeting or clicked ground,using normal range/LOS plus a maximum40-source-unit initial link distance. No extra cursor-nearest targeting is added. A living selected enemy's position is refreshed at release;an enemy lost during windup leaves the original ground aim. Moving,being disabled,changing the equipped weapon,death or interruption cancels the unpaid windup.

At successful release,snapshot effective Chain/Entropy ranks,hard Bolt/Abyss synergy points,shape/endpoints,magic mastery,potion/elemental mastery and Slow Missiles state. Later gear,training and respec do not rewrite that release. Charge the pinned fractional cost once:4.5mana at rank1,5at rank2,and the table progression thereafter. Insufficient mana creates no actor and makes no ordinary attack. Failures before payment preserve the roster;released resources are not refunded. The D2 launch request remains separately captured;Sanctuary maps action completion through the shared SC state rather than inventing an extra native entity flag.

The new paid chain replaces the oldest live chain when its captured cap requires it,ordered by creation UTC then ID. Count all of that owner's unexpired links,including inactive facets. **Already-emitted clouds from a replaced or naturally expired link keep their own expiry. The live-link cap does not also cap lingering cloud groups.** That retention and replacement order are explicit choices,not recovered D2R behavior.

## Link and stamp rule

- Release creates the711maker. After one40ms update it becomes the712head/link. Link lifetime is125further frames,five seconds;total release-to-link-expiry is5.04seconds. The first head movement follows on its own update. InitSteps12 is not treated as twelve historical damaging steps at creation.
- The head travels with owned velocity35 through the shared source factory/vector projection,including captured Slow Missiles velocity. It aims at the selected living hostile NPC's current position,or the retained ground point. Sweep source cells in order and supported NPCs by serial,using the owned size2footprint. A wall or invalid movement location cuts the link. No obstacle pathfinding or Blade Sentinel reversal is invented.
- The first eligible monster intercepted receives one direct magic spell contact and becomes the attached endpoint. Commit attachment before damage callbacks. Use existing missile avoidance and return-fire hooks;there is no weapon attack-rating roll,weapon shield-component roll,weapon damage/wear/proc or weapon leech. Head GetHit requests hit recovery;SoftHit is not mapped to an additional accuracy or recovery bypass under this adaptation. Avoidance still leaves the endpoint attached. Ground arrival attaches without a direct hit. No repeating direct damage is applied along the tether.
- An attached living hostile endpoint follows that monster. If it dies,is deleted,changes facets or ceases to be eligible,retain the last valid point for the remaining original lifetime. It can resume following that same eligible monster if it becomes valid again;there is no search for a replacement enemy.
- The source endpoint follows the caster. Same-facet walking or teleporting,including Blade Warp,does not cancel the released link merely for moving. Clip the effective span to40source units with the shared port's octagonal distance scale. When preparing a stamp,an obstructed span cuts the link;cross-facet/inactive behavior is described below. Retain the original height plane;an owner-height difference of16or more suspends activity.
- Make the first stamp when the711maker becomes a link,using its current extent,which can initially be zero length. Subsequent stamp deadlines are500ms apart. Each stamp creates a713maker that ages one40ms step,then creates one stationary696field with frozen endpoints. There is no damaging cloud at the713queue stage. These first-stamp/maker delays are explicit adaptations.
- A late update produces at most one current stamp,skips missed stamp opportunities and keeps the original half-second deadline phase. It does not generate historical fields at old target positions. Head movement can catch up through retained40ms steps against the present world. At or after link expiry,do not emit a terminal stamp or direct hit.

## Cloud area and damage rule

Use a fixed capsule-like area between each stamp's frozen endpoints:project each candidate to its nearest point on that segment,round to the existing source grid,and apply the shared circular radius test and nearest-point LOS/height/safety eligibility. It is one saved field per stamp,rather than many overlapping damage actors. Effective Entropy gives radius2/3/4at effective ranks0/5/10 and lifetime `50+min(effective rank*5,100)`frames,starting when the696field is born.

**This proposal uses effective radius as the sole damage width. The separate hard-only SrvCalc1 value2/3/4at hard ranks0/10/20 is captured and validated,but its unavailable layer meaning is not applied as an additional radius or damage modifier.** This is an explicit area adaptation,not an inferred interpretation of do41. The value remains available for later source research.

Scan present occupants every40ms,including field birth. Before damage,claim a one-second recipient lease shared by all stamps of the same chain;different chains have independent leases. The direct head contact does not consume that cloud lease. Each accepted cloud contact rolls its own captured magic endpoints. An entrant can be hit at the next scan. A fully resisted or zero-damage contact still claims its delay.

Apply captured magic mastery before the random roll and captured Slow Missiles damage to both head and cloud696. Add captured Magical Penetration elements through the shared spell helper. Use current target defenses,absorb and caster resistance-piercing effects when damage lands. Magic immunity blocks magic while added elements follow their own defenses. These mastery/pierce/roll timing choices are adaptations shared with the accepted magic pipeline.

Cloud contacts use typed direct-spell damage events and applicable existing reactions,such as Health Link/Hex consumers. Cloud GetHit requests hit recovery;clouds do not repeat the head's avoidance or return-fire roll. There is no poison status,cure interaction,extra explosion or damage on expiry. Fields have half-open lifetime:at expiry they are removed before scanning. The original supported ordinary-recipient boundary remains;unmapped,special,PvP and class-pet contexts are not silently generalized.

## Lifecycle and saving

On logout,facet travel or safe-area entry,retain the link and fields at their saved state,stop activity and let original UTC lifetimes run. Height suspension pauses the link;already-emitted fields keep the common same-facet activity rule. Advance past inactive frame/stamp opportunities without damage or backfill. Pending713makers keep aging;any resulting field retains its originally scheduled birth/expiry and cannot damage while its owner is inactive. Eligible return resumes only remaining lifetime. There is no healing,refill or reset. Owner death,deletion or class-history switching removes the entire owned graph;respec preserves releases.

The existing Miasma persistence profile254/version0 adds optional link and713maker arrays plus optional cloud path/slow metadata. Old three-field Bolt graphs still load. Save original IDs,owner/facet/target,last endpoint,positions,stage,rank/shape/packet,seed and clocks. A shared monotonic ID source covers Bolt and Chain pools. Validate all pools,ownership/producer consistency,IDs,numeric domains and UTC clocks before committing any restored state. Missing/dead endpoint entities follow the retained-point rule. Do not restore transient casting actions.

Prepared Chain has no public binding or automatic caller;the accepted Bolt clock excludes its fields until approval. After approval,join the existing clock and verify real left/right/quick inputs before increasing coverage. No new timer or persistence profile is needed. Full Enhanced Entropy remains incomplete until its remaining consumers,including Abyss,are implemented.

Native sound0x1E5,linked-end0x37C4,cloud0x374A and contact0x375A with hue0x48D are identified placeholders. Final cast/head/tether/stamp/contact/expiry effects and sound remain separate from talent/hotkey icons. No imagegen or installed deployment accompanies this candidate. Native tests are not graphical playtesting or a full-class certificate.

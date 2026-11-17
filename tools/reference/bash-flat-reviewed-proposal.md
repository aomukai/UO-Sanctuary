# Bash flat-damage proposal

Status: tested candidate, awaiting a Sanctuary design decision. Reference remains D2R Reign of the Warlock 3.3.93847. Learned Bash and the installed build are unchanged.

The pinned skill table defines `calc2` as flat damage: 1 at effective rank 1, 20 at rank 20. The preserved older startup appends this amount to the physical event field after allocation has stored total damage. Its health callback commits that earlier total. Sanctuary currently retains this explicit old behavior, so the extra field alone does not increase life loss. The saved source proves the older ordering; it does not prove the current D2R callback. No running-client test is requested from the user, who does not play Barbarian.

Proposed Sanctuary rule:

1. Capture Bash's flat `calc2` amount with the ordinary attack packet.
2. On a successful, unblocked contact, add it after weapon percentage damage and weapon critical/deadly-strike multiplication, before shared damage mitigation and absorption. The flat addition receives neither weapon percentage multiplication nor critical doubling.
3. Carry the mitigated result in both the physical event lane and the life-damage budget. Physical resistance and immunity apply normally; the addition cannot bypass them. Shared physical-damage consumers use that consistent result.
4. A miss, dodge or block adds nothing. Keep the existing capture/release timing, knockback, mana payment, ordinary underfunded fallback, cancellation, equipment and poison rules. Do not add RNG draws or change Stun, Concentrate or Berserk.

For a captured 100 physical weapon contribution and rank-20 Bash: the candidate is 120 before mitigation, or 220 when that weapon contribution critically doubles. At 50% physical resistance, these lose 60 or 110 life respectively. Physical immunity takes zero. These are isolated examples after the weapon's percentage calculation, not character-sheet predictions.

The candidate reuses `GearEffects.PrepareWeaponImpact`'s existing `physicalAfterCritical` hook, already used by Hunger, and the ordinary player contact/release services. The test uses an explicit 100-point weapon contribution to isolate ordering; the proposed learned Bash wiring is still pending. Sixteen distinct rank/resistance/critical combinations verify typed damage and actual native life loss, with miss controls. The generic runner repeats these across three boots and retains its ordinary Iron birth/item/resource prerequisites: 12 result markers, three saves and two reloads. It does not verify new learned Bash input, all shield/avoidance cases, leech/proc combinations or saved pending-candidate actions. Those require focused qualification when the approved rule is wired.

Both production binaries and numerical data remain unchanged. Only a test fixture, driver and evidence are added. Separate talent/hotkey icons and effects remain existing placeholders; this proposal creates no new asset requirement. The [candidate receipt](../tools/reference/bash-flat-candidate-evidence.json) binds the exact proposal, source, artifacts and results.

Decision requested: adopt this post-critical, pre-mitigation flat-damage rule as an explicit Sanctuary adaptation. Until approved, retain existing learned behavior and continue unaffected skills.

# T36 — Chitin and maximum-durability restoration

Staged T49 update (22 October2026): actual eligible named items now share this path. The [complete return list](named-item-readiness.md) records every held definition and its prerequisites; unavailable outcomes retain their original weight. These changes are isolated staging; production remains T45.

Wild Solen, Terathans, spiders and scorpions have one separate 10% chance to drop one chitin. The same chance applies in Normal, Nightmare and Hell, independently of Magic Find and encounter grade. The configured 19 native family roots include their derived species. Summons, controlled pets, animated/raised creatures, vendors and reward-disabled lives remain excluded.

Chitin uses the existing saved reward-life claim, alongside ordinary treasure, moulds and XP. Killing or reviving the same claimed life cannot pay it again. A qualifying arachnid outside the ordinary gold/gear roster can award chitin without enabling its other rewards.

## Alchemy

At a public Alchemy workshop, select the active Alchemy profession, use a mortar and pestle and choose **Powder of Fortifying** from the Alchemy book. Confirming the recipe starts ordinary timed crafting.

- Requires Alchemy 80 and 10 chitin; produces one single-use powder. No bottle.
- Uses the existing required-skill-to-+50 success and gain curve: 0% at 80, 20% at 90 and 80% at 120. Skill locks and caps still apply.
- Success consumes 10 chitin; failure consumes 5. Either completed outcome spends one mortar use, with existing crafted-tool saving.
- Cancelled, stale, remote, disconnected or otherwise invalid work spends nothing. A full backpack cannot lose ingredients to an undeliverable success.

## Fortifying equipment

Double-click the powder, target identified equipment in your own backpack and confirm the preview. Each dose adds up to **5 maximum durability**, clamped to the native type's natural maximum, including that item's saved crafted durability bonus. The final dose can add fewer than five. Current durability remains unchanged, including when it is zero; ordinary repair fills it afterward and retains its normal costs and maximum-loss risk.

An item that originally rolled below its type's natural maximum can reach that ceiling. Naturally non-wearing jewellery, footwear and cosmetic clothing gain no artificial durability. Equipment already at or above its ceiling cannot consume powder. Fortifying does not change affixes, sockets, quality, creator or identification state.

Existing native Powder of Fortifying routes through the same confirmation for class characters: each remaining legacy charge supplies one maximum-only application. Non-class native behavior stays intact. Native and Sanctuary powder cannot bypass backpack, identification, movement-only or stale-request checks. A quote becomes invalid if the equipment, powder, connection or relevant state changes; it cannot execute twice. Fortifying also invalidates a pending ordinary repair quote through its changed durability state.

## Configuration and verification

`data/Sanctuary/Balance/fortification.json` owns species, probability, recipe and restoration values. The resource and powder save their stack quantities and origins through native serialization. No existing equipment save format changes.

`FortificationChecks` exercises exact drop boundaries, all configured families and difficulties, excluded/duplicate reward lives, crafted ceilings, broken and partially restored equipment, legacy powder, stale transactions, crafting losses and a complete kill → chitin → powder → fortify → ordinary repair chain. Reload checks preserve maximum/current durability, gear state, resources and paid corpse identities across four boots. `check_fortification_ui.py` operates the actual client recipe, cancel, timed craft, target and fortify controls.

## Deployment checkpoint — 21 October 2026

Installed and live-save/restart verified. Source and installed cumulative/legacy checks pass, as do the actual-client powder controls, 41 Python tests, catalogue reproduction and all native patch round trips. Pre-install, installed and post-live copied-world comparisons preserve both characters, their skills/caps/locks, and all saved gear/material/gold snapshots. The complete backup is `.local/backups/pre-t36-20261021-041349`; local evidence is under `.local/logs/t36` and `.local/logs/deploy-t36`. The server remains running. Engine and client are unchanged; the native content patch routes legacy powder into the guarded class flow.

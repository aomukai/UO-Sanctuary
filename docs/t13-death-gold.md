# T13 — carried-gold death loss

Scope: replace the prototype level-based penalty with the agreed flat 20% for class characters. Bank gold and all character progression remain safe. Corpse lifetime/escrow, insurance and mercenary changes remain later cards.

Acceptance checks:

- Sum physical Gold recursively in the backpack once at death, including nested and protected stacks. Calculate `floor(total / 5)` with a 64-bit total; 0–4 gold costs zero, 5 costs one, and 1,001 costs 200. Do not round each stack separately or impose a minimum charge.
- Remove that amount across the enumerated stacks, deleting empty stacks and reducing a final partial stack. Gold elsewhere, including the bank and older corpses, is outside this bill. Surviving gold follows normal death transfer/recovery.
- Claim each new corpse before consumption, including zero-gold deaths. Duplicate callbacks, resurrection, reconnect and reload cannot repeat that charge. A subsequent genuine death receives its own claim and counts only the backpack at that time.
- Preserve non-class behavior, XP/level/allocations, equipment recovery/capacity handling, belt potion transfer and minion dismissal.
- Verify real death/recovery and restart behavior in disposable worlds, then back up and deploy while preserving player saves, configuration and client preferences.

Implementation: an optional content hook runs after native corpse creation and before inventory transfer, while the backpack still identifies the relevant gold. `Corpse.SanctuaryDeathCharged` uses a spare bit in its already serialized flags; no save-version change or new persistence file is needed. Pre-existing corpses load normally and never run the creation hook again. The native transfer skips stacks deleted by the hook. The existing post-death handler retains belt/combat/minion cleanup and validates the current, owned corpse and dead player.

Status: implemented, tested and deployed on20 October2026 (JST). The live server is running with T13 and the completed profession/crafting features enabled.

## Verification

`DeathGoldChecks` covers levels1/10/20/99 with totals0/1/4/5/6/9/10/99/1001/120001, separate one-coin stacks, nested/protected gold, exact bank/XP preservation, repeated creation/death callbacks, resurrection/recovery, new deaths and unrecovered older corpses. Persistent fixtures include a ghost, a recovered player, zero-gold death followed by later gold, and an unclaimed corpse representing pre-T13 data. Reload checks exercise repeated disconnect/logout/connect notifications and verify both claimed and legacy flags. Existing corpse checks retain equipment restoration, full-pack/overweight leftovers and safe retries.

The four-boot cumulative suite passes death claims alongside repairs, all486 durable recipe identities, markets, reward lives, effective gear, crafting and processing. The separate legacy suite passes combat, XP, belt, pets, resurrection and save migrations. The actual ClassicUO flow verifies ghost steering, the free healer prompt, full-life resurrection, single-click starting-gear/gold recovery, mouse/hotkey summoning, pet stats and injured guardian identity/health/order through disconnect/relogin. Test UI log readers now honor the configured isolated log path. No client code changes are required.

Native application/content, Sanctuary content and integration builds have zero warnings/errors. All16 Python checks pass after regenerating the bounded authoring audit for the current assembly; its24 affix queries and1,536 rolls pass. Patch reproduction and reverse round trips pass for23 ModernUO patches/53 paths and22 ClassicUO patches/36 paths. Native upstream revisions remain unchanged. Production source did not change after these builds/checks.

Implementation evidence is under `.local/logs/t13/`. Deployment and preservation evidence is in `.local/logs/deploy-t13/deployment.json`.


## Deployment

The offline T12 server completed a fresh automatic save and shut down gracefully before installation. Full server/client runtime backup: `.local/backups/pre-t13-20261020-023011`. All59 protected save/configuration/client payloads were byte-identical during staging. Matching application/UOContent/Sanctuary content are installed; the server engine, logger and client binaries are unchanged. Only `UOContent.dll` and `Sanctuary.Content.dll` are registered; no integration assembly, fixture account or kit is installed.

The installed cumulative suite passes all four boots, including the new legacy-claim and reconnect checks. Both live boots restore all476 merchants without creating new ones, listen normally and save successfully; the first shuts down cleanly and the second remains running. Copied pre-deployment, installed and post-live-save worlds preserve both characters' profiles/allocations/native skills/caps/locks/worn identities, gear state, materials and all485 physical gold-stack identities/amounts/parents/origins. All34 configuration/client/assembly-list files, including13 client preference files, retain their hashes. The client is closed. Source changes remain local for the next requested Git checkpoint. T14 is next.

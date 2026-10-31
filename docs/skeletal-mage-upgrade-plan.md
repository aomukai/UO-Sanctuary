# Skeletal Mage upgrade plan

User decision, 2026-10-31 JST: Skeletal Mages should receive **extra spells** to improve their usefulness. This is approved enhancement intent beyond the pinned D2R Reign of the Warlock3.3.93847 baseline. The specific spells and their mechanics have not been selected; no additional spell is implemented or silently substituted by this note.

The user has no points in Raise Skeletal Mage and cannot supply observations from that character. Continue with the pinned tables, existing source references and native checks without requesting Mage tests from the user.

Retain the mechanically verified elemental-bolt baseline as the foundation. The [skill-clock correction](mage-clock-checkpoint.md) is independent work and does not choose the new spell package. Reuse existing player/NPC spell, projectile, status and summon services when implementing the upgrades.

Nonblocking design questions for the later upgrade pass: which spells each elemental Mage gains; unlock ranks, scaling and resource/cooldown rules; AI selection and target eligibility; whether extra spells supplement or replace individual bolt attacks; balance and save/lifecycle treatment. These remain open, rather than using guessed D2R behavior. Follow the user's priority of finishing class skills before the later improvement pass unless explicitly redirected.

After spell choices are approved, record every needed cast/projectile/impact/status effect and audio in the effects ledger, and separately determine whether any tree/hotkey icon is actually required. Autonomous pet spells do not automatically imply new player hotkeys. Identified placeholders must keep implementation independent of final art. No final assets or new spells are claimed here.

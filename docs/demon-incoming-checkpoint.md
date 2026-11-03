# Prepared demon incoming damage checkpoint

Reference: **D2R Reign of the Warlock 3.3.93847**. Owned `monstats`/`monstats2`, the existing return-identity table and shared older-source return implementation remain separate evidence sources. Neither demon talent is newly enabled by this component.

## Change

The shared Goatman/Tainted profile previously omitted `hcIdx` and source modes, so the ordered incoming receiver refused their return metadata. It now reuses their existing pinned identities: Goatman class/base742 and velocity8; Tainted743 and velocity6. Both are killable, have hitclass3, no get-hit/block mode, and permit knockback. Their mapped Gargoyle/Succubus appearance supplies none of these capabilities.

The native receiver also refused their inherited `SkeletonGuardian.Damage` override. That override adds only a friendship guard. The adapter accepts that exact inherited method for these two demon types, preserves the guard using the actual attacker, and retains refusal of other overrides and incoming native modifiers. The NPC fire receiver supplies its actual emitter independently of credit ownership. Shared resistance, notification, life commitment, return and death services remain in use; there is no second damage formula.

## Verification

Build2/main-run1/focus-run2 pass **390 broader /58 focused checks**, retaining all387/55 predecessor markers across three boots,two actual reloads and three saves. Original Tainted,enemy,owner,Tarnhelm and mana22,784 persist; the same wounded actor receives another real qualified fire packet after each reload,ending at life116,274. Saved recovery/aura inspection clocks are held explicitly. The clean content/integration builds have zero warnings/errors. All71 Python tests,source-review reproducibility,573-definition readiness,240-skill catalogue and roadmap checks pass. The strict recorder confirms106 installed artifacts,38 save files and20 configuration files unchanged. Fresh980/21-fingerprint audits preserve all573 named decisions. [Native evidence](../tools/reference/demon-incoming-native-evidence.json) binds consistency and installed-artifact/save protection checks.

The initial native run correctly refused the inherited friendship override; its source,logs,inputs and binaries are preserved. The final adapter explicitly preserves that guard. Two source-review diagnostics record the absent historical ignored dependency and raw *Id versus cached Id header; neither was silently treated as evidence.

New native coverage exercises metadata, friendly refusal without life/packet changes, qualified enemy fire, the native resisted-damage entry, NPC420 aura protection, source-lethal return and the original wounded body/helm/owner/mana through two actual reloads. These are receiver tests with supplied successful packets, not automatic enemy AI or whole combat certification.

The source review binds both current raw rows and their existing identity entries, plus the unchanged original bodies named in the existing return proof. One historical ignored dependency is absent, so that old proof's whole artifact inventory is not revalidated and no new complete source oracle is claimed.

## Blood Boil and remaining work

Blood Boil380 uses new do160. Its table-shaped candidate is RVA0x44e2c0, but dispatcher linkage and semantics are unverified; the observed code mapping is unreadable. This observation read only pinned-file data and process map metadata. No code, heap, saves or protected pages were sampled. Health cost, pet iteration, proc cadence and target/damage ordering remain unimplemented rather than inferred from the formulas. This is nonblocking for other skills.

GenericPet world/controller/owner/mode admission, complete skill-list/extended-passive services, full incoming event/reaction paths, learned summoning/payment/shared cap and complete lifecycle remain unfinished. Current executable, scheduler/world order, hostile source production and graphical acceptance are separate.

## Assets

No new icon or effect is needed for the metadata/guard change. Generic aura and native hurt/death presentation remain identified placeholders. Final summon, spell, aura, hurt/death and audio assets remain in the effects register; talent-tree and right/quick-hotkey artwork remain in the icon register.

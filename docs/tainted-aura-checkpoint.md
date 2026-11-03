# Tainted inherent Resist Fire checkpoint

Reference: **D2R Reign of the Warlock 3.3.93847**, owned NPC420 skill row, with unchanged older engine routines at D2MOO `0c0bbd1e6bef9181a3da1c046523af622a925416`. This is a prepared summon component; learned Summon Tainted remains gated.

The unclean reboot preserved the source changes. Recovery compared all106 installed artifacts,38 save files and20 configuration files with their protected hashes. No surviving build/test process was found. Recovered oracle outputs were copied before rerunning; an absent completion record was not treated as a pass.

## Implementation

* Positive summon-grant aura selection registers NPC420 automatically, with immediate application for a connected prepared owner and no S1/A2 cast or mana payment.
* The existing innate NPC getter supplies the pet’s own effective rank. Pulses occur every50 frames and leases last51 frames. Current radius, diminishing fire resistance and zero cost come from the pinned NPC row.
* Paladin aura storage now retains the actual source skill. NPC420 and Paladin100 share resistance state3: weaker same-skill ranks refuse, equal ranks retain creator/refresh expiry, and different skills replace irrespective of rank. They never stack in that state. NPC420 grants no maximum resistance.
* The owned row names `passive_resistfire` for removal, despite having no passive stat or maximum-resistance payload. The existing dynamic Paladin maximum consumer remains unchanged; complete removal-callback/restoration chronology is still outside the source composition.
* Selected emitters persist on the original actor through offline/parking periods. Recipient leases keep expiring, with no recovery/reset or catchup pulses. Deleting the actor removes its emitter; other recipients keep only their existing finite leases.
* V0 Paladin lease JSON without SourceSkill remains readable and writes unchanged default fields. Selected Tainted emitters have a separate V0 persistence profile. Restore builds temporary collections and rejects malformed inputs atomically.

## Verification

Build3/main-run1/focus-run3 pass387 broader and55 focused native checks, retaining every382/50 predecessor marker across three boots,two actual reloads and three saves. Native checks exercise same-state replacement, legacy JSON, atomic refusal, own Command rank, exact radius/height and friendly filtering,typed fire resistance,offline no-catchup,connected immediate selection,and original-actor timer pulses followed by real parking expiry after both reloads. Original helm,wounds,owner,mana22,784 and selected aura retain. Saved inspection/recovery clocks are explicitly held; this is not unheld downtime proof.

All71 Python tests, source reproducibility,573-definition readiness,240-skill catalogue and roadmap checks pass. The final strict recorder rechecks106 installed artifacts,38 save files and20 configuration files. Fresh980/21-fingerprint audits preserve all573 named decisions. The clean content and integration builds have zero warnings/errors. Two failed native runs were occupied-placement and overlap/training fixture errors; their logs,inputs,source and DLLs are preserved. The production content DLL is identical across all three builds. The recovered source run also corrected the passive-state metadata assertion; all diagnostics remain recorded. [Native evidence](../tools/reference/tainted-aura-native-evidence.json) binds the final consistency/protection checks.

The source oracle executes1,152 complete callback/factory/resistance-modifier contexts and770 complete do65/filter/iterator contexts under ASan/UBSan. Their compositions are separate: do65 uses an explicit callback recorder; the complete callback fixture has no passive list and records, rather than invokes, removal callbacks. Native projection checks576 NPC420 callback contexts and all770 formula/period contexts, independently of real actor/recipient tests.

## Remaining work and assets

Actual GenericPet world/controller/owner/mode admission, complete skill-list/extended-passive services, Blood Boil, incoming combat, learned summoning/payment/shared cap and full lifecycle remain unfinished. Older unchanged source plus current owned tables do not certify current executable, global scheduler, world enumeration/geometry, complete passive-list history or graphical-client acceptance.

The generic0x375A/hue0x489 aura marker is an identified placeholder. Final aura, summon/cast/contact/hurt/death and audio remain in the separate effects register. Summon Tainted still needs talent-tree and right/quick-hotkey artwork; internal NPC420/422 add no player-bindable icons.

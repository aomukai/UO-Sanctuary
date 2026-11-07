# Shadow incoming defense checkpoint

Reference: **D2R Reign of the Warlock3.3.93847**, approved Sanctuary deviations first. Learned coverage remains208/240, Assassin28/30.

The user approved determining Shadow Weapon Block eligibility from its actual original active equipment: two claws supply composite13; no shield is fabricated from ToBlock50. Both species use the existing `SourceAvoidance` routine, posted passive chance/type layers and saved actor RNG. Stationary defense and supplied walk/run modes remain distinct. Removing one claw disables Weapon Block without an RNG draw.

An explicit optional `DefenseProfile` qualifies a stationary original Shadow as an incoming world recipient. It retains its original owner/items/binding/skill book/passives/resources/events and uses the existing block/accuracy, ordered melee/missile HP, friendship guard, item callbacks and scalar/duration defenses. World item-proc/projectile services use its own source identity. Legacy parked bodies do not acquire this profile automatically; the optional field does not alter their JSON. `CanActivate=false`, stopped AI and unavailable learned routes remain. This is not a finished summon controller: intrinsic actions, movement, AI105/106 and current modifier42 death behavior still need qualification.

Native checking exposed an actual type-domain mismatch: skills use the owned item-type linker IDs (h2h68), whereas public item JSON lineNumber gives h2h69. The generated109-row table preserves four reserved blank rows and105distinct named types. Its builder pins the owned itemtypes extraction and compares3,003compiled skill fields. Defense and ordinary NPC weapon-mastery lookups now share this compiled domain; public item initialization metadata is unchanged.

Verification is recorded in `tools/reference/shadow-defense-native-evidence.json`. The focused native cohort retains original Shadow host/body, Valkyrie defense, actual saved projectile/chain and ordinary/incoming original-item checks. Initial failed logs are retained in the ignored stage: the first exposed the type-domain bug; the second caught an overstrict table validator that rejected the four reserved rows. No failing check was removed. Full class suites and the item audit exporter are deferred to the next class/shared milestone.

Presentation reuses identified0x375A/hue0x481 and0x1A4defense placeholders. Final Shadow body/equipment/effects/audio and talent/hotkey icons remain in their separate inventories. No imagegen asset is required for this mechanical connection.

**Verified:** Content4/Integration4/snapshot4,33native markers,three boots/two reloads/three saves;source snapshot matches actual tested inputs. The pinned metadata builder’s `--check` passes3,003compiled field comparisons. Installed106artifacts,38saves,20configuration files and historical client patches remain unchanged. See [receipt](../tools/reference/shadow-defense-native-evidence.json).

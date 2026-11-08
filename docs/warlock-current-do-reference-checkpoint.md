# Current Warlock skill-function reference checkpoint

2026-11-08 JST. Pinned reference: **D2R Reign of the Warlock3.3.93847**, approved Sanctuary deviations first. **210/240 handlers; Warlock1/30; no class certified complete.** No production,installed-build or save change;no new native test run was needed for this reference-only capture.

The running Steam/Proton client was found by its actual executable arguments and owned-process identity. Linux reports its name as **`Main`**. Earlier exact-name `pgrep D2R.exe` checks were insufficient to establish that it was closed. Future reference work should use `probe_warlock_runtime.processes()` and then the existing pinned image/identity/anchor checks,which verify the reference before reading code.

The preserved complete current dispatcher at RVA3aaf70 checks the do index against190,reads the primary skill's field4e or alternate ItemEffect context,and calls `163a8b0[index]`. The freshly read immutable191-entry table matches the pinned executable exactly. This establishes index linkage beyond the earlier unique-table-shape corroboration. The capture tool follows that verified table and existing unwind metadata;it never scans the process heap or changes protections,input or memory.

Complete new captures,with every unwind fragment retained and no invalid-instruction markers:

| Do slot | Current skill use / boundary |
| --- | --- |
|154|Ring of Fire launch|
|155|Death Mark|
|157|Shared Sigil casting|
|158|Bind Demon|
|163|New area/effect entry;retain its exact skill/ItemEffect mapping in the receipt|
|164|Cleave,including NPC use where selected by current data|
|167|Miasma Chain|

Ring of Fire's directly referenced missile factory at RVA400760 is also captured completely. Its progressive-missile/velocity helper bodies remain inaccessible in this observation. Do56/156/159/160/161/162/165/166remain incomplete;the first combined capture exhausted its byte budget,so separate per-slot captures established the final complete set. Partial fragments are preserved as partial and never substituted for a complete body.

Reactive event candidates35/36/37/39/40remain inaccessible. Their immutable candidate table is unchanged,but callback semantics/identity remain unverified. No unchanged polling window or forced read was used. Each bounded capture reads at most8192bytes,checks process identity and readable executable permissions,and double-reads stable code. Existing captures were preserved. Raw binaries/disassembly remain local and ignored;reproducible tools and hashes are backed up in Git. [Authenticated evidence](../tools/reference/warlock-skill-do-current-evidence.json).

This closes reference-access/index-linkage gaps,not every function's behavior or callee identity. Next translate or emulate a complete launch/cast body using the existing formula,vector,factory,area and status services;qualify relevant missile/controller/damage and resource/lifecycle boundaries before opening its learned route. Ring launch and shared Sigil casting now have concrete current bodies for that work.

Effects/audio and talent/hotkey artwork remain in their existing separate [effect](skill-effects.md) and [icon](skill-icons.md) inventories. Reference capture adds no asset identity or final art. Installed artifacts,configuration and saves pass the existing preservation check.

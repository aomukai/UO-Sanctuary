# Miasma Chain current launch checkpoint

Reference: D2R Reign of the Warlock **3.3.93847**. **212/240 handlers, Warlock3/30; no class certified.** This implements the launch request producer, not the learned cast or its missile controllers.

`MiasmaChainLaunch` translates the complete current do167 at452650. It resolves the row, rejects nonpositive progressive missile IDs, resolves the target, sets flag0x40, preserves source-path coordinate widths and emits one flags0x21 request. All remaining fields in the152-byte factory structure are zero, including velocity and opaque context. Factory failure returns0 while retaining the already-written flag. The actual progressive helper, target helper and factory remain explicit services.

The primary skill399/start0/do167 creates **maker711**, not a straight bolt or the moving chain itself. The full pinned dependency graph is:

| Stage | ID | Server behavior | Next stage |
| --- | ---: | --- | --- |
| miasmachainsmaker | 711 | do43, velocity0, lifetime1, collision6 | miasmachains |
| miasmachains | 712 | do45, hit41, velocity35, InitSteps12 | miasmachainscloudmaker |
| miasmachainscloudmaker | 713 | do43, collision6 | miasmachainscloud |
| miasmachainscloud | 696 | do41, magic, NextDelay25 | terminal cloud |

The four original rows are copied unchanged into an isolated `miasma-chain-route.json`; they do not expand active collision-footprint producers. Existing Entropy suppliers still distinguish effective Range/Radius from the hard-point cloud layer formula. Miasma Bolt uses its own maker736/do37 before cloud705/do41. These extra maker/motion dependencies were missing from the older roadmap wording and are now recorded explicitly.

**Verification:**20 focused executions of retained current instructions match native C# service order, request fields, early guards, path widths and allocation outcomes. Stage`.local/class-skills/miasma-chain-launch`:Content1/Integration1/snapshot1/native1,12markers/3boots/2reloads/3saves,zero build warnings/errors. Original Tarnhelm moves effective rank1→2 and the half-mana table cost4.5→5;original owner/gear/request DTO survives both reloads. No active missile persistence or learned cast is claimed. Source snapshot matches exact native inputs. No compiler matrix rerun.

Two bounded read-only observations verify an immutable53-entry missile-do candidate table at163bc90. Its old duplicate23/24 entries and new candidate slots are consistent with the table shape, but **current dispatcher linkage remains unverified**. Slot41candidate3fb680/170bytes and slot45candidate3fdbb0/245bytes are wholly protected. Both observations read1,408bytes of identity/table anchors only; zero callback code. No forced read,input,attachment,protection change or write. Previously protected3fabf0/3fb050/3fbf40 align with candidate slots37/42/43;do not repeat those unchanged observations or turn that table-shape lead into a dispatch certificate.

Remaining: maker43, moving-chain45, hit41/cloud41 emission/geometry/hit windows, magic mastery/pierce, real factory/world placement/ownership/lifecycle and learned input/payment. Continue around these explicit gaps; do not substitute BasicMissileUpdate or the ordinary Miasma Bolt for the chain.

The complete Ring world rule was approved while these checks ran. Its [approval](../tools/reference/ring-world-approval.json) is saved; next wire and verify Ring’s shared SC/left/right/quick route. Effects/audio and icons remain separate. Installed106artifacts,38saves and20configuration files remain intact;no deployment.

[Current instruction evidence](../tools/reference/miasma-chain-launch-current-evidence.json),[native receipt](../tools/reference/miasma-chain-launch-native-evidence.json).

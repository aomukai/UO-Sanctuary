# Installed class package and main consolidation

Reference: **D2R Reign of the Warlock 3.3.93847**, with approved Sanctuary adaptations first. [Mechanical reference](skill-mechanical-reference.md). Charms and runewords are excluded. The [installation receipt](../tools/reference/class-skills-installation-evidence.json) records the actual installed artifacts,source comparison,original-save preservation and reviewed history merges.

**Installed — 2026-11-18 JST.** All240talent routes across eight classes,including Warlock/Witch,are present in the matching server/content/data/client package. Current combined class cohorts retain their individual scopes;the installed production artifacts match that verified package. This is a playable-build milestone,not a certificate of exact modern executable or graphical combat parity. The [overview](class-skill-status-overview.md) and [roadmap](class-skill-roadmap.md) retain remaining uncertainties.

The real installed server loaded all **572mobiles and4,698items**,including both existing player characters and their two original Skeleton Guardians,and stopped cleanly without saving. All **38original save files**,20configuration files,client settings and client data retain their original hashes. The earlier copied-world check saved/reloaded all5,270entity identities three times without changing their serials,types or creation ticks. Both server and client are stopped after verification.

The corrected44-patch client is installed with its complete dependency graph. Its qualified source and shared input definitions are unchanged;the fresh build repairs inherited project metadata that omitted the ClassicUO runtime libraries. Actual login-screen rendering was inspected in Xvfb. Account login,character creation and graphical combat remain separate from that startup check.

The native reference assemblies,normal client build output,content staging DLL and development Integration cache now match the verified package. Production contains only its normal UOContent/Sanctuary.Content assembly manifest;Integration is excluded. The normal `./dev server` staging operation was exercised and retains the matching content/data.

The four [reviewed side histories](main-side-branch-review.md) are joined into main while retaining the verified source tree. Every local and fetched remote branch head is an ancestor. Earlier branches remain available. The main checkout preserves all unrelated local source edits;41ClassicUO and101ModernUO original modified/untracked files and their binary diffs are backed up. After applying the existing patch suffixes,all **4,251native and946client captured source files** match their qualified sources. The SDK regenerated the incidental FNA Release framework-attribute cache;no new gameplay code was introduced by reconciliation.

The first checkout attempt exhausted the FILES filesystem and was recovered from the recorded source state. The complete5,041-file pinned SDK was copied and verified at `/home/aomukai/Sanctuary-build-cache/dotnet-sdk-10.0.201-main`,freeing space. FILES is exFAT and does not support symlinks;its existing `.tools/dotnet/dotnet` command now uses a small local launcher for the same10.0.201SDK. The server smoke used that launcher successfully. No SDK version or gameplay behavior changed.

To play from the main checkout:

```sh
cd /media/aomukai/FILES/Sanctuary
./dev server
# In another terminal:
./dev client
```

The existing account and characters are retained. Installed-world verification did not create an account or alter credentials. Durable rollback files and original-world backups are under the local `.local/class-skills/main-consolidation` and `.local/class-skills/class-skills-handoff` evidence links in the class worktree;their exact paths and hashes are recorded in the receipt. Keep the original backups when making further changes.

Final [effects/audio](skill-effects.md) and [talent/hotkey icons](skill-icons.md) remain separately listed with identified placeholders. Exact modern timing/RNG/stat/event/geometry,unsupported world/recipient/item and graphical gameplay gaps remain explicit;passing the installation smoke does not resolve them. The goal remains active for the final requirement-by-requirement completion audit.

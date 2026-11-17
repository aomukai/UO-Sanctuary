# Consolidate class work into main

The user requested consolidating the branches into `main` once the class work is finished. This is authorized follow-up to the class goal, including reconciling genuine side-branch work, checking the resulting build and pushing the verified result.

A read-only inventory on 2026-11-18 JST, at working commit `910559402a30293b8629c48709569fa197c544b0`, found 117 remote branches and 120 local branches. Remote `main` was `4c5d3a0752b005c8950b43aeccc0541ba1fdd695`, an ancestor of the working branch, with a divergence of zero main-only commits and 920 working-branch commits. Most branch tips are already ancestors of this work. Advancing main to the verified working history is therefore structurally straightforward; the side branches below need a separate content review first.

| Side branch | Commits outside working ancestry | Review |
| --- | ---: | --- |
| `codex/death-sentry-corpse` | 1 | Local-only head `66071a63`; compare its corpse-base-life work with the current completed implementation. |
| `codex/npc-kill-recovery-checkpoint` | 1 | Head `07d68de7`; compare the unfinished checkpoint with subsequent recovery work. |
| `codex/revive-learned-casting` | 3 | Head `16c2f171`; review recovery/respec/resume notes against the current implementation and checkpoint. |
| `codex/revive-regeneration` | 1 | Head `4aee67ea`; Git already finds an equivalent patch in the working history. |

The read-only comparison found six side-branch commits total, including the equivalent regeneration patch. An ancestry difference is not by itself missing functionality: compare the actual code and documentation before bringing older changes forward. Final consolidation must use fresh remote refs and the then-current accepted class checkpoint.

The main worktree is `/media/aomukai/FILES/Sanctuary`; its ClassicUO and ModernUO submodules currently contain local modifications. Capture and preserve those actual changes and the installed artifacts/saves before updating that checkout, then validate the combined source and matching server/client package. The existing class-work protection procedure remains authoritative. Current inventory files are saved under `.local/class-skills/main-consolidation-inventory-2026-11-18.json` and `main-consolidation-side-branches-2026-11-18.json`. This inspection made no main or working-tree update.

The [side-branch content review](main-side-branch-review.md) now resolves all four heads:their touched files are present,with identical implementations or later qualified replacements. The selected history merges retain the current tree. Original main submodule file backups are captured in `.local/class-skills/main-consolidation`;source synchronization and promotion follow the verified merge.

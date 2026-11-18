# Local storage cleanup, 18 November 2026

The cleanup restores space for the Codex update while retaining the installed Sanctuary release and verification records. FILES increased from approximately **2 GiB to 11 GiB available**; the mounted Codex filesystem increased from approximately **320 MiB to 4.2 GiB available**. The host system filesystem retains approximately 36 GiB available.

| Action | Result |
| --- | --- |
| Compact legacy rollback directories | 64 old directories / 43,657 files retained in a verified 1.03 GiB compressed archive; two newest expanded rollbacks and top-level backup files remain |
| Remove ordinary project intermediates | 29 `obj`/Python cache directories, about 169 MiB allocated, archived and verified before deletion; frozen stage intermediates were left intact |
| Remove obsolete Codex package | Inactive 0.167.0 removed after checking executable/memory-map use; active 0.170.0 and its selection link retained; about 516 MiB freed |
| Relocate frozen evidence | Ten complete stages, 2.96 GiB of file data, moved from the fixed-size Codex image to the host filesystem; original paths remain directory links and file bytes were verified |
| Verify Git preservation | All existing Sanctuary worktree tips already belong to main; all upstream modifications reconstruct exactly from the pushed 45 client / 75 server patches, with no uncovered local source files |

The archive lives in the runtime checkout at `.local/cold-storage/legacy-backups-20261118.tar.gz`, with its checksum, snapshot list and per-file SHA-256 manifest beside it. `.local/backups/ARCHIVED-20261118.json` points to the archive. The two expanded rollback directories are `pre-t44-20261021-211320` and `pre-t45-20261021-221335`. Current class-release installation/rollback artifacts and the actual server saves/configuration were not compacted.

From the runtime checkout, list or restore one old rollback:

```bash
python3 tools/restore_archived_backup.py --list
python3 tools/restore_archived_backup.py --snapshot pre-skills-ui-20261017-173851
```

When invoking the helper from another checkout, add `--runtime-root /media/aomukai/FILES/Sanctuary`. Restoration verifies the archive and every restored file, refuses existing destinations, and only expands the selected snapshot. It does not install that snapshot or change live saves/settings. A real restoration into a temporary directory passed, along with refusal checks for an existing destination and an invalid snapshot name. Older scripts that inspect an archived rollback directory need that snapshot restored first.

Relocated frozen stages live under `/home/aomukai/Sanctuary-checkpoint-archives/relocated-from-codex-20261118/`. Their original `.local/class-skills` paths remain valid. Cleanup plans, full retained-file manifests, cache archive, archive verification, upstream patch round-trip checks and Git inventory are retained locally under `/home/aomukai/Sanctuary-checkpoint-archives/cleanup-20261118/`. A second copy of the compressed old backups remains there as well. These local archives can contain original settings and remain outside Git.

The current class release, generated roadmap and rendered-class evidence checks all pass after cleanup. Installed binaries, pinned SDK, UO data, reference captures, Codex chats, personal downloads and other projects remain untouched. The two mounted ext4 disk images were neither deleted nor resized. There are no game server/client processes running. All source changes remain on GitHub; this checkpoint adds only the restoration helper and cleanup documentation.

# Sanctuary

Diablo II combat and itemisation in a persistent Ultima Online world. The design baseline is [the kickoff brief](Britannia-Diablo-Project-Kickoff.md). Agreed direction and candidate mechanics remain distinct.

The local foundation is installed with Classic Client data **7.0.117.1**. Both upstream projects compile. The server boots, authenticates the local owner account, saves, and authenticates again after restart. Andy confirmed successful desktop login and entry into the game world. Movement, interactions, equipment, and character/inventory persistence still need explicit play checks before combat work.

## Pinned foundation

| Dependency | Revision |
| --- | --- |
| ModernUO | `eff9de028136b11e544fd81375d1e0e710e22dd6` |
| Desktop ClassicUO | `ee79d7ebc1cc0e53ff84fe6cae17d389e2737f93` |
| .NET SDK | `10.0.201`, installed locally in `.tools/dotnet` |

The server and client are Git submodules under `upstream/`. ClassicUO's nested dependencies are pinned by its own submodules. The server source is unchanged. The client has two local patches: [native-library startup compatibility](patches/ClassicUO/0001-use-fna-native-resolver.patch) and [password-field compatibility](patches/ClassicUO/0002-match-password-field-to-login-packet.patch). The latter accepts up to 30 password characters, matching the login packet, and logs login-state transitions without credentials. `setup` and client builds apply the patches idempotently and reject conflicting edits. Git therefore reports the client submodule as modified; the patch files record those intentional differences. Project build overrides live in `build/`; runtime files, saves, accounts, and logs live in ignored `.local/`.

## Linux setup

Prerequisites: Python 3.10 or later, Git, Bash, and network access for initial source and NuGet downloads. Native server dependencies on Ubuntu/Pop!_OS 24.04 are `libicu74`, `libdeflate0`, `libargon2-1`, and `tzdata`. They were already installed on the initial development machine. The client bundles its FNA graphics/audio libraries and also needs a working desktop graphics driver.

```bash
./dev setup
./dev build
./dev install-data
./dev doctor
python3 -m unittest discover -s tests -v
```

`setup` installs the project SDK without changing the system .NET installation. `build` creates Debug binaries, stages them into `.local/server` and `.local/client`, and writes logs to `.local/logs/`. It preserves existing runtime configuration and saves. The client launcher adds its bundled native-library directory to its own library search path. `doctor` returns a nonzero status while required setup is incomplete.

The server build pins transitive `System.Security.Cryptography.Pkcs` to `10.0.13` via [our MSBuild override](build/ModernUO.targets). The unmodified upstream restore fails on vulnerability advisories for `10.0.0`; NuGet auditing remains enabled. See [Microsoft's advisory](https://github.com/advisories/GHSA-846f-f764-76mx).

For these shallow Debug checkouts, the server build uses `NBGV_GitEngine=Disabled` and `PublicRelease=false`. Assembly version metadata therefore does not identify the source commit; use the submodule hashes above or `./dev doctor`. This follows [Nerdbank's documented shallow-clone option](https://dotnet.github.io/Nerdbank.GitVersioning/docs/shallow-cloning-agents.html). This is a development build, not a release publishing pipeline.

The client compiles with upstream warnings, including ImageSharp package vulnerability advisories. They are retained in its build log; a successful build does not mean those advisories are resolved.

## UO data and first local session

The data is installed in `.local/uo-data`. The official Windows installer is retained in `.local/downloads/`; its source is the [official download page](https://uo.com/client-download/). ClassicUO uses this separate installation's art and maps.

For a fresh Linux setup, `./dev install-data` uses [UO Patcher](https://github.com/andrezaiats/uo-patcher) at commit `e7e890334705032f66b2ed99eee08e5cace6db93`, verifies its source hash, and downloads from the publisher's patch servers. It excludes unavailable patch-note web pages and checks game-file sizes and archive entries against the manifests. The initial installation verified 420 game files and 44,200 game archive entries. Asset data follows the publisher's current manifest, so future downloads may have a different client version.

The helper stages temporary chunks in `/dev/shm` to avoid slow small-file writes on this exFAT project drive. It needs at least 5 GiB free there; set `SANCTUARY_ASSET_SCRATCH` to another suitable staging directory when needed. Finished files stay on the project drive, and temporary staging is removed.

On this machine, the data path and actual version are already configured. The provisional test era is `SA`, with Felucca and Trammel enabled. This does not settle the eventual world or expansion. To configure a fresh checkout after installing data:

```bash
./dev configure .local/uo-data --client-version 7.0.117.1 --expansion SA
```

Use the version matching the installed `client.exe` if downloading newer data. On this machine, start directly with:

```bash
./dev server
# In a second terminal:
./dev client
```

`configure` checks a few required asset files; it does not certify a complete installation. It refuses to overwrite existing configuration. Edit `.local/server/Configuration/` and `.local/client/settings.json` for later changes.

The generated server listens on `127.0.0.1:2593` and disables public-address detection. The ping listener also binds to loopback. Account auto-creation is disabled. This installation has an owner account; its username and generated password are stored in `~/.config/sanctuary/owner-login.json` with mode `0600`. No credentials are stored in the repository or command arguments. On a fresh installation, create an owner account at the server's interactive console.

For the remaining baseline play checks, log in with the local owner credentials and enter the world. Before adding combat code, verify walking/running, doors, a container, equipment, and banking. Use the upstream `[Save` command as owner, stop the server cleanly, restart it, and verify the same character and inventory return. At the server console, `save` writes a save and `shutdown` stops the server; shutdown alone should not replace an explicit save. Record outcomes in [the baseline audit](docs/baseline-audit.md).

The next experiment is an isolated combat room with one provisional class, a basic attack, a projectile, an area attack, and visible recovery timing. Final class selection, production skill trees, economy rules, and NPC dialogue are still ahead.

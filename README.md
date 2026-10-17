# Sanctuary

Diablo II combat and itemisation in a persistent Ultima Online world. The design baseline is [the kickoff brief](Britannia-Diablo-Project-Kickoff.md). Agreed direction and candidate mechanics remain distinct.

The first milestone is in progress: both upstream projects compile on Linux. They have **not** been verified together in a running game. Combat-room gameplay is the next milestone after vanilla login and persistence work.

## Pinned foundation

| Dependency | Revision |
| --- | --- |
| ModernUO | `eff9de028136b11e544fd81375d1e0e710e22dd6` |
| Desktop ClassicUO | `ee79d7ebc1cc0e53ff84fe6cae17d389e2737f93` |
| .NET SDK | `10.0.201`, installed locally in `.tools/dotnet` |

The server and client are Git submodules under `upstream/`. ClassicUO's nested dependencies are pinned by its own submodules. No upstream source files have been edited. Project build overrides live in `build/`; runtime files, saves, accounts, and logs live in ignored `.local/`.

## Linux setup

Prerequisites: Python 3.10 or later, Git, Bash, and network access for initial source and NuGet downloads. Native server dependencies on Ubuntu/Pop!_OS 24.04 are `libicu74`, `libdeflate0`, `libargon2-1`, and `tzdata`. They were already installed on the initial development machine. The client bundles its FNA graphics/audio libraries and also needs a working desktop graphics driver.

```bash
./dev setup
./dev build
./dev doctor
python3 -m unittest discover -s tests -v
```

`setup` installs the project SDK without changing the system .NET installation. `build` creates Debug binaries, stages them into `.local/server` and `.local/client`, and writes logs to `.local/logs/`. It preserves existing runtime configuration and saves. `doctor` returns a nonzero status while required setup is incomplete.

The server build pins transitive `System.Security.Cryptography.Pkcs` to `10.0.13` via [our MSBuild override](build/ModernUO.targets). The unmodified upstream restore fails on vulnerability advisories for `10.0.0`; NuGet auditing remains enabled. See [Microsoft's advisory](https://github.com/advisories/GHSA-846f-f764-76mx).

For these shallow Debug checkouts, the server build uses `NBGV_GitEngine=Disabled` and `PublicRelease=false`. Assembly version metadata therefore does not identify the source commit; use the submodule hashes above or `./dev doctor`. This follows [Nerdbank's documented shallow-clone option](https://dotnet.github.io/Nerdbank.GitVersioning/docs/shallow-cloning-agents.html). This is a development build, not a release publishing pipeline.

The client compiles with upstream warnings, including ImageSharp package vulnerability advisories. They are retained in its build log; a successful build does not mean those advisories are resolved.

## UO data and first local session

Obtain and patch the **Ultima Online Classic Client** data using the [official download page](https://uo.com/client-download/). ClassicUO needs that separate installation's art and maps; those assets are not included in either source project. Installing and patching the Windows data on Linux is still an outstanding setup step.

Once the data is available, supply its actual path and matching version. Choose a provisional test era explicitly; `AOS` and `SA` are supported setup choices, with neither selected yet. Configuration starts with Felucca and Trammel only; this does not settle the eventual world or expansion.

```bash
./dev configure "/absolute/path/to/Ultima Online Classic" \
  --client-version YOUR.INSTALLED.CLIENT.VERSION --expansion SA
./dev server
# In a second terminal:
./dev client
```

Replace `YOUR.INSTALLED.CLIENT.VERSION` with the four numeric components matching the installed data. `configure` checks a few required asset files; it does not certify a complete installation. It refuses to overwrite existing configuration. Edit `.local/server/Configuration/` and `.local/client/settings.json` for later changes.

The generated server listens on `127.0.0.1:2593` and disables public-address detection. It does not create accounts automatically. On first server start, create the owner account at its interactive console; use those credentials in ClassicUO. No credentials are stored in the repository or command arguments.

Before adding combat code, verify character creation, walking/running, doors, a container, equipment, and banking. Use the upstream `[Save` command as owner, stop the server cleanly, restart it, and verify the same character and inventory return. Record outcomes in [the baseline audit](docs/baseline-audit.md).

The next experiment is an isolated combat room with one provisional class, a basic attack, a projectile, an area attack, and visible recovery timing. Final class selection, production skill trees, economy rules, and NPC dialogue are still ahead.

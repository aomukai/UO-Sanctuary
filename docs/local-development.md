# Local development and deployment

Use the [README’s local setup](../README.md#running-locally) for supported commands. The checkout, source build output, installed runtime, and currently running game can be at different stages. Check the one relevant to the work and use the repository instructions and tracked upstream patch stacks when editing native sources.

## Builds and checks

- Local server files are under `.local/server`, client files under `.local/client`, and Classic Client data under `.local/uo-data`. Use `./dev doctor` to check prerequisites and build/configuration readiness.
- Stop the relevant installed process before replacing its binaries. Test fixtures can use source assemblies, so finish those checks before rebuilding their inputs. Check the selected test runner’s current flags, logs, and runtime paths before launching it.
- Run actual-client tests with isolated settings and disposable worlds. Keep fixture assemblies and test worlds in the verification environment. Respect existing checkout and submodule changes while applying or verifying patches. [Character UI checkpoint](class-roster-ui-checkpoint.md) · [Storage and evidence locations](storage-cleanup-2026-11-18.md)

## Installed runtime

For an installed update, request a server save and wait for it to complete. Shut down the relevant live processes cleanly. Back up server saves and configuration, client settings, and the entire client `Data` directory, including profiles. Install matching server content and client assets as needed, then verify startup and preservation of the saved world. The archived starter-item migration documents a one-time operation. [Deployment record](deployment-t01-t09.md) · [Death and pets](death-and-pets.md)

# Sanctuary launcher and first-run setup

The player downloads the Sanctuary launcher and opens it. The launcher walks them from a fresh machine to the game. After first-run setup, playing again takes one click. Local play, community shards, language, and optional LLM dialogue all have a home in the launcher.

## Local development launcher

Run `./dev launcher` from a configured Linux checkout. **Play** uses the installed ModernUO and ClassicUO builds. After ModernUO starts accepting connections, the launcher opens ClassicUO. **Save & Shut Down** closes the client, waits for ModernUO to publish the world snapshot, and then shuts down the server. Closing either window also follows the save path. The first local account is set up through the existing server console. [Running locally](../README.md#running-locally)

## First run

1. Choose English, 日本語, or Deutsch, then choose whether to play locally or connect to a community shard. A player joining a shard can enter its address. They can switch language and play mode later.
2. Fetch or detect the required Ultima Online client files, install the Sanctuary-compatible ClassicUO client and the ModernUO server for local play, and apply the matching Sanctuary patches and data. Reuse compatible files already on disk. Show what is being downloaded or prepared and give the player a way to retry if a step fails.
3. Set up the chosen play mode and open the game. For a fresh local world, guide the player through any first-time account setup. The launcher can offer LLM settings later, so a player can be on their way into Britannia straight from installation.

The launcher supplies the client and server settings for the chosen installation. Downloaded components and Sanctuary data should come from a compatible release set. Updating the game should preserve saved worlds, characters, and player preferences.

## Playing and changing settings

- **Play locally.** Start the local ModernUO server, wait until it is ready to accept a connection, then open ClassicUO pointed at it. Provide a clean save-and-shutdown path for a server started by the launcher. Keep the first run and later launches in the same flow.
- **Connect to a shard.** Open ClassicUO pointed at the chosen community server. Remember server addresses for later sessions. The server owns the characters created there, and the local server owns local characters. [The invasion campaign](invasion-campaign.md)
- **Language.** Use the saved language in the launcher and pass it to the game when it starts. English, Japanese, and German are the initial languages. [Game UI and localisation](game-ui.md)
- **LLM dialogue.** Let the player turn LLM mode on and off from the launcher. With LLM mode enabled, give them a place to choose and configure a provider. Scripted dialogue is available with the feature off. [NPC dialogue and personal world state](npc-dialogue.md)

## GitHub README and agent setup

The release README should give a player the launcher download and a short description of what happens next. It should also give someone using a setup agent a copyable request and a supported path the agent can follow. Use real launcher commands or a documented setup entry point, with a readiness check that distinguishes installed files from a game the player can actually open. The player and the agent should end up with the same installation and settings.

The current Linux development checkout already has `./dev setup`, `./dev build`, `./dev install-data`, `./dev configure`, and `./dev doctor`. The README includes an agent prompt for that path. Those commands are a reference for the setup stages and checks a release launcher needs. [Running locally](../README.md#running-locally)

## Decisions still open

Choose the first supported release platforms, the package sources and version manifest, and how the launcher handles updates. Settle the local first-account and shutdown flows, the setup entry point for agents, and how the client learns a community shard’s optional features.

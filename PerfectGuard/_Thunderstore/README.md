# PerfectGuard

This is a mod targeted towards server hosts that helps prevent Denial of Service attacks, such as:
- crashes
- effect & audio spam
- vanilla vulnerabilities found by the community (if present)

Some of the features of this mod can also be used client side.

# Special mentions & thanks

- [Soap](https://github.com/Amethystic) for contributing multiple improvements and additions as part of [this commit](https://github.com/Marioalexsan/AtlyssPerfectGuard/commit/3a2456309ae4df24e82ed8ab7b7b6fa13cac7a5a)

# Features

By default, PerfectGuard does the following:
- Applies rate limiting to abusable network calls, preventing DoS attacks from excessive particles, visuals, audio, etc.
  - The mod will log suspicious players / game objects in the BepInEx console
- Applies rate limiting to audio sources, preventing the same audio source from playing too many clips at the same time
- Cleans up excessive items when there are too many of them at the same time in the server
  - The item threshold can be configured to be anywhere between 50 and 500 total items
  - Currently applies to server hosts only

Each of these features can be toggled on or off either via the configuration file of the mod (`BepInEx/config/Marioalexsan.PerfectGuard.cfg`) or via [EasySettings](https://thunderstore.io/c/atlyss/p/Nessie/EasySettings/) (if available).

For some features, detailed logging can be toggled on or off in the configuration.

# Mod Compatibility

PerfectGuard targets the following game versions and mods:

- ATLYSS 102025.a5
- Nessie's EasySettings v1.2.0 (optional dependency used for configuration)

Compatibility with other game versions and mods is not guaranteed, especially for updates with major changes.
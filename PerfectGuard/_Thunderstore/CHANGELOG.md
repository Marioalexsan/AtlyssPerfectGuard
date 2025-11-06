# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.1.0] - 2025-Nov-06

Special thanks to [Soap](https://github.com/Amethystic/) for the [submitted PR / code](https://github.com/Marioalexsan/AtlyssPerfectGuard/commit/3a2456309ae4df24e82ed8ab7b7b6fa13cac7a5a) for this update.

### Added

- Improved network rate limiting functionality (implemented by Soap)
  - Network rate limiting is now generic and covers a wider variety of commands
- Added an audio rate limiting feature (implemented by Soap)
  - Should protect against crashes caused by excessive audio spam on specific audio sources
- Item cleanup functionality (implemented by Soap)
  - Removes excessive items based on a configurable threshold
  - Currently implemented as a server-side functionality
- EasySettings support for mod configuration

## [1.0.0] - 2025-Aug-27

### Changed

**Initial mod release**
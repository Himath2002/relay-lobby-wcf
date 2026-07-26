# Changelog

All notable changes to RelayLobby are documented here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and releases use [Semantic Versioning](https://semver.org/).

## [1.0.0] - 2026-07-27

### Added

- Polling and duplex WPF clients over dedicated Net.TCP endpoints.
- Public and private messaging, file relay, room management, and participant rosters.
- Recipient-filtered private callback payloads.
- Central transport and input limits.
- Windows CI, repository security guidance, and publication-ready visuals.

### Changed

- Reorganized the solution into domain, contracts, server, polling-client, and duplex-client projects.
- Migrated legacy project files to SDK-style .NET Framework 4.8 projects.
- Replaced third-party-looking background artwork with a code-native WPF theme.
- Consolidated server state management behind one synchronized service.

### Fixed

- Prevented polling requests from attempting to acquire duplex callback channels.
- Corrected room navigation so duplex clients reuse their lobby window.
- Closed WCF channels safely and removed unlimited transport quotas.
- Prevented room-directory queries and callbacks from exposing unrelated private payloads.

[1.0.0]: https://github.com/Himath2002/relay-lobby-wcf/releases/tag/v1.0.0

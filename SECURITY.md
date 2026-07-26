# Security policy

## Supported version

| Version | Supported |
|---|---|
| 1.x | Yes |
| Earlier unpublished versions | No |

## Reporting a vulnerability

Please use GitHub's **private vulnerability reporting** feature on this repository. Include:

- the affected component or endpoint;
- steps to reproduce;
- expected and observed behavior;
- potential impact;
- any suggested mitigation.

Do not open a public issue for an unpatched vulnerability.

## Intended boundary

RelayLobby is designed for local demonstration on one Windows machine. It uses an unauthenticated, unencrypted Net.TCP binding on `localhost:8100`, stores state only in memory, and does not scan transferred files.

Treat any LAN, public-network, multi-tenant, or production deployment as unsupported until transport security, authentication, authorization, persistence controls, rate limits, and file-content scanning are added.

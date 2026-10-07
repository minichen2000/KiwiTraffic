# Changelog

All notable user-visible changes to this project are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Project scaffolding: solution, layered projects, test projects, build script
  with `fast` / `release` modes.
- Pure calculation layer (`UsageCalculator`) with unit/edge-case coverage.
- Settings window: alias, VEID, API key (masked, with a temporary reveal),
  remember-key toggle, and proxy mode / manual proxy fields.
- "Test connection" queries the API with the values currently on screen and
  saves nothing, so a failed attempt cannot disturb a working configuration.
- The API key is stored with Windows DPAPI (`CurrentUser`) and is stamped with
  the VPS it was entered for: after switching VPS the key is asked for again
  instead of being replayed against another account.
- Settings and cache files use UTF-8 without a BOM, are written atomically, and
  a file that cannot be read is backed up as `<name>.corrupt-<timestamp>`
  rather than being silently replaced with defaults.
- KiwiVM client: parameters travel in the request body (never in the URL),
  redirects are not followed, TLS validation stays on, and timeouts are
  reported separately from cancellations.
- The widget now shows real readings; a failed refresh keeps the previous
  numbers on screen and states the reason instead of showing zeros.
- API error codes are surfaced verbatim; only `error == 0` counts as success.

### Changed

- The M1 simulated-data preview has been replaced by real data. There is no
  simulated mode any more.
- Traffic amounts are now shown in binary units (TiB / GiB) instead of decimal
  ones (TB / GB). The KiwiVM panel divides by 1024 but labels the result TB, so
  a 1 TiB quota appeared as "1.1 TB" here while the panel said "1 TB". The
  numbers now agree with the panel; the labels are the correct binary ones.

### Known limitations

- The KiwiVM API contract is only as good as public sources allow (the official
  API documentation is behind a panel login). Which way `monthly_data_multiplier`
  is applied is **still undecided** and has not been verified against a live
  account — see `docs/api-contract.md`.
- No widget chrome yet: no tray icon, no always-on-top, no remembered position,
  no automatic refresh. Those arrive with M3 and M4.

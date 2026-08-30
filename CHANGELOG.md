# Changelog

All notable changes are documented here. Version tags use the `vX.Y.Z` format and must match the application `Version` metadata.

## [0.6.1] - Unreleased

### Added

- Windows 11 search highlights switch.
- Individual taskbar switches for search, Task View, Widgets button, and left alignment.
- Lock-screen Spotlight and tips switch that preserves the normal Windows lock and sign-in screens.

### Fixed

- Windows 11 search highlights now use the documented computer policy and a one-time UAC operation, avoiding access-denied errors on protected `SearchSettings` user keys.

## [0.6.0] - 2026-08-30

### Added

- Hardware and power inspection with adaptive, low-risk recommendations.
- Windows 10/11/LTSC-aware system detection.
- GitHub Actions builds for full offline and lightweight Windows x64 executables.

### Security

- Conservative startup, background, visual-effect and power optimization boundaries.
- SHA256 release verification files.

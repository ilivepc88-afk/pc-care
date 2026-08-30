# Code signing policy

Free code signing provided by [SignPath.io](https://about.signpath.io/), certificate by [SignPath Foundation](https://signpath.org/).

## Scope

After SignPath Foundation enrollment is approved, only the two Windows x64 release executables produced by the GitHub Actions workflow for a signed version tag may be submitted for signing:

- `PcCare-<version>-win-x64-offline.exe`
- `PcCare-<version>-win-x64-lite.exe`

The source repository, build scripts, GitHub Actions workflows and release metadata are part of the signed supply chain and are public.

## Team roles

- Committer and reviewer: [@ilivepc88-afk](https://github.com/ilivepc88-afk)
- Release signing approver: [@ilivepc88-afk](https://github.com/ilivepc88-afk)

All maintainers must enable multi-factor authentication on GitHub and SignPath. Changes contributed by people without commit access must be reviewed before merge. Each signing request requires explicit approval in SignPath; a GitHub tag alone never authorizes a signature.

## Release controls

1. A maintainer updates the project version and changelog, then pushes a `vX.Y.Z` tag matching the project version.
2. GitHub-hosted Windows runners restore, build and test the public source.
3. The workflow uploads the unsigned output as a GitHub Actions artifact and submits it to SignPath only when `SIGNPATH_ENABLED=true` is configured.
4. The release signing approver reviews the SignPath request and approves only the expected version, repository, workflow run and artifact paths.
5. GitHub Release assets are created only from the returned signed executables. SHA256 files are generated after signing.

No certificate private key, `.pfx` file, password or SignPath API token is stored in source control.

## Metadata restrictions

All signed files must use the product name `PcCare`; both executables in one release must use the same version metadata. The signed tag, project version, Release title and file names must agree.

## Privacy statement

This program will not transfer any information to other networked systems unless specifically requested by the user or the person installing or operating it.

PcCare does not include telemetry, automatic updates, remote command execution or a backend service. Its supported system changes are documented in the safety design and require user confirmation.

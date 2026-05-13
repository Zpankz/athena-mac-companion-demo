# Athena macOS Companion Demo

Athena is migrating to a native macOS companion architecture. The repository currently keeps the existing .NET/Avalonia implementation as the compatibility reference while adding a SwiftPM-native app framework for the macOS shell, Keychain, AVFoundation audio, screen capture, local media playback, and `gpt-realtime-2` session transport.

![Athena companion demo](videos/athena-demo.gif)

## Requirements

- Apple Swift 6 / Xcode command line tools for the native macOS app
- .NET 9 SDK
- macOS 12 or newer for the packaged `.app`
- `OPENAI_API_KEY` or an API key saved through Athena's setup window

## Run Native Swift App

```bash
swift run AthenaNative
```

## Test Native Swift App

```bash
swift test
```

## Build Native macOS App

```bash
./scripts/build-native-macos.sh 0.1.7
```

Artifacts are written to:

```text
artifacts/native-macos/Athena Native-0.1.7-osx-arm64.app
artifacts/native-macos/Athena Native-0.1.7-osx-arm64.zip
```

See [docs/native-macos-migration.md](docs/native-macos-migration.md) for the migration boundary and Realtime-2 integration contract.

## Run Compatibility .NET App

```bash
dotnet run --project ./AthenaCompanion/AthenaCompanion.csproj
```

## Test Compatibility .NET App

```bash
dotnet test ./AthenaCompanion.sln
```

## Build Compatibility macOS App

```bash
./scripts/build-macos.sh 0.1.7
```

Artifacts are written to:

```text
artifacts/macos/Athena Companion-0.1.7-osx-arm64.app
artifacts/macos/Athena Companion-0.1.7-osx-arm64.zip
```

The local app bundles are ad-hoc signed. Developer ID signing and notarization are still release-hardening steps.

## Music Mode

Athena can open a compact local music player from voice, text, or the app menu. Put `.mp3` or `.m4a` files under:

```text
~/Music/Athena Companion
```

On macOS, local playback uses the native `afplay` bridge.

## GitHub Release

Push a version tag to build and publish GitHub release artifacts:

```bash
git tag v0.1.7
git push origin v0.1.7
```

The release workflow builds the macOS `.app` zip and also keeps the existing Windows installer job.

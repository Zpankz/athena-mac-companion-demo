# Native macOS Migration

Athena now has a native SwiftPM macOS app framework next to the existing .NET implementation. The migration boundary is intentional:

- Swift owns the macOS app shell, SwiftUI scenes, Keychain access, AVFoundation microphone/output plumbing, native screen capture, local media playback, and the Realtime transport shape.
- The existing .NET project remains as the compatibility/reference implementation for tool behavior, sprite semantics, and deterministic tests.
- Shared behavior should be promoted through explicit interfaces and fixtures before any library rewrite.

## Native Targets

```text
Package.swift
Sources/AthenaNative
Sources/AthenaNativeCore
Tests/AthenaNativeCoreTests
```

`AthenaNativeCore` contains the reusable native boundaries:

- `RealtimeSessionConfiguration`: `gpt-realtime-2` session update payload, reasoning effort, semantic VAD, noise reduction, input transcription, PCM audio formats, and strict tool definitions.
- `RealtimeWebSocketClient`: stateful Realtime WebSocket event transport.
- `CoreAudioRealtimeBridge`: AVFoundation microphone capture and PCM playback bridge for 24 kHz mono PCM16.
- `KeychainOpenAIKeyStore`: native Keychain storage with `OPENAI_API_KEY` fallback through `CompositeOpenAIKeyStore`.
- `NativeScreenCaptureService`: CoreGraphics/ImageIO main-display PNG capture.
- `NativeMusicPlaybackService`: AVAudioPlayer-based local playback.

`AthenaNative` is the SwiftUI app shell. It wires settings, API-key setup, voice start/stop, and status display without porting every legacy window.

## Realtime-2 Integration Shape

The native session payload is deliberately more explicit than the interim Avalonia pass:

- model: `gpt-realtime-2`
- reasoning: low effort for low-latency desktop voice
- output modalities: audio only
- audio input: 24 kHz mono PCM
- audio output: PCM audio
- turn detection: `semantic_vad` with automatic eagerness and interruption enabled
- input audio transcription: `gpt-4o-transcribe` for async transcript context
- tools: strict `inspect_screen`, `create_screen_image`, and `open_music_player` schemas

The Swift tests assert this JSON shape so future migrations do not silently regress the Realtime contract.

## Validation

```bash
swift build --product AthenaNative
swift test
./scripts/build-native-macos.sh 0.1.7
```

The native app bundle is written to:

```text
artifacts/native-macos/Athena Native-0.1.7-osx-arm64.app
artifacts/native-macos/Athena Native-0.1.7-osx-arm64.zip
```

## Open Work

- Run an interactive microphone and speaker smoke test from the signed `.app`, because TCC behavior differs between SwiftPM executables and bundled apps.
- Wire native tool execution to the existing screen-analysis and image-generation client behavior.
- Decide whether sprite animation should be ported as SwiftUI/Canvas, SpriteKit, or a small AppKit transparent window.
- Replace the main-actor Realtime transport with a dedicated transport actor if socket/audio throughput becomes a measurable bottleneck.

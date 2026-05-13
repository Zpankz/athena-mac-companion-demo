# Athena Companion

Transparent Avalonia desktop companion window for macOS. This project is now the compatibility/reference implementation while the native SwiftPM app shell is introduced under `Sources/AthenaNative*`.

Athena walks just above the Dock area, pauses for pose animation, can spawn a small puppy companion, and exposes native menu/tray controls for pause, text chat, click-through, voice setup, music, and exit.

Left-click Athena to toggle voice pause mode. Click the small chat bubble to open text pause mode. Click the puppy icon to toggle Athena's autonomous puppy companion. Right-click Athena to open the context menu.

## Voice Agent Plan

Voice support is implemented as a Realtime WebSocket pass. Athena ships with no OpenAI API key; users provide their own key during setup, and the app stores it in macOS Keychain on macOS or Windows Credential Manager on Windows. Local development can continue to use the `OPENAI_API_KEY` environment variable.

The current macOS port compiles and runs the app shell, text mode, menu/key storage, screen-capture tool path, and native music playback path. Realtime microphone capture and PCM speaker output are isolated behind platform adapters and still need a native CoreAudio implementation before voice mode can complete a speech roundtrip on macOS.

Athena only listens while she is paused. Walking mode stops microphone capture and closes the Realtime session. See [docs/voice-agent-plan.md](docs/voice-agent-plan.md) for the implementation plan and privacy boundary.

Voice behavior:

- left-click Athena to pause and start voice mode
- left-click again to resume walking and stop voice mode
- right-click for the menu, including voice status, voice selection, and API key setup
- first voice use asks for an API key if neither macOS Keychain nor `OPENAI_API_KEY` has one
- default voice is `alloy`; selected voice is saved under the user's application-data settings

Pause-only voice tools:

- screen questions such as "what's on my screen?" capture the primary display and ask `gpt-5.5` for a concise spoken answer
- image requests such as "generate an infographic of what I am seeing" capture the primary display, prepare an image brief with `gpt-5.5`, generate a PNG with `gpt-image-2`, and open it in Athena's lightbox
- generated screen images are saved under the user's `Pictures/Athena Companion` folder
- screen capture is only triggered by an explicit voice tool request while Athena is paused

## Text Chat

Text chat is a separate pause mode from voice:

- click the chat bubble while Athena is walking to pause her and open the text chat window
- text mode uses `gpt-5.5` through the Responses API, not the Realtime voice WebSocket
- text mode has the same local tools as voice mode, including screen inspection and `gpt-image-2` image generation
- closing the text chat window resumes walking
- text mode does not start microphone capture

## Music Mode

Music mode is separate from voice and text:

- open it from Athena's tray menu or by asking Athena to play/open local music
- the default library folder is `~/Music/Athena Companion`
- Athena creates the folder on first run and scans it recursively for `.mp3` and `.m4a`
- if the folder is empty, the player shows the exact folder path and how to populate it
- entering music mode stops Realtime voice and microphone capture before playback begins
- macOS playback uses the native `afplay` bridge; the deterministic radio-effect transform remains covered by tests but is not yet in the playback path

## Puppy Companion

The puppy is a separate transparent click-through Avalonia window. It is session-only, follows Athena near the Dock area, wanders locally, and shows small bark bubbles without making OpenAI API calls.

Puppy assets live at:

```text
Assets/Sprites/puppy-atlas.png
Assets/Sprites/puppy-atlas.json
Assets/Sprites/puppy-atlas.prompt.txt
Assets/Icons/puppy-icon.png
```

Preview GIFs live next to the sprite atlas as `puppy-walk-preview.gif`, `puppy-idle-preview.gif`, and `puppy-bark-preview.gif`.

## Run

```bash
dotnet run --project ./AthenaCompanion.csproj
```

## Release

Build the self-contained macOS app locally from the repository root:

```bash
./scripts/build-macos.sh 0.1.7
```

The app bundle and zip are written to:

```text
artifacts/macos/Athena Companion-0.1.7-osx-arm64.app
artifacts/macos/Athena Companion-0.1.7-osx-arm64.zip
```

GitHub Actions also builds release artifacts when a `v*` tag is pushed:

```bash
git tag v0.1.7
git push origin v0.1.7
```

The workflow uploads the macOS app zip and the existing Windows installer artifact.

## App Icon

The generated Athena app icon lives at:

```text
Assets/Icons/athena.ico
```

The `gpt-image-2` source prompt is saved at:

```text
Assets/Icons/athena-icon.prompt.txt
```

## Sprite Atlas

The runtime looks for this generated atlas:

```text
Assets/Sprites/athena-atlas.png
```

Expected atlas format:

- 2048x768 PNG
- 8 columns by 3 rows
- 256x256 cells
- frames 1-15: right-facing walk cycle, curated from every other generated walk frame
- frames 16-24: idle/pose loop
- transparent background after chroma-key cleanup
- metadata: `Assets/Sprites/athena-atlas.json`

The exact `gpt-image-2` generation prompt is saved at:

```text
Assets/Sprites/athena-atlas.prompt.txt
```

Dry-run the image request:

```powershell
python 'C:\Users\asfar\.codex\skills\.system\imagegen\scripts\image_gen.py' generate `
  --prompt-file 'Assets\Sprites\athena-atlas.prompt.txt' `
  --model gpt-image-2 `
  --size 2048x1024 `
  --quality high `
  --out 'output\imagegen\athena-atlas-raw.png' `
  --no-augment `
  --dry-run
```

Generate the raw chroma-key source:

```powershell
python 'C:\Users\asfar\.codex\skills\.system\imagegen\scripts\image_gen.py' generate `
  --prompt-file 'Assets\Sprites\athena-atlas.prompt.txt' `
  --model gpt-image-2 `
  --size 2048x1024 `
  --quality high `
  --out 'output\imagegen\athena-atlas-raw.png' `
  --no-augment
```

Remove the chroma-key background:

```powershell
python 'C:\Users\asfar\.codex\skills\.system\imagegen\scripts\remove_chroma_key.py' `
  --input 'output\imagegen\athena-atlas-raw.png' `
  --out 'Assets\Sprites\athena-atlas.png' `
  --auto-key border `
  --soft-matte `
  --transparent-threshold 12 `
  --opaque-threshold 220 `
  --despill
```

Keep the transparent generated layout, then normalize it into fixed 256x256 cells:

```powershell
Copy-Item 'Assets\Sprites\athena-atlas.png' 'output\imagegen\athena-atlas-transparent-layout.png' -Force

python 'tools\normalize_athena_atlas.py' `
  --input 'output\imagegen\athena-atlas-transparent-layout.png' `
  --out 'Assets\Sprites\athena-atlas.png' `
  --preview 'output\imagegen\athena-atlas-normalized-preview.png' `
  --columns 8 `
  --rows 3 `
  --walk-frames 30 `
  --walk-stride 2 `
  --pose-frames 9
```

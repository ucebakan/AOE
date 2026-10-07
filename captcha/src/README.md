# 4Unity Puzzle Test v1.1

Standalone Windows x64 helper for the user's owned offline test client. Select the client and start monitoring before a puzzle appears. Automatic dialog location and matching are enabled by default; manual cropping and read-only operation remain available. The existing farm executable is separate and cannot be paused by this release.

Read KULLANIM.txt for the Turkish workflow. Normal EXE startup requires UAC approval (requireAdministrator, uiAccess=false). The self-contained release includes .NET. Windows OCR uses an installed English or Turkish recognizer, falling back to the user profile; languages are not downloaded automatically.

## Automatic detection

Whole-client OCR locates the unique puzzle title, aligned target label and stage label, then estimates the dialog bounds from their geometry. The cropped dialog is read independently. Supported symbols are SUN, STAR, MOON, LEAF, CROWN, GEM, SHIELD and SWORD. The fixed Target label tolerates its observed OCR spelling and punctuation; symbol names require exact recognition.

A read-only memory observer is provided for the exact TClient.exe build documented in [the profile](docs/puzzle_detection_profile.md). It validates the disk hash, loaded vtable methods, process creation time and UI object shape before observing visibility and puzzle mode. A bounded private-memory scan runs once per connection. Unsupported builds, access denial or missing/changed objects fall back to visual scanning. Memory never selects an answer or replaces the OCR checks.

The elevated game denied the development process's read-only connection with Win32 error 5. Static fields and parser checks are established; live open/closed transitions remain unverified. The packaged application requests administrator elevation on ordinary startup.

## Selection and input

The title, target, stage 1–4, remaining attempts and four distinct symbols in a 2×2 layout must be present. Two consecutive identical readings and a fresh third reading precede input. PID, creation time, path, HWND ownership, foreground, client geometry, ROI and sampled occlusion are checked before input.

Each stage receives at most one mouse down/up pair. A 12-second progression timeout stops the session; focus waiting pauses that clock. Stage jumps and premature disappearance fault. Completion requires three absent observations spanning at least one second after stage 4. An active memory signal prevents unreadable images from being treated as successful closure. Monitoring continues after completion and starts the next session at step 1.

Input uses PostMessage to the selected HWND; the system cursor is untouched. The actual game client's acceptance remains unverified. Screen reading and clicking require a visible foreground window; a connected memory observer can report activity while waiting for focus. F9 cancels pending decisions; Ctrl+Shift+F9 is a fallback if F9 is occupied. Startup failures are displayed and logged.

## Farm integration boundary

logs/integration-state.json is atomically replaced with timestamp, connection phase, selected process, frame/click counts, reading, memory observation and running/blocked/faulted/completed state. It is an observation interface, not connected farm control. A future consumer should stop on a stale/missing record, stopped/faulted helper or blocked state, and preserve its own user-stop state. Focus waiting publishes a blocked heartbeat. Events and cropped click frames are local.

## Build and verification

Build the csproj with dotnet build -c Release. The resulting DLL supports --self-test, --integration-test, --automatic-session-test, --auto-test followed by reference image paths, and --render-ui. Do not run visible window tests concurrently.

Publish with dotnet publish tools/4UnityPuzzleTest/4UnityPuzzleTest.csproj -c Release --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o dist/4UnityPuzzleTest-v1.1.

Passed: 41 state/parser/OCR checks; 38 automatic location and memory parser checks using four supplied images at three scales; 23 native fixture checks. The full application test begins with a hidden fixture puzzle, observes idle frames without input, then detects arrival and completes exactly four accepted selections with no manual area selection or user arrival notification. All input tests target this application's own fixture. These are not live game or live memory transition validation.

--analyze-files reads image files without input. --inspect-targets lists matching windows without screen capture. --memory-probe <pid> <path> attempts a read-only connection and writes its result. Reports are in verification.

OCR reference: [Microsoft OcrEngine.RecognizeAsync](https://learn.microsoft.com/en-us/uwp/api/windows.media.ocr.ocrengine.recognizeasync?view=winrt-26100).

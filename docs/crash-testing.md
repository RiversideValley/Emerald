# Testing launcher crashes

Build Debug, then select a launch profile in `Emerald/Properties/launchSettings.json`:

- **Crash - MainPage**: throws during essential shell initialization.
- **Crash - Dispatcher**: throws in an actual queued Uno dispatcher callback.
- **Crash - DesktopHost**: throws before desktop host construction.
- **Emerald (Desktop)**: normal startup, with `EMERALD_TEST=0`. Launch this after a crash profile to see recovery and history.
- **Crash - MainPage (WinAppSDK Packaged)**: packaged Windows equivalent of the MainPage test.
- **Crash - Dispatcher (WinAppSDK Packaged)**: packaged Windows equivalent of the dispatcher test.
- **Crash - Startup (WinAppSDK Packaged)**: throws at WinAppSDK launch, the packaged equivalent of the pre-host startup test.
- **Emerald (WinAppSDK Packaged)**: normal packaged Windows startup. Launch this after a packaged crash profile to see recovery and history.

Crash profiles deliberately throw once **per process**, every launch. Historical `.once` files are ignored; no cleanup or environment-variable removal is needed to repeat a test. An armed crash profile bypasses previous recovery prompts so it can reach its requested fault. Normal Desktop startup does not inject another fault and shows the saved crash automatically.

All test environment variables are effective only in Debug with `EMERALD_TEST=1`. The checked-in manual crash profiles leave `EMERALD_TEST_DATA_ROOT` empty, so they use exactly the same settings, lifecycle files, and report history as normal Desktop. These profiles intentionally crash your regular development instance; save ongoing work before using them.

Packaged WinAppSDK profiles use gated command-line activation arguments instead of `environmentVariables`, because Visual Studio's MSIX launcher does not reliably propagate profile environment variables. `--emerald-test=1` is still required before any packaged test option is honored, and the normal packaged profile explicitly passes `--emerald-test=0`. Packaged tests use the package's regular local app-data directory, so recovery appears when the ordinary packaged profile is launched next.

For automated tests, set `EMERALD_TEST_DATA_ROOT` to a writable absolute temporary directory. That isolates settings, reports, lifecycle files, and fallback diagnostics. To inspect that isolated run, retain the same root with `EMERALD_TEST=1` and an empty `EMERALD_TEST_CRASH`. Reports from the previous `/tmp/emerald-crash-test` profiles remain in that folder; they are not deleted or automatically imported into normal history.

Recovery uses one dialog: View shows details in place, Report and Open logs leave it open, and Continue starts the shell. After repeated startup failures, the same dialog explains recovery mode and uses an explicit Try normal startup button. There is no separate recovery launch profile or follow-up crash notification dialog.

Additional `EMERALD_TEST_CRASH` values for automated validation are `MainPage_Loaded_BeforeAwait`, `MainPage_Loaded_AfterAwait`, `AsyncVoidBeforeAwait`, `AsyncVoidAfterAwait`, `WorkerThread`, and `CaptureThenTerminate`. `OrdinaryError` and `UnobservedTask` exercise nonfatal paths. `EMERALD_TEST_DISABLE_STUDIO=1` disables Studio only while test mode is enabled.

## Real application process tests

Build the solution, then point the opt-in test suite at the Debug desktop executable (not the DLL):

```sh
dotnet build Emerald.slnx
EMERALD_PROCESS_TEST_APP="$PWD/Emerald/bin/Debug/net10.0-desktop/Emerald" \
  dotnet test Emerald.CoreX.Tests/Emerald.CoreX.Tests.csproj --no-build \
  --filter FullyQualifiedName~CrashProfileProcessTests
```

On Windows use the corresponding `.exe`. These tests need an interactive desktop session. They launch the actual app, repeat each fatal case using the same isolated directory, assert termination and exactly one report per run, then relaunch and wait for the actual recovery dialog's `Opened` event before host construction. Separate checks require normal startup and nonfatal errors to reach `ShellReady`. Test-owned processes are terminated and their temporary directories removed afterward. Native OS crash reports may still be retained by the OS.

An isolated Debug-only action seam (`EMERALD_TEST_RECOVERY_ACTION=view-continue`) exercises the same View and Continue callbacks as the dialog buttons. Process tests use it after one and three failed launches to verify acknowledgement, a single recovery dialog, and successful shell initialization. It is ignored without both test mode and an isolated data root.

Without `EMERALD_PROCESS_TEST_APP`, process tests are explicitly skipped; the ordinary CoreX suite still runs. An alive process alone is never treated as proof that startup or recovery succeeded. Dialog appearance and button interactions still need visual validation on Windows, macOS, and Linux.

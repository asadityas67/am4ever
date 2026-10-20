# am4ever Milestone 1: App Shell, Startup Warning, WinForms Look

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A runnable, portable am4ever app for Windows x64 and Linux x64. It opens with the mandatory red blinking risk warning, then shows an empty main window in a classic WinForms look with light and dark modes. A CLI enforces the same warning, and the repo has its licence, legal hygiene and CI checks from day one.

**Architecture:** One solution with four projects:
- `Am4ever.Core`: UI-free shared logic. In this milestone, the warning copy and the theme/acknowledgement policy.
- `Am4ever.Cli`: the `am4ever` command line.
- `Am4ever.App`: the Avalonia GUI.
- `Am4ever.Tests`: xUnit, including Avalonia headless UI tests and repo-hygiene tests.

The WinForms look is a custom Avalonia theme built from colour tokens, with light and dark variants keyed on Avalonia's `ThemeVariant`. Later milestones (ROM core, emulator, wizard, Board Lab, docs) plug into this shell.

**Tech Stack:**
- C# on .NET 8 (SDK 8.0.101, `net8.0`)
- Avalonia 11.2.3 (`Avalonia`, `Avalonia.Desktop`, `Avalonia.Themes.Simple`, `Avalonia.Headless.XUnit`)
- xUnit 2.9.2
- GitHub Actions

**Spec:** `G:\am4ever\docs\design.md` (§4 constraints, §6.0 startup warning, §10.1 legal hygiene, §13 decisions).

## Roadmap (separate plans, written when each milestone starts)

1. **M1 (this plan):** shell, warning, theme, CLI gate, legal hygiene, portable publish, CI.
2. **M2 ROM core:**
   - PSP directories
   - FV/FFS/sections
   - zlib, LZMA and Tiano decompression
   - recompression and checksum fix-ups
   - round-trip tests on the developer's local ROMs
3. **M3 Board model + operations:**
   - CPM tables (both formats)
   - APCB, verb tables, SMBIOS
   - profile schema and operations
   - ASRock P10.43 donor adapter
   - HP 285 G3 profile
   - semantic reference test against T5
4. **M4 Emulator:** Unicorn PEI/DXE harness, EC simulator, Keystone EC-init generator.
5. **M5 Beginner wizard:** the six steps of design §6.1.
6. **M6 Board Lab:** analysis report, workbench, profile export.
7. **M7 Docs:** learning path, how-tos, reference, cheat sheet; in-app viewer.

## Global Constraints

- **Target and toolchain:**
  - `net8.0`, C# `latest`
  - `<Nullable>enable</Nullable>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`
  - SDK pinned in `global.json` to `8.0.101` with `rollForward: latestFeature`
- **Avalonia:** every Avalonia package pinned to exactly `11.2.3`.
- **Licence:** GPL-2.0-or-later. SPDX header `// SPDX-License-Identifier: GPL-2.0-or-later` on every `.cs` file.
- **No network access** anywhere in the app or CLI.
- **No firmware in the repo.**
  - No tracked file over 262144 bytes.
  - No tracked file containing the byte sequences `_FVH`, `$PSP`, `$BHD`, `$PL2` or `$BL2`.
  - Exemption: anything under `docs/` and every `*.md` file, which can mention these strings as text.
- **Publish:** self-contained, single-file, for `win-x64` and `linux-x64`.
  - `PublishSingleFile=true`, `SelfContained=true`, `IncludeNativeLibrariesForSelfExtract=true`
  - `PublishTrimmed=false`
  - `DebugType=embedded`
- **Warning copy, verbatim (design §6.0):**
  - Banner: `WARNING: You are responsible for what you do. Flashing a modified BIOS can permanently kill your PC if you are not careful.`
  - Checkbox: `I understand and accept the risk`
  - Buttons: `Continue`, `Exit`
- **Warning behaviour:**
  - The acknowledgement is never persisted; the warning shows on every launch.
  - The banner alternates bright red `#E00000` and dark red `#6E0000` every 500 ms (1 Hz full cycle).
  - With reduce-motion on, it stays solid `#E00000` with a 6 px `#E00000` border.
- **CLI:**
  - The warning is printed in red on every run.
  - `build` needs `--i-understand-the-risk`, or an interactive `yes` when stdin is a terminal.
  - Exit code `2` when refused.
- **Not-affiliated line, verbatim:** `am4ever is not affiliated with AMD, AMI, ASRock, Biostar, HP, Dell or Lenovo.`
- **Theme:**
  - Modes are `FollowSystem` (default), `Light` and `Dark`, chosen from the `View > Theme` menu.
  - The chosen mode may be remembered (unlike the warning).

## Review Focus

Most likely first:

1. **Closing the warning with the title-bar X or Alt+F4.** Expected: the app exits and never opens the main window. Test `Warning_CloseButton_ExitsApp` in Task 4.
2. **Enter or Space while the box is unticked.** Expected: nothing proceeds; Continue stays disabled and is not the default button. Test `Warning_EnterKey_DoesNotContinueWhenUnticked` in Task 4.
3. **CLI with redirected stdin and no flag** (scripts, CI). Expected: refuses immediately with exit code 2 and does not hang waiting for input. Test `Build_NonInteractiveWithoutFlag_Refuses` in Task 3.
4. **Linux without `gsettings`** (KDE, minimal or headless systems). Expected: reduce-motion detection returns `false` without throwing, and the app starts. Test `LinuxReduceMotion_MissingGsettings_ReturnsFalse` in Task 4.
5. **OS switches light/dark while the app runs.** Expected: under `FollowSystem` the app follows; under explicit `Light`/`Dark` it doesn't. Test `ThemeService_FollowSystem_TracksPlatformChange` in Task 5.

---

### Task 1: Repository, solution and legal-hygiene guard

**Files:**
- Create: `global.json`, `Directory.Build.props`, `am4ever.sln`, `.gitignore`, `.gitattributes`
- Create: `LICENSE` (full GPL-2.0 text from https://www.gnu.org/licenses/old-licenses/gpl-2.0.txt)
- Create: `README.md`, `CONTRIBUTING.md`, `.github/ISSUE_TEMPLATE/bug_report.md`
- Create: `src/Am4ever.Core/Am4ever.Core.csproj`, `src/Am4ever.Cli/Am4ever.Cli.csproj`, `src/Am4ever.App/Am4ever.App.csproj`
- Create: `tests/Am4ever.Tests/Am4ever.Tests.csproj`
- Test: `tests/Am4ever.Tests/RepoHygieneTests.cs`

**Interfaces:**
- Produces: the solution and projects every later task adds files to. Project references are App→Core, Cli→Core, and Tests→Core, Cli and App.
- Produces: `RepoHygieneTests`, which runs in CI on every pull request.

- [ ] **Step 1: Initialise the repo and solution.**
  - Run `git init` in `G:\am4ever` and create a `main` branch.
  - `global.json`: `{"sdk":{"version":"8.0.101","rollForward":"latestFeature"}}`.
  - `Directory.Build.props` sets `TargetFramework net8.0`, `LangVersion latest`, `Nullable enable`, `TreatWarningsAsErrors true`, `ImplicitUsings enable`.
  - `Am4ever.App` is `WinExe` with Avalonia `11.2.3` packages (`Avalonia`, `Avalonia.Desktop`, `Avalonia.Themes.Simple`).
  - `Am4ever.Cli` is `Exe` with `AssemblyName am4ever`.
  - `Am4ever.Tests` uses xUnit `2.9.2`, `xunit.runner.visualstudio 2.8.2`, `Microsoft.NET.Test.Sdk 17.11.1` and `Avalonia.Headless.XUnit 11.2.3`.
  - `.gitignore`: standard .NET entries, `.superpowers/`, `publish/`, and ROM-like extensions `*.bin *.rom *.cap *.fd *.BSS`.
  - `.gitattributes`: `* text=auto eol=lf`, `*.cs text eol=lf`.

- [ ] **Step 2: Write the failing hygiene tests**

```csharp
// SPDX-License-Identifier: GPL-2.0-or-later
public class RepoHygieneTests
{
    [Fact] public void NoTrackedFileExceeds256KiB() =>
        Assert.Empty(RepoFiles.Tracked().Where(f => new FileInfo(f).Length > 262144));

    [Fact] public void NoTrackedNonDocFileContainsFirmwareSignatures() =>
        Assert.Empty(RepoFiles.Tracked()
            .Where(f => !RepoFiles.IsDoc(f))
            .Where(f => RepoFiles.ContainsAny(f, "_FVH", "$PSP", "$BHD", "$PL2", "$BL2")));

    [Fact] public void EveryCsFileHasSpdxHeader() =>
        Assert.Empty(RepoFiles.Tracked().Where(f => f.EndsWith(".cs"))
            .Where(f => !File.ReadLines(f).First().Contains("SPDX-License-Identifier: GPL-2.0-or-later")));

    [Fact] public void ReadmeStatesNotAffiliated() =>
        Assert.Contains("am4ever is not affiliated with AMD, AMI, ASRock, Biostar, HP, Dell or Lenovo.",
            File.ReadAllText(RepoFiles.Root("README.md")));

    [Fact] public void IssueTemplateAsksForBuildReportNotRoms()
    {
        var t = File.ReadAllText(RepoFiles.Root(".github", "ISSUE_TEMPLATE", "bug_report.md"));
        Assert.Contains("build report", t);
        Assert.Contains("Do not attach BIOS/ROM images", t);
    }

    [Fact] public void ContributingForbidsRoms() =>
        Assert.Contains("Never attach or commit BIOS/ROM images", File.ReadAllText(RepoFiles.Root("CONTRIBUTING.md")));
}
```

`RepoFiles` is a static test helper in the same project:
- `Root(params string[])` walks up from `AppContext.BaseDirectory` to the folder containing `am4ever.sln`.
- `Tracked()` runs `git ls-files -z`.
- `IsDoc(path)` is true under `docs/` or for `*.md` files.
- `ContainsAny(path, params string[])` searches the bytes, ASCII.

- [ ] **Step 3: Run the tests and see them fail**

Run: `dotnet test --filter RepoHygieneTests`
Expected: FAIL. README and CONTRIBUTING don't exist yet.

- [ ] **Step 4: Write `README.md`, `CONTRIBUTING.md` and the issue template**
  - **README:** what am4ever is (two paragraphs from design §1), its status ("Milestone 1: shell only"), and the build commands. Include a "No warranty / you can kill your PC" paragraph and the not-affiliated line verbatim.
  - **Issue template:** asks for the am4ever build report (`build report` appears in it), board model, CPU and what happened. It contains the exact sentence `Do not attach BIOS/ROM images or dumps.`
  - **CONTRIBUTING:** contains the exact sentence `Never attach or commit BIOS/ROM images, dumps or finished modded images, in issues, pull requests or anywhere else.` Add that profiles and donor adapters arrive as pull requests reviewed by the maintainer, and that build reports, not ROMs, go in issues.

- [ ] **Step 5: Run the tests and see them pass**

Run: `git add -A && dotnet test --filter RepoHygieneTests`
Expected: 6 passed. Files must be staged so `git ls-files` sees them.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "chore: solution skeleton, GPL-2.0 licence, legal-hygiene tests"
```

### Task 2: Risk-warning copy and acknowledgement policy (Core)

**Files:**
- Create: `src/Am4ever.Core/Safety/RiskWarning.cs`
- Test: `tests/Am4ever.Tests/Core/RiskWarningTests.cs`

**Interfaces:**
- Produces: `public static class RiskWarning` with
  - `const string Banner`
  - `IReadOnlyList<string> Details`
  - `const string AcceptLabel`
  - `const string ContinueLabel`, `const string ExitLabel`
  - `const string NotAffiliated`
- Produces: `public sealed class RiskAcknowledgement` with
  - `bool Accepted { get; }`
  - `void Accept()`
  - `static RiskAcknowledgement NewForThisLaunch()`

  No persistence API exists, by design.

- [ ] **Step 1: Write the failing tests**

```csharp
// SPDX-License-Identifier: GPL-2.0-or-later
public class RiskWarningTests
{
    [Fact] public void BannerIsVerbatim() => Assert.Equal(
        "WARNING: You are responsible for what you do. Flashing a modified BIOS can permanently kill your PC if you are not careful.",
        RiskWarning.Banner);

    [Fact] public void DetailsCoverWarrantyBackupProgrammerAffiliation()
    {
        var d = string.Join("\n", RiskWarning.Details);
        Assert.Contains("no warranty", d);
        Assert.Contains("GPL-2.0", d);
        Assert.Contains("backup", d);
        Assert.Contains("programmer", d);
        Assert.Contains(RiskWarning.NotAffiliated, d);
    }

    [Fact] public void LabelsAreVerbatim()
    {
        Assert.Equal("I understand and accept the risk", RiskWarning.AcceptLabel);
        Assert.Equal("Continue", RiskWarning.ContinueLabel);
        Assert.Equal("Exit", RiskWarning.ExitLabel);
    }

    [Fact] public void EveryLaunchStartsUnaccepted()
    {
        var first = RiskAcknowledgement.NewForThisLaunch(); first.Accept();
        Assert.False(RiskAcknowledgement.NewForThisLaunch().Accepted);
    }
}
```

- [ ] **Step 2: Run the tests and see them fail**

Run: `dotnet test --filter RiskWarningTests`
Expected: FAIL. `RiskWarning` is not defined.

- [ ] **Step 3: Implement `RiskWarning` and `RiskAcknowledgement`**

The `Details` lines, verbatim:
1. `am4ever comes with no warranty (GPL-2.0). You use it at your own risk.`
2. `Keep your original BIOS backup somewhere safe before you flash anything.`
3. `Only flash with an SPI programmer you know how to use, and know how to restore your backup.`
4. `RiskWarning.NotAffiliated`

- [ ] **Step 4: Run the tests and see them pass**

Run: `dotnet test --filter RiskWarningTests`
Expected: 4 passed.

- [ ] **Step 5: Commit**

```bash
git add src/Am4ever.Core tests/Am4ever.Tests/Core
git commit -m "feat(core): risk warning copy and per-launch acknowledgement"
```

### Task 3: CLI risk gate

**Files:**
- Create: `src/Am4ever.Cli/Program.cs`, `src/Am4ever.Cli/CliApp.cs`, `src/Am4ever.Cli/IConsoleIo.cs`
- Test: `tests/Am4ever.Tests/Cli/CliGateTests.cs`

**Interfaces:**
- Consumes: `RiskWarning`, `RiskAcknowledgement` (Task 2).
- Produces:
  - `public interface IConsoleIo { TextWriter Out { get; } TextWriter Err { get; } TextReader In { get; } bool StdinIsTerminal { get; } bool SupportsColor { get; } }`
  - `public static class CliApp { public static int Run(string[] args, IConsoleIo io); }`
  - `Program.Main` calls `CliApp.Run` with a real console implementation.
- Exit codes:
  - `0`: OK, including `--version`, `--help`, and an accepted `build`, which in M1 prints `build: not implemented yet (milestone 3)` and returns 0.
  - `2`: risk refused.
  - `64`: usage error.

- [ ] **Step 1: Write the failing tests**

```csharp
// SPDX-License-Identifier: GPL-2.0-or-later
public class CliGateTests
{
    [Fact] public void EveryRunPrintsBannerInRed()
    {
        var io = FakeIo.Create(color: true);
        CliApp.Run(new[] { "--version" }, io);
        Assert.Contains("\u001b[31m" + RiskWarning.Banner, io.ErrText);
    }

    [Fact] public void Build_WithFlag_Proceeds() =>
        Assert.Equal(0, CliApp.Run(new[] { "build", "--i-understand-the-risk" }, FakeIo.Create()));

    [Fact] public void Build_NonInteractiveWithoutFlag_Refuses()
    {
        var io = FakeIo.Create(stdinIsTerminal: false, stdin: "");
        Assert.Equal(2, CliApp.Run(new[] { "build" }, io));
        Assert.Contains("--i-understand-the-risk", io.ErrText);
    }

    [Fact] public void Build_InteractiveYes_Proceeds() =>
        Assert.Equal(0, CliApp.Run(new[] { "build" }, FakeIo.Create(stdinIsTerminal: true, stdin: "yes\n")));

    [Fact] public void Build_InteractiveAnythingElse_Refuses() =>
        Assert.Equal(2, CliApp.Run(new[] { "build" }, FakeIo.Create(stdinIsTerminal: true, stdin: "y\n")));

    [Fact] public void UnknownCommand_IsUsageError() =>
        Assert.Equal(64, CliApp.Run(new[] { "frobnicate" }, FakeIo.Create()));
}
```

`FakeIo` is a test helper. It builds an `IConsoleIo` over `StringWriter`s and a `StringReader`, and exposes `ErrText`.

- [ ] **Step 2: Run the tests and see them fail**

Run: `dotnet test --filter CliGateTests`
Expected: FAIL. `CliApp` is not defined.

- [ ] **Step 3: Implement `CliApp.Run`**
  - Hand-rolled argument parsing, no command-line library.
  - The banner and `Details` go to `Err`, wrapped in `\u001b[31m … \u001b[0m` when `SupportsColor`.
  - The interactive prompt reads exactly one line and accepts only `yes`, case-insensitive and trimmed.
  - Never read stdin when `StdinIsTerminal` is false.
  - The real console's `SupportsColor` is `!Console.IsErrorRedirected && Environment.GetEnvironmentVariable("NO_COLOR") is null`.

- [ ] **Step 4: Run the tests and see them pass**

Run: `dotnet test --filter CliGateTests`
Expected: 6 passed.

- [ ] **Step 5: Commit**

```bash
git add src/Am4ever.Cli tests/Am4ever.Tests/Cli
git commit -m "feat(cli): red risk banner and --i-understand-the-risk gate"
```

### Task 4: Startup warning window

**Files:**
- Create: `src/Am4ever.App/Safety/IReduceMotionProvider.cs`, `src/Am4ever.App/Safety/ReduceMotionProviders.cs`
- Create: `src/Am4ever.App/Safety/RiskWarningWindow.axaml`, `src/Am4ever.App/Safety/RiskWarningWindow.axaml.cs`
- Test: `tests/Am4ever.Tests/App/RiskWarningWindowTests.cs`, `tests/Am4ever.Tests/App/ReduceMotionTests.cs`, `tests/Am4ever.Tests/App/TestAppBuilder.cs`

**Interfaces:**
- Consumes: `RiskWarning`, `RiskAcknowledgement` (Task 2); WinForms theme resources by key (Task 5; until then default colours are fine).
- Produces:
  - `public interface IReduceMotionProvider { bool ReduceMotion { get; } }`
  - Implementations: `WindowsReduceMotionProvider` (P/Invoke `SystemParametersInfo(SPI_GETCLIENTAREAANIMATION = 0x1042)`; reduce = animation off), `LinuxReduceMotionProvider` (runs `gsettings get org.gnome.desktop.interface enable-animations` with a 2 s timeout; reduce = output `false`; any failure → `false`), and `EnvReduceMotionProvider` (`AM4EVER_REDUCE_MOTION=1` → true; it wraps the platform provider and wins over it).
  - `public partial class RiskWarningWindow : Window`
    - `ctor(RiskAcknowledgement ack, IReduceMotionProvider motion, TimeProvider? time = null)`
    - `bool Accepted` is set only by Continue.
    - `bool BannerBright { get; }`, exposed for tests.
    - `Thickness BannerBorderThickness { get; }`
    - `Task<bool> ShowAndWaitAsync()` returns `Accepted` after the window closes.
  - Control names (`x:Name`): `Banner`, `BannerText`, `AcceptCheck`, `ContinueButton`, `ExitButton`.
- Behaviour:
  - The window is modal, fixed size 560×360 and not resizable.
  - Its title is `am4ever: read this first`.
  - Continue is enabled only while `AcceptCheck` is ticked, and is never `IsDefault`.
  - Exit is `IsCancel`.
  - Closing the window by any means other than Continue leaves `Accepted=false`.

- [ ] **Step 1: Write the failing tests**

```csharp
// SPDX-License-Identifier: GPL-2.0-or-later
public class RiskWarningWindowTests
{
    static RiskWarningWindow Make(bool reduce = false, FakeTimeProvider? t = null) =>
        new(RiskAcknowledgement.NewForThisLaunch(), new FixedReduceMotion(reduce), t);

    [AvaloniaFact] public void ShowsVerbatimCopy()
    {
        var w = Make(); w.Show();
        Assert.Equal(RiskWarning.Banner, w.FindControl<TextBlock>("BannerText")!.Text);
        Assert.Equal(RiskWarning.AcceptLabel, w.FindControl<CheckBox>("AcceptCheck")!.Content);
    }

    [AvaloniaFact] public void ContinueDisabledUntilTicked()
    {
        var w = Make(); w.Show();
        var c = w.FindControl<Button>("ContinueButton")!;
        Assert.False(c.IsEnabled);
        w.FindControl<CheckBox>("AcceptCheck")!.IsChecked = true;
        Assert.True(c.IsEnabled);
        Assert.False(c.IsDefault);
    }

    [AvaloniaFact] public void Warning_EnterKey_DoesNotContinueWhenUnticked()
    {
        var w = Make(); w.Show();
        w.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Assert.False(w.Accepted);
        Assert.True(w.IsVisible);
    }

    [AvaloniaFact] public void Warning_CloseButton_ExitsApp()
    {
        var w = Make(); w.Show();
        w.FindControl<CheckBox>("AcceptCheck")!.IsChecked = true;
        w.Close();                       // title-bar X / Alt+F4 path
        Assert.False(w.Accepted);
    }

    [AvaloniaFact] public void BlinksEvery500ms()
    {
        var t = new FakeTimeProvider(); var w = Make(t: t); w.Show();
        var first = w.BannerBright;
        t.Advance(TimeSpan.FromMilliseconds(500)); Dispatcher.UIThread.RunJobs();
        Assert.NotEqual(first, w.BannerBright);
        t.Advance(TimeSpan.FromMilliseconds(500)); Dispatcher.UIThread.RunJobs();
        Assert.Equal(first, w.BannerBright);
    }

    [AvaloniaFact] public void ReduceMotion_SolidWithThickBorder()
    {
        var t = new FakeTimeProvider(); var w = Make(reduce: true, t: t); w.Show();
        Assert.True(w.BannerBright);
        t.Advance(TimeSpan.FromSeconds(2)); Dispatcher.UIThread.RunJobs();
        Assert.True(w.BannerBright);
        Assert.Equal(new Thickness(6), w.BannerBorderThickness);
    }
}

public class ReduceMotionTests
{
    [Fact] public void LinuxReduceMotion_MissingGsettings_ReturnsFalse() =>
        Assert.False(new LinuxReduceMotionProvider(gsettingsPath: "/nonexistent/gsettings").ReduceMotion);

    [Fact] public void EnvOverrideWins() =>
        Assert.True(new EnvReduceMotionProvider(new FixedReduceMotion(false), env: _ => "1").ReduceMotion);
}
```

Helpers:
- `FakeTimeProvider` comes from the `Microsoft.Extensions.TimeProvider.Testing 8.10.0` package; add it to the test project.
- `FixedReduceMotion` is a one-line test fake.
- `TestAppBuilder` uses `[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]` and builds `App` with `UseHeadless(new AvaloniaHeadlessPlatformOptions())`.
- `LinuxReduceMotionProvider` and `EnvReduceMotionProvider` take the gsettings path and the env lookup as constructor parameters, for tests.

- [ ] **Step 2: Run the tests and see them fail**

Run: `dotnet test --filter "RiskWarningWindowTests|ReduceMotionTests"`
Expected: FAIL. The types are not defined.

- [ ] **Step 3: Implement the providers and `RiskWarningWindow`**
  - The blink uses `TimeProvider.CreateTimer` with a 500 ms period, marshalled to `Dispatcher.UIThread.Post`.
  - Each tick toggles `BannerBright` and the banner background: bright `#E00000` with white text, dark `#6E0000` with white text.
  - Under reduce-motion, no timer is created, the banner stays bright, and its border is 6 px `#E00000`.
  - The checkbox and buttons use the theme's classic styles.

- [ ] **Step 4: Run the tests and see them pass**

Run: `dotnet test --filter "RiskWarningWindowTests|ReduceMotionTests"`
Expected: 8 passed.

- [ ] **Step 5: Commit**

```bash
git add src/Am4ever.App/Safety tests/Am4ever.Tests/App
git commit -m "feat(app): blinking startup risk warning with reduce-motion fallback"
```

### Task 5: WinForms theme (light + dark) and theme switching

**Files:**
- Create: `src/Am4ever.App/Theme/WinFormsTheme.axaml`, `src/Am4ever.App/Theme/WinFormsColors.axaml`
- Create: `src/Am4ever.App/Theme/ThemeService.cs`, `src/Am4ever.App/Theme/ThemeSettingsStore.cs`
- Modify: `src/Am4ever.App/App.axaml`, `src/Am4ever.App/App.axaml.cs`
- Test: `tests/Am4ever.Tests/App/ThemeTests.cs`

**Interfaces:**
- Produces:
  - `public enum ThemeMode { FollowSystem, Light, Dark }`
  - `public sealed class ThemeService`
    - `ctor(Application app, IPlatformThemeSource platform, ThemeSettingsStore store)`
    - `ThemeMode Mode { get; }`
    - `void SetMode(ThemeMode mode)`
    - `ThemeVariant Effective { get; }`
  - `public interface IPlatformThemeSource { ThemeVariant Current { get; } event EventHandler? Changed; }`
    - Real implementation: `public sealed class PlatformThemeSource : IPlatformThemeSource` with `static PlatformThemeSource FromApplication(Application app)`. It wraps `Application.Current.PlatformSettings` (`GetColorValues().ThemeVariant` and `ColorValuesChanged`).
  - `ThemeSettingsStore` reads and writes `{"theme":"FollowSystem|Light|Dark"}` at `<AppData>/am4ever/settings.json`:
    - Windows: `%APPDATA%`
    - Linux: `$XDG_CONFIG_HOME`, or `~/.config`
    - Any read failure → `FollowSystem`.
- Colour tokens are `SolidColorBrush` resources, keyed per `ThemeVariant` in `ThemeDictionaries`:

  | Key | Light | Dark |
  |---|---|---|
  | `am4.Control` | `#F0F0F0` | `#2D2D30` |
  | `am4.ControlText` | `#000000` | `#F1F1F1` |
  | `am4.Window` | `#FFFFFF` | `#1E1E1E` |
  | `am4.ControlLightLight` | `#FFFFFF` | `#4A4A4F` |
  | `am4.ControlDark` | `#A0A0A0` | `#1A1A1C` |
  | `am4.ControlDarkDark` | `#696969` | `#000000` |
  | `am4.Highlight` | `#0078D7` | `#0A64AD` |
  | `am4.HighlightText` | `#FFFFFF` | `#FFFFFF` |
  | `am4.GrayText` | `#6D6D6D` | `#9A9A9A` |

- Control styles over `SimpleTheme`:
  - `Button`: raised 3D borders (top/left `am4.ControlLightLight`, bottom/right `am4.ControlDarkDark`, 1 px each, 2 px total) on `am4.Control`. Pressed state swaps the edges.
  - `TextBox`: sunken border on `am4.Window`.
  - `CheckBox`
  - `HeaderedContentControl` with class `groupbox`: an etched border with the title over it.
  - `TabControl`/`TabItem`, `Menu`/`MenuItem`
  - `StatusBar`: a `Border` with class `statusbar`.
  - Font: `Segoe UI, Ubuntu, DejaVu Sans, sans-serif`, 12 px.

- [ ] **Step 1: Write the failing tests**

```csharp
// SPDX-License-Identifier: GPL-2.0-or-later
public class ThemeTests
{
    [AvaloniaFact] public void LightAndDarkControlColoursDiffer()
    {
        var app = Application.Current!;
        Assert.True(app.TryGetResource("am4.Control", ThemeVariant.Light, out var l));
        Assert.True(app.TryGetResource("am4.Control", ThemeVariant.Dark, out var d));
        Assert.Equal(Color.Parse("#F0F0F0"), ((SolidColorBrush)l!).Color);
        Assert.Equal(Color.Parse("#2D2D30"), ((SolidColorBrush)d!).Color);
    }

    [AvaloniaFact] public void ExplicitModeIgnoresPlatform()
    {
        var p = new FakePlatformTheme(ThemeVariant.Light);
        var s = new ThemeService(Application.Current!, p, ThemeSettingsStore.InMemory());
        s.SetMode(ThemeMode.Dark);
        p.Raise(ThemeVariant.Light);
        Assert.Equal(ThemeVariant.Dark, Application.Current!.RequestedThemeVariant);
    }

    [AvaloniaFact] public void ThemeService_FollowSystem_TracksPlatformChange()
    {
        var p = new FakePlatformTheme(ThemeVariant.Light);
        var s = new ThemeService(Application.Current!, p, ThemeSettingsStore.InMemory());
        s.SetMode(ThemeMode.FollowSystem);
        p.Raise(ThemeVariant.Dark);
        Assert.Equal(ThemeVariant.Dark, s.Effective);
    }

    [Fact] public void CorruptSettingsFallBackToFollowSystem()
    {
        var path = Path.GetTempFileName(); File.WriteAllText(path, "{not json");
        Assert.Equal(ThemeMode.FollowSystem, new ThemeSettingsStore(path).Load());
    }
}
```

`FakePlatformTheme` implements `IPlatformThemeSource`, with `Raise(variant)` firing `Changed`. `ThemeSettingsStore.InMemory()` is a factory for tests.

- [ ] **Step 2: Run the tests and see them fail**

Run: `dotnet test --filter ThemeTests`
Expected: FAIL. The resources and `ThemeService` are missing.

- [ ] **Step 3: Implement the theme files and `ThemeService`**
  - `App.axaml` includes `<SimpleTheme/>` then `WinFormsTheme.axaml`.
  - `ThemeService.SetMode` sets `RequestedThemeVariant` to `ThemeVariant.Default`, `Light` or `Dark`, and saves the mode.

- [ ] **Step 4: Run the tests and see them pass**

Run: `dotnet test --filter ThemeTests`
Expected: 4 passed.

- [ ] **Step 5: Commit**

```bash
git add src/Am4ever.App/Theme src/Am4ever.App/App.axaml* tests/Am4ever.Tests/App/ThemeTests.cs
git commit -m "feat(app): classic WinForms theme with light/dark and follow-system"
```

### Task 6: Main window shell and startup flow

**Files:**
- Create: `src/Am4ever.App/MainWindow.axaml`, `src/Am4ever.App/MainWindow.axaml.cs`, `src/Am4ever.App/AboutWindow.axaml`, `src/Am4ever.App/AboutWindow.axaml.cs`
- Create: `src/Am4ever.App/Program.cs`, `src/Am4ever.App/StartupFlow.cs`
- Modify: `src/Am4ever.App/App.axaml.cs`
- Test: `tests/Am4ever.Tests/App/StartupFlowTests.cs`, `tests/Am4ever.Tests/App/MainWindowTests.cs`

**Interfaces:**
- Consumes:
  - `RiskWarningWindow(ack, motion, time)` and `ShowAndWaitAsync()` (Task 4)
  - `ThemeService`, `ThemeMode` (Task 5)
  - `RiskWarning.NotAffiliated` (Task 2)
- Produces:
  - `public sealed class StartupFlow`
    - `ctor(Func<Task<bool>> showWarning, Func<Window> createMain, Action shutdown)`
    - `Task<Window?> RunAsync()` returns the main window, or `null` after calling `shutdown`.
  - `MainWindow`
    - `ctor(ThemeService? themes = null)`. When `themes` is null it builds `new ThemeService(Application.Current!, PlatformThemeSource.FromApplication(Application.Current!), ThemeSettingsStore.InMemory())`, so tests can construct it directly; `App` passes the real one with the on-disk store.
    - Title `am4ever`, 900×600.
    - Menu: `File > Exit`; `View > Theme > Follow system | Light | Dark` (radio, wired to `ThemeService`); `Help > About`.
    - Body: a `groupbox` titled `Getting started` with the text `The beginner wizard and Board Lab arrive in later milestones.`
    - Status bar: `Ready`.
    - Control names: `ThemeFollowItem`, `ThemeLightItem`, `ThemeDarkItem`, `AboutItem`, `StatusText`.
  - `AboutWindow` shows `am4ever`, the version from the assembly informational version, `GPL-2.0-or-later` and `RiskWarning.NotAffiliated`.
- `App.OnFrameworkInitializationCompleted` builds the real providers and runs `StartupFlow`. The warning window is shown before any other window is created.

- [ ] **Step 1: Write the failing tests**

```csharp
// SPDX-License-Identifier: GPL-2.0-or-later
public class StartupFlowTests
{
    [Fact] public async Task Declined_ShutsDownAndNeverCreatesMain()
    {
        bool created = false, shut = false;
        var w = await new StartupFlow(() => Task.FromResult(false), () => { created = true; return null!; }, () => shut = true).RunAsync();
        Assert.Null(w); Assert.True(shut); Assert.False(created);
    }

    [AvaloniaFact] public async Task Accepted_CreatesMain()
    {
        var w = await new StartupFlow(() => Task.FromResult(true), () => new MainWindow(), () => { }).RunAsync();
        Assert.IsType<MainWindow>(w);
    }
}

public class MainWindowTests
{
    [AvaloniaFact] public void ThemeMenuSwitchesTheme()
    {
        var m = new MainWindow(); m.Show();
        m.FindControl<MenuItem>("ThemeDarkItem")!.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Assert.Equal(ThemeVariant.Dark, Application.Current!.RequestedThemeVariant);
    }

    [AvaloniaFact] public void AboutShowsNotAffiliatedAndLicence()
    {
        var a = new AboutWindow(); a.Show();
        var text = string.Join("\n", a.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text));
        Assert.Contains(RiskWarning.NotAffiliated, text);
        Assert.Contains("GPL-2.0-or-later", text);
    }
}
```

- [ ] **Step 2: Run the tests and see them fail**

Run: `dotnet test --filter "StartupFlowTests|MainWindowTests"`
Expected: FAIL. The types are missing.

- [ ] **Step 3: Implement `StartupFlow`, `MainWindow`, `AboutWindow` and the wiring in `App`/`Program`**
  - `Program.Main` uses `BuildAvaloniaApp().StartWithClassicDesktopLifetime(args)`.
  - The lifetime's `ShutdownMode` is `OnExplicitShutdown`, so closing the warning window doesn't leave a zombie process. `shutdown` calls `desktop.Shutdown(0)`.
  - Closing the main window shuts the app down.

- [ ] **Step 4: Run the tests and see them pass**

Run: `dotnet test --filter "StartupFlowTests|MainWindowTests"`
Expected: 4 passed.

- [ ] **Step 5: Run the app and check it by eye**

Run: `dotnet run --project src/Am4ever.App`

Expected:
- The warning appears first, with a red banner blinking about once per second.
- Continue is greyed out until the box is ticked.
- Exit closes everything.
- Continue opens the grey WinForms-style main window.
- `View > Theme > Dark` turns it dark.
- Help > About shows the not-affiliated line.

Take a screenshot of the warning and of the main window in both themes for the user.

- [ ] **Step 6: Commit**

```bash
git add src/Am4ever.App tests/Am4ever.Tests/App
git commit -m "feat(app): main window shell, about box, warning-first startup flow"
```

### Task 7: Portable publish and CI

**Files:**
- Create: `src/Am4ever.App/Properties/PublishProfiles/win-x64.pubxml`, `linux-x64.pubxml` (and the same two for `Am4ever.Cli`)
- Create: `.github/workflows/ci.yml`, `scripts/publish.ps1`, `scripts/publish.sh`
- Test: `tests/Am4ever.Tests/PublishSettingsTests.cs`

**Interfaces:**
- Produces:
  - `scripts/publish.*` writes `publish/am4ever-<rid>/` containing exactly one GUI file and one CLI file: `am4ever-gui(.exe)` and `am4ever(.exe)`.
  - The CI workflow runs on `push` and `pull_request` with an `ubuntu-latest` + `windows-latest` matrix: `dotnet test`, then the publish script.
  - It uploads `publish/am4ever-<rid>` as artifacts.
  - On `ubuntu-latest`, it smoke-runs `./am4ever --version` and expects exit 0 and the banner on stderr.

- [ ] **Step 1: Write the failing test**

```csharp
// SPDX-License-Identifier: GPL-2.0-or-later
public class PublishSettingsTests
{
    [Theory]
    [InlineData("src/Am4ever.App/Properties/PublishProfiles/win-x64.pubxml")]
    [InlineData("src/Am4ever.App/Properties/PublishProfiles/linux-x64.pubxml")]
    [InlineData("src/Am4ever.Cli/Properties/PublishProfiles/win-x64.pubxml")]
    [InlineData("src/Am4ever.Cli/Properties/PublishProfiles/linux-x64.pubxml")]
    public void ProfileIsPortableSingleFile(string rel)
    {
        var x = XDocument.Load(RepoFiles.Root(rel)).Descendants().ToDictionary(e => e.Name.LocalName, e => e.Value, StringComparer.Ordinal);
        Assert.Equal("true", x["PublishSingleFile"]);
        Assert.Equal("true", x["SelfContained"]);
        Assert.Equal("true", x["IncludeNativeLibrariesForSelfExtract"]);
        Assert.Equal("false", x["PublishTrimmed"]);
        Assert.Equal("embedded", x["DebugType"]);
    }
}
```

`RepoFiles.Root` is from Task 1. Use `GroupBy` + `First` if a key repeats.

- [ ] **Step 2: Run the test and see it fail**

Run: `dotnet test --filter PublishSettingsTests`
Expected: FAIL. The profiles don't exist.

- [ ] **Step 3: Create the four publish profiles, both scripts and `ci.yml`**
  - Each `.pubxml` also sets `RuntimeIdentifier`, `Configuration Release` and `PublishDir ../../publish/am4ever-<rid>/`.
  - The GUI project sets `AssemblyName am4ever-gui`.

- [ ] **Step 4: Run the test, then publish for real**

Run: `dotnet test --filter PublishSettingsTests` → 4 passed.

Run: `pwsh scripts/publish.ps1`

Expected: `publish/am4ever-win-x64/` holds exactly `am4ever-gui.exe` and `am4ever.exe`. `DebugType=embedded` means there are no `.pdb` files.

Then run `publish/am4ever-win-x64/am4ever.exe --version`. Expected: exit 0, red banner, and the version line.

- [ ] **Step 5: Run the full suite**

Run: `dotnet test`
Expected: all tests from Tasks 1–7 pass. That's 36 tests, and the hygiene tests still pass with the publish output ignored by git.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "build: portable single-file publish for win-x64/linux-x64 and CI"
```

- [ ] **Step 7: Hand over to the user.**
  - Send the screenshots from Task 6 Step 5 and the Windows binaries.
  - Explain how to create the public GitHub repo, since `gh` isn't installed here: create an empty repo named `am4ever` on github.com, then run `git remote add origin … && git push -u origin main`.
  - CI then builds the Linux binary.

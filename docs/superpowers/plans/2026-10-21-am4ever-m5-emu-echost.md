# am4ever Milestone 5: Out-of-Process EC Emulator Host

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Verify the HP EC-fan driver (`HpEcFanInit`, from M4) by actually running it in an x86 emulator against a simulated ACPI EC, and confirm it writes `EC[0x7E]=0xE7`, clears bit 1 of `EC[0x80]`, turns on the LPC 0x62/0x66 decode, and returns within a bounded timeout when no EC answers. This is the first use of Unicorn in am4ever and establishes the out-of-process native-host pattern the rest of the emulator (the PEI CPM harness) will reuse in a later milestone.

**Architecture:** Unicorn cannot run in-process inside a .NET app on CET-enabled Windows (the .NET process has user shadow stack on; Unicorn's TCG uses `longjmp`, which raises a control-protection fault — proven during M5 spiking, exit `0xC0000409`). The fix, validated by spike: a small **native host** program (`uc_host`), compiled as a plain native exe (no CET shadow stack, like `python.exe`), embeds Unicorn and runs the emulation; the .NET side invokes it as a subprocess and parses its output. This also gives one code path for Windows and Linux.
- `native/uc_host/uc_host.c` — C program. Subcommand `ecfan`: load a PE, set up Unicorn x86-64, simulate the EC on ports 0x62/0x66 and PCI config on 0xCF8/0xCFC, run the driver, print the results as `key=value` lines. Ports the `Dxe` and `Ec` classes of `G:\HPBIOS\tools\emulate_modules.py`.
- `src/Am4ever.Emu/EcEmulator.cs` — .NET wrapper: writes the PE to a temp file, runs `uc_host ecfan …`, parses stdout into an `EcResult`, locates the host binary + its Unicorn native lib.
- Native binaries are **never committed** (they exceed the 256 KiB tracked-file limit). A build script produces them into a git-ignored `native/out/<rid>/`; CI builds them into release artifacts. Emulator tests skip when the host is absent.

**Tech Stack:**
- C (C11), built with MSVC (`cl`) on Windows and `cc` on Linux; links Unicorn 2.1.x.
- Unicorn native library: `unicorn.dll` (win-x64, MSVC Release — built from source during M5 spiking) / `libunicorn.so` (linux-x64, from the `UnicornEngine.Unicorn` 2.1.3 NuGet package's `runtimes/linux-x64/native`).
- .NET 8, `System.Diagnostics.Process` for the subprocess. xUnit 2.9.2.

**Spec:** `G:\am4ever\docs\design.md` §5.4 (emulator: the DXE EC simulation and the "EC absent must time out" check), §4 (native helpers bundled per platform; GPLv2-compatible), §10 (verification). Byte/behaviour reference: the `Ec`, `Dxe`, and `run_dxe` code in `G:\HPBIOS\tools\emulate_modules.py` (lines ~426–587).

**Branch:** `m5-emu-echost`, cut from `m4-ec-fan`. M1–M4 stay un-merged.

**Scope boundary:** DXE EC verification only. The PEI harness (fake PEI services, CPM table walk under every CPU family — the larger half of `emulate_modules.py`) and Keystone-based EC-init composition are a later milestone. This milestone proves the native-host plumbing on the smallest valuable check.

## Global Constraints

- **Inherited (.NET side):** `net8.0`, nullable, warnings-as-errors, SPDX header on every `.cs` file, no network at runtime, no tracked file over 262144 bytes, no firmware signatures in source.
- **No native binaries in git.** `unicorn.dll`, `libunicorn.so`, and `uc_host`/`uc_host.exe` are produced into `native/out/` (git-ignored, added to `.gitignore`), never committed. `native/uc_host/uc_host.c` and the build scripts ARE committed (text, small).
- **The native host is a plain native exe** — on Windows it must NOT be marked CET-compatible (do not pass `/CETCOMPAT`), so the process has no user shadow stack and Unicorn's `longjmp` works. The build script asserts this premise by running the host's self-test.
- **Licence:** `uc_host.c` is GPL-2.0-or-later (SPDX header). Unicorn is GPLv2-compatible; bundling its native lib is fine. No firmware is ever passed to or embedded in the host.
- **EC simulation values (from `emulate_modules.py`):** ports 0x62 (data) / 0x66 (cmd+status); EC command 0x80 = read, 0x81 = write; status IBF (bit 1) reads 0 (instant EC), OBF (bit 0) set when a read result is pending; PCI config index 0xCF8 selects D14F3x44 with the value `0x8000A344`, data at 0xCFC. Image base `0x10000000`, stack `0x20000000`, fake system table `0x30000000`, return address `0x30008000`. EC-absent reads return `0xFF`.

## Review Focus

Most likely first:

1. **The native host binary is missing** (fresh checkout, not built). Expected: emulator tests skip with a message telling the developer to run the native build script; they never fail the suite or throw an opaque "file not found". Test `EcEmulator` tests gated by `[EmuFact]` (Task 2).
2. **The driver loops forever when the EC never answers** (a bug in the timeout). Expected: the host stops at a bounded instruction count and reports a non-hang; `EcEmulator` surfaces it as a result, not a hang. Test `EcAbsent_ReturnsBoundedTimeout` (Task 3).
3. **The host crashes or emits malformed output** (a bad PE, an emulator fault). Expected: `EcEmulator` throws a clean `EmulatorException` with the host's stderr/exit code, not a parse crash. Test `Run_HostFailure_ThrowsClean` (Task 2).
4. **The driver writes the wrong EC register / misses the LPC decode.** Expected: the verification asserts each of `EC[0x7E]`, `EC[0x80]`, LPC bit 30 and reports which differs. Test `HpEcFanInit_PerformsEcHandoff` (Task 3).

---

### Task 1: `uc_host` native EC-emulator program + build scripts

**Files:**
- Create: `native/uc_host/uc_host.c`, `native/build.sh`, `native/build.cmd`, `native/README.md`
- Modify: `.gitignore` (add `native/out/`)

**Interfaces:**
- Produces: a native executable `native/out/<rid>/uc_host[.exe]` beside its Unicorn native lib. CLI:
  `uc_host ecfan --pe <file> [--no-ec] [--reg <hex>=<hex>]…` — maps the PE, runs it against the simulated EC, and prints to stdout, one per line:
  `status=<int>` (0 ok / 1 did-not-return / 2 crash), `insns=<int>`, `lpc=0x<hex>`, and `ec[0x<hex>]=0x<hex>` for every EC register touched or queried. Exit code 0 when it ran (even if the driver misbehaved), non-zero only on a host error (bad args, PE load failure).
  Also `uc_host selftest` — runs the inc-ecx program and prints `selftest=ok` / exits non-zero if Unicorn cannot execute (the CET premise check).

- [ ] **Step 1: Write `uc_host.c`.** Port the `Ec` class (register file, `inb`/`outb` state machine for cmd 0x80/0x81) and the `Dxe` class (map PE sections at `0x10000000`, stack, fake system table, `IN`/`OUT` hooks for 0x62/0x66 and 0xCF8/0xCFC, instruction counter, `run` with an instruction limit) from `emulate_modules.py` into C against the Unicorn C API (`uc_open`, `uc_mem_map`, `uc_mem_write`, `uc_reg_write/read`, `uc_hook_add` for `UC_HOOK_INSN` IN/OUT, `uc_emu_start`). `ecfan` sets the initial EC regs (default `0x7E=0x00`, `0x80=0x03`), runs, prints the result lines.
- [ ] **Step 2: Write `native/build.sh` / `native/build.cmd`.** Each: obtain the Unicorn native lib for the platform (Windows: build `unicorn.dll` from the Unicorn 2.1.4 source with MSVC Release, x86-only, per `native/README.md`; Linux: copy `libunicorn.so` from the restored `UnicornEngine.Unicorn` NuGet package), compile `uc_host.c` against Unicorn's headers + import lib, and place `uc_host[.exe]` + the Unicorn lib into `native/out/<rid>/`. The Windows compile must NOT pass `/CETCOMPAT`.
- [ ] **Step 3: Build locally and self-test.**
  Run: `native/build.cmd` (Windows), then `native/out/win-x64/uc_host.exe selftest`
  Expected: `selftest=ok`, exit 0 (proves Unicorn executes in the plain native host).
- [ ] **Step 4: Run the host on the real EC-fan driver.** Extract the `HpEcFanInit` PE (the PE32 section of `EcFanDriver.Build()`), write it to a temp file, and run `uc_host ecfan --pe <file>`.
  Expected: `status=0`, `ec[0x7e]=0xe7`, `ec[0x80]=0x01`, `lpc=0x40000000`, and a modest `insns`. With `--no-ec`: `status=0` and a bounded `insns` (it timed out and returned, did not hang). Compare against `G:\HPBIOS\build\T5_emulation.txt`'s DXE section.
- [ ] **Step 5: Commit** (source + scripts only; `native/out/` is git-ignored): `feat(emu): native uc_host EC emulator and build scripts`.

### Task 2: `Am4ever.Emu` — EcEmulator .NET wrapper

**Files:**
- Create: `src/Am4ever.Emu/Am4ever.Emu.csproj`, `src/Am4ever.Emu/EcEmulator.cs`, `src/Am4ever.Emu/EmulatorException.cs`
- Modify: `am4ever.sln` (add the project); `tests/Am4ever.Tests/Am4ever.Tests.csproj` (reference it)
- Create: `tests/Am4ever.Tests/Emu/EmuFactAttribute.cs`, `tests/Am4ever.Tests/Emu/HostLocatorTests.cs`

**Interfaces:**
- Consumes: the `uc_host` binary from Task 1 (located via `native/out/<rid>/` relative to the repo, or an `AM4EVER_UC_HOST` env override).
- Produces:
  - `public sealed record EcResult(int Status, long Instructions, uint Lpc, IReadOnlyDictionary<int,byte> Ec)`.
  - `public static class EcEmulator` — `static string? HostPath { get; }` (the resolved `uc_host` path or null); `static EcResult RunEcFan(byte[] pe, bool ecPresent = true, IReadOnlyDictionary<int,byte>? initialRegs = null)` — writes `pe` to a temp file, runs the host, parses stdout, deletes the temp; throws `EmulatorException` on a non-zero host exit or unparseable output (message includes exit code + stderr).
  - `public sealed class EmulatorException : Exception`.
  - Test side: `[EmuFact]` — a `FactAttribute` whose `Skip` is set when `EcEmulator.HostPath` is null, with a message pointing at `native/build`.

- [ ] **Step 1: Write the failing tests**

```csharp
// SPDX-License-Identifier: GPL-2.0-or-later
public class HostLocatorTests
{
    [Fact] public void HostPath_WhenUnset_IsNullNotThrow()
    {
        // With no built host and no env override, HostPath is null and RunEcFan is skippable, never throwing on resolve.
        _ = EcEmulator.HostPath;   // must not throw
    }

    [EmuFact] public void Run_HostFailure_ThrowsClean() =>
        Assert.Throws<EmulatorException>(() => EcEmulator.RunEcFan(new byte[] { 1, 2, 3 }));  // not a valid PE
}
```

- [ ] **Step 2:** Run `dotnet test --filter HostLocatorTests` → FAIL (types missing).
- [ ] **Step 3:** Implement the project and `EcEmulator`. `HostPath`: check `AM4EVER_UC_HOST`, then `native/out/<rid>/uc_host[.exe]` walking up to the repo root; return null if absent. `RunEcFan`: `Process.Start` with redirected stdout/stderr, 30 s cap, parse `key=value` lines.
- [ ] **Step 4:** Run with the host built (reference lines parse) and without (`HostPath` null, `[EmuFact]` skips). Both green.
- [ ] **Step 5:** Commit: `feat(emu): EcEmulator out-of-process wrapper`.

### Task 3: EC-fan hand-off verification

**Files:**
- Create: `tests/Am4ever.Tests/Emu/EcFanVerificationTests.cs`

**Interfaces:**
- Consumes: `EcFanDriver` (M4), `EcEmulator` (Task 2), `FirmwareVolume`/`FfsFile` (M2) to extract the PE.

- [ ] **Step 1: Write the failing tests**

```csharp
// SPDX-License-Identifier: GPL-2.0-or-later
public class EcFanVerificationTests
{
    static byte[] DriverPe()
    {
        var buf = SyntheticFv.WrapFile(EcFanDriver.Build(), out _);
        var file = new FirmwareVolume(buf, 0).FindByGuid(EcFanDriver.FileGuid);
        var (s, e) = file.ImageSection();          // the PE32 section body
        return buf[s..e];
    }

    [EmuFact] public void HpEcFanInit_PerformsEcHandoff()
    {
        var r = EcEmulator.RunEcFan(DriverPe());
        Assert.Equal(0, r.Status);
        Assert.Equal(0xE7, r.Ec[0x7E]);
        Assert.Equal(0x01, r.Ec[0x80]);            // bit 1 cleared from 0x03
        Assert.True((r.Lpc & 0x40000000) != 0);    // D14F3x44 bit 30 set
    }

    [EmuFact] public void EcAbsent_ReturnsBoundedTimeout()
    {
        var r = EcEmulator.RunEcFan(DriverPe(), ecPresent: false);
        Assert.Equal(0, r.Status);                 // returned, did not hang
        Assert.True(r.Instructions > 0);
    }
}
```

- [ ] **Step 2:** Run → with the host built, these must pass; without it, skip.
- [ ] **Step 3:** Run the whole suite `dotnet test` with and without the host built. Both green (emulator tests run / skip).
- [ ] **Step 4:** Commit: `test(emu): HP EC-fan hand-off verification in the emulator`.
- [ ] **Step 5:** Hand over: report the counts for both modes; note that the native host must be built with `native/build` and is not committed; and that the CI job to build the host for both platforms, and the PEI CPM harness, are the next steps.
```

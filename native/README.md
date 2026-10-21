# native/uc_host — out-of-process emulator host

`uc_host` runs am4ever's generated DXE drivers under the [Unicorn](https://www.unicorn-engine.org/)
CPU emulator against a simulated ACPI EC, and reports what they did. The .NET side
(`Am4ever.Emu`) invokes it as a subprocess.

## Why a separate native process

Unicorn's TCG uses `longjmp`, which is incompatible with the user-mode **CET shadow stack**
that a .NET process has enabled on modern Windows — executing emulated code there fail-fasts
(`0xC0000409`). A plain native exe (not marked `/CETCOMPAT`) has no shadow stack, so Unicorn
runs, exactly as it does under `python.exe`. Running it out-of-process also gives one code path
for Windows and Linux.

## Binaries are not committed

`uc_host`, `unicorn.dll` and `libunicorn.so` are larger than the repo's file-size limit and are
**never committed**. Build them into the git-ignored `native/out/<rid>/`. CI builds them into the
release artifacts; `Am4ever.Emu` finds them there or via the `AM4EVER_UC_HOST` environment variable,
and the emulator tests skip when the host is absent.

## Building Unicorn (once)

Build Unicorn 2.1.x as a shared library with the **same** toolchain family as the host:

- **Windows (MSVC Release, x86-only):**
  ```
  git clone --branch 2.1.4 --depth 1 https://github.com/unicorn-engine/unicorn
  cmake -S unicorn -B unicorn/build -G "Visual Studio 17 2022" -A x64 -DUNICORN_ARCH=x86 -DUNICORN_BUILD_TESTS=OFF
  cmake --build unicorn/build --config Release
  ```
  gives `unicorn/build/Release/unicorn.dll` + `unicorn-import.lib`, headers in `unicorn/include`.
  (A symlink post-step may fail without Developer Mode — the DLL is already built, ignore it.)
- **Linux:** build from source, or reuse `libunicorn.so` from the restored `UnicornEngine.Unicorn`
  NuGet package (`runtimes/linux-x64/native`), with headers from the Unicorn source `include/`.

## Building the host

```
# Windows
set UNICORN_INCLUDE=...\unicorn\include
set UNICORN_BIN=...\unicorn\build\Release
native\build.cmd

# Linux
UNICORN_INCLUDE=.../unicorn/include UNICORN_BIN=.../lib native/build.sh
```

Then `native/out/<rid>/uc_host selftest` must print `selftest=ok`.

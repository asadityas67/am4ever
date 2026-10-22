# Generic engine: remove the HP/ASRock hardcodes

## Intent & why
am4ever's architecture is data-driven (profiles + donor adapters), but three spots still bake in the
HP 285 G3 / ASRock reference port, which makes the app feel HP-only. This removes those three hardcodes
from the shared engine so a new OEM board (against a supported donor) works without touching code —
the groundwork for real genericity. Authoring a brand-new *donor adapter* (item 4) is explicitly later.

**Hard constraint:** the HP 285 G3 reference build stays **byte-for-byte identical** — `Hp285G3ReferenceTests`
(decompressed PEI + DXE equal to the T5 image) must keep passing. Every change below is designed to be
output-neutral for the HP case.

## Scope (the three items)
1. **Board Lab load offsets from data, not constants.** Board Lab currently calls
   `BoardAnalysis.Load(bytes, 0xC00000, 0xE02000)` — HP/ASRock offsets. Replace with auto-detection for
   the OEM dump (scan for the PEI firmware-volume signature, pick the FV that holds a CPM OEM PEIM) and
   the chosen **adapter's** volume offsets for the donor.
2. **Verb-table subsystem-ID check optional.** `VerbTable.ExtractHp` hard-validates HP's subsystem ID
   (`0x34,0x84,0x3C,0x10`). Make that validation optional (skipped unless an expected SSID is supplied).
   The returned verbs are unchanged, so the HP build output is identical.
3. **Generic identity, no per-board extractor gate.** The build's SMBIOS identity base is
   `DmiIdentity.HpDefault` (hard HP strings), and the composer gates `smbios_identity` on a known Core
   extractor. Resolve the base identity from the profile instead: a known built-in extractor
   (`hp-8434`) still yields the exact HP identity (so the reference bytes are unchanged), and **any other
   board derives its identity generically from the profile's own board metadata** (vendor / model /
   board-id). `--dmi` continues to override. The composer no longer gates `smbios_identity`.

## Goals
- No hardcoded ROM offsets in Board Lab; it works on a dump whose PEI volume is anywhere, given the
  donor adapter for the donor side.
- `verb_table` discovery/build works for a non-HP audio subsystem (the SSID check no longer rejects it).
- `smbios_identity` works for any board the composer can author, carrying that board's identity (from
  its profile metadata, or `--dmi`), with no code change required per board.
- The HP 285 G3 reference build is unchanged (same image bytes).

## Non-goals / anti-goals
- No new donor-adapter authoring (item 4, later).
- Do **not** change the HP reference output. A change that alters the T5 bytes is a failure, even if it
  "works" for a new board.
- No change to the profile schema's required fields; `identity.extractor` stays an optional hint.
- Keep it simple; no speculative generality beyond these three.

## How it's built (decisions made)
1. **OEM PEI auto-detect.** Add `BoardAnalysis.LoadOem(byte[] rom)` that scans for the FV signature
   (`0x4856465F` at FV-header `+0x28`) and selects the FV whose files include an `AmdCpmOem*` PEIM;
   `BoardAnalysis.LoadDonor(byte[] rom, DonorAdapter adapter)` uses `adapter.Volumes`. Board Lab's
   `Open` uses these (donor load needs the adapter chosen first). The existing `Load(rom, pei, boot)`
   stays for callers that know offsets.
2. **Optional SSID.** `VerbTable.Extract(rom, fvOffset, codecId, romBase, uint[]? expectedSsid = null)`
   does the check only when `expectedSsid` is non-null; `ExtractHp` becomes a thin wrapper that passes
   the HP SSID (so existing callers/tests are unchanged), and `BuildPipeline` calls `Extract` with
   `expectedSsid: null`. The verbs returned are identical either way.
3. **Identity resolver.** Add `DmiIdentity.FromBoard(string vendor, string model, string boardId)`
   (System/Board/Chassis manufacturer = vendor, system product = model, board product = board-id —
   the generic analogue of `HpDefault`). Add `IdentityResolver.BaseFor(BoardProfile p)`:
   a known built-in extractor name → its fixed identity (`hp-8434` → `HpDefault`), else
   `FromBoard(p.Board.*)`. `BuildPipeline.ApplyDxe` uses `IdentityResolver.BaseFor(profile).Merge(dmi?)`
   instead of `HpDefault`. `ComposerCore.IdentitySupported` becomes always-true (the generic path always
   exists); `ProfileDiscovery`'s `smbios_identity` note drops the "needs a Core extractor" wording.

## What's left to the builder
- The exact FV-scan heuristic (how to enumerate candidate FVs, how strictly to match `AmdCpmOem*`) —
  any approach that finds the HP PEI FV and ignores others.
- Where `IdentityResolver` lives (Core, beside `DmiIdentity`) and the known-extractor table (today just
  `hp-8434`).
- Board Lab UX when no adapter is chosen before loading a donor (disable, or prompt).

## Testing approach (TDD)
- **#1:** `[RomFact]` — `BoardAnalysis.LoadOem(hpDump)` finds the PEI FV and lists the CPM OEM PEIMs
  (same modules as the hardcoded-offset load). A synthetic/offset guard if feasible without ROMs.
- **#2:** `VerbTable.Extract` with a non-HP SSID in the data does **not** throw when `expectedSsid` is
  null, and still returns the verbs; the HP wrapper path still validates. Reuse the synthetic verb fixtures.
- **#3:** `IdentityResolver.BaseFor` returns `HpDefault` for the HP profile (assert a couple of fields)
  and a vendor/model/board-id-derived identity for a non-HP draft; a plain xUnit test. **Plus** the
  existing `Hp285G3ReferenceTests` must still pass (`[RomFact]`/`[T5Fact]`) — the HP output is unchanged.

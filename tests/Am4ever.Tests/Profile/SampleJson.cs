// SPDX-License-Identifier: GPL-2.0-or-later
using System.Text;

namespace Am4ever.Tests.Profile;

/// <summary>Small inline JSON fixtures for profile/adapter loading tests (no firmware bytes).</summary>
public static class SampleJson
{
    public static byte[] MinimalHpProfile => Profile("am4ever.profile/1",
        "{ \"op\": \"port_gpio\", \"family\": \"MTS_VMR\", \"from\": \"oem:pinnacle\", \"merge\": \"family_delta\" }");

    public static byte[] WithSchema(string schema) => Profile(schema, "");

    public static byte[] WithBytePatch(int expectedHexBytes) => Profile("am4ever.profile/1",
        $"{{ \"op\": \"byte_patch\", \"offset\": 16, \"expected\": \"{new string('a', expectedHexBytes * 2)}\" }}");

    static byte[] Profile(string schema, string op) => Encoding.UTF8.GetBytes($$"""
    {
      "schema": "{{schema}}",
      "board": { "vendor": "HP", "model": "HP 285 G3 MT", "board_id": "8434",
                 "match": { "ssid": "8434", "bios_versions": ["F.65"], "rom_size": 16777216 } },
      "donor": { "adapter": "asrock-ab350m-pro4-p10.43", "sha256": "deadbeef" },
      "authors": ["am4ever"],
      "status": { "tested": [], "untested": [] },
      "operations": [ {{op}} ],
      "identity": { "extractor": "hp-8434" }
    }
    """);

    public static byte[] AsrockAdapter => Encoding.UTF8.GetBytes("""
    {
      "id": "asrock-ab350m-pro4-p10.43",
      "identity": { "vendor": "ASRock", "board": "AB350M Pro4", "version": "P10.43", "sha256": "0011" },
      "modules": [ { "name": "AmdCpmOemInitPeim", "family": "ZP", "index": 0 } ],
      "overrides": [ { "family": "ZP", "offset": 726, "expected": "81ecc4000000" } ],
      "volumes": { "boot_blob_offset": 14688256, "boot_blob_length": 873926, "boot_fv_offset": 32, "dxe_fv_offset": 9760768 },
      "hazards": [],
      "download_url": "https://www.asrock.com/"
    }
    """);
}

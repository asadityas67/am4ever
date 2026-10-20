// SPDX-License-Identifier: GPL-2.0-or-later
using System.Text.Json;

namespace Am4ever.Core.Board.Smbios;

/// <summary>
/// The SMBIOS identity fields am4ever can set. Any field left null is cleared when its structure is
/// rewritten (the generic-identity case); <see cref="FromJson"/> reads the snake_case keys produced
/// by the <c>save_hp_dmi.ps1</c> helper, so a machine-data file loads unchanged.
/// </summary>
public sealed record DmiIdentity
{
    public string? SystemManufacturer { get; init; }
    public string? SystemProduct { get; init; }
    public string? SystemVersion { get; init; }
    public string? SystemSerial { get; init; }
    public string? SystemSku { get; init; }
    public string? SystemFamily { get; init; }
    public string? SystemUuid { get; init; }
    public string? BoardManufacturer { get; init; }
    public string? BoardProduct { get; init; }
    public string? BoardVersion { get; init; }
    public string? BoardSerial { get; init; }
    public string? BoardAssetTag { get; init; }
    public string? BoardLocation { get; init; }
    public string? ChassisManufacturer { get; init; }
    public string? ChassisVersion { get; init; }
    public string? ChassisSerial { get; init; }
    public string? ChassisAssetTag { get; init; }
    public string? ChassisSku { get; init; }
    public int? ChassisType { get; init; }
    public IReadOnlyList<string>? OemStrings { get; init; }

    /// <summary>The generic HP 285 G3 identity used when the user gives no machine data.</summary>
    public static DmiIdentity HpDefault => new()
    {
        SystemManufacturer = "HP",
        SystemProduct = "HP 285 G3 MT Business PC",
        BoardManufacturer = "HP",
        BoardProduct = "8434",
        ChassisManufacturer = "HP",
    };

    /// <summary>This identity with any non-empty field of <paramref name="overrides"/> taking precedence.</summary>
    public DmiIdentity Merge(DmiIdentity overrides) => new()
    {
        SystemManufacturer = Pick(overrides.SystemManufacturer, SystemManufacturer),
        SystemProduct = Pick(overrides.SystemProduct, SystemProduct),
        SystemVersion = Pick(overrides.SystemVersion, SystemVersion),
        SystemSerial = Pick(overrides.SystemSerial, SystemSerial),
        SystemSku = Pick(overrides.SystemSku, SystemSku),
        SystemFamily = Pick(overrides.SystemFamily, SystemFamily),
        SystemUuid = Pick(overrides.SystemUuid, SystemUuid),
        BoardManufacturer = Pick(overrides.BoardManufacturer, BoardManufacturer),
        BoardProduct = Pick(overrides.BoardProduct, BoardProduct),
        BoardVersion = Pick(overrides.BoardVersion, BoardVersion),
        BoardSerial = Pick(overrides.BoardSerial, BoardSerial),
        BoardAssetTag = Pick(overrides.BoardAssetTag, BoardAssetTag),
        BoardLocation = Pick(overrides.BoardLocation, BoardLocation),
        ChassisManufacturer = Pick(overrides.ChassisManufacturer, ChassisManufacturer),
        ChassisVersion = Pick(overrides.ChassisVersion, ChassisVersion),
        ChassisSerial = Pick(overrides.ChassisSerial, ChassisSerial),
        ChassisAssetTag = Pick(overrides.ChassisAssetTag, ChassisAssetTag),
        ChassisSku = Pick(overrides.ChassisSku, ChassisSku),
        ChassisType = overrides.ChassisType ?? ChassisType,
        OemStrings = overrides.OemStrings is { Count: > 0 } ? overrides.OemStrings : OemStrings,
    };

    static string? Pick(string? over, string? fallback) => string.IsNullOrWhiteSpace(over) ? fallback : over.Trim();

    public static DmiIdentity FromJson(byte[] utf8)
    {
        var dto = JsonSerializer.Deserialize<Dto>(utf8, Options)
                  ?? throw new ArgumentException("empty DMI JSON");
        return new DmiIdentity
        {
            SystemManufacturer = dto.SystemManufacturer,
            SystemProduct = dto.SystemProduct,
            SystemVersion = dto.SystemVersion,
            SystemSerial = dto.SystemSerial,
            SystemSku = dto.SystemSku,
            SystemFamily = dto.SystemFamily,
            SystemUuid = dto.SystemUuid,
            BoardManufacturer = dto.BoardManufacturer,
            BoardProduct = dto.BoardProduct,
            BoardVersion = dto.BoardVersion,
            BoardSerial = dto.BoardSerial,
            BoardAssetTag = dto.BoardAssetTag,
            BoardLocation = dto.BoardLocation,
            ChassisManufacturer = dto.ChassisManufacturer,
            ChassisVersion = dto.ChassisVersion,
            ChassisSerial = dto.ChassisSerial,
            ChassisAssetTag = dto.ChassisAssetTag,
            ChassisSku = dto.ChassisSku,
            ChassisType = dto.ChassisTypes is { Length: > 0 } ? dto.ChassisTypes[0] : null,
            OemStrings = dto.OemStrings,
        };
    }

    static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    sealed class Dto
    {
        public string? SystemManufacturer { get; set; }
        public string? SystemProduct { get; set; }
        public string? SystemVersion { get; set; }
        public string? SystemSerial { get; set; }
        public string? SystemSku { get; set; }
        public string? SystemFamily { get; set; }
        public string? SystemUuid { get; set; }
        public string? BoardManufacturer { get; set; }
        public string? BoardProduct { get; set; }
        public string? BoardVersion { get; set; }
        public string? BoardSerial { get; set; }
        public string? BoardAssetTag { get; set; }
        public string? BoardLocation { get; set; }
        public string? ChassisManufacturer { get; set; }
        public string? ChassisVersion { get; set; }
        public string? ChassisSerial { get; set; }
        public string? ChassisAssetTag { get; set; }
        public string? ChassisSku { get; set; }
        public int[]? ChassisTypes { get; set; }
        public string[]? OemStrings { get; set; }
    }
}

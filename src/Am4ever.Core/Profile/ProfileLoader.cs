// SPDX-License-Identifier: GPL-2.0-or-later
using System.Text.Json;
using System.Text.Json.Serialization;
using Am4ever.Core.Rom;

namespace Am4ever.Core.Profile;

/// <summary>Loads and validates board profiles and donor adapters from JSON.</summary>
public static class ProfileLoader
{
    public const string ProfileSchema = "am4ever.profile/1";
    public const int MaxPatchBytes = 64;

    static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new HexBytesConverter() },
    };

    public static BoardProfile LoadProfile(byte[] utf8)
    {
        var profile = Deserialize<BoardProfile>(utf8);
        if (profile.Schema != ProfileSchema)
            throw new RomFormatException($"unsupported profile schema '{profile.Schema}', expected '{ProfileSchema}'");
        foreach (var op in profile.Operations)
        {
            CheckPatchSize(op.Expected, "expected", op.Op);
            CheckPatchSize(op.New, "new", op.Op);
        }
        return profile;
    }

    public static DonorAdapter LoadAdapter(byte[] utf8)
    {
        var adapter = Deserialize<DonorAdapter>(utf8);
        foreach (var o in adapter.Overrides)
            CheckPatchSize(o.Expected, "expected", $"override:{o.Family}");
        return adapter;
    }

    static T Deserialize<T>(byte[] utf8)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(utf8, Options)
                   ?? throw new RomFormatException("empty JSON document");
        }
        catch (JsonException e)
        {
            throw new RomFormatException($"invalid {typeof(T).Name} JSON: {e.Message}");
        }
    }

    static void CheckPatchSize(byte[]? bytes, string field, string op)
    {
        if (bytes is not null && bytes.Length > MaxPatchBytes)
            throw new RomFormatException(
                $"operation '{op}' {field} byte string is {bytes.Length} bytes; the limit is {MaxPatchBytes} (profiles carry no firmware)");
    }
}

// SPDX-License-Identifier: GPL-2.0-or-later
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Am4ever.Core.Profile;

/// <summary>
/// Serialises a <see cref="BoardProfile"/> back to the <c>am4ever.profile/1</c> JSON that
/// <see cref="ProfileLoader"/> reads: the same snake_case naming and hex byte-string converter, run in
/// reverse. The output always parses back through <see cref="ProfileLoader.LoadProfile"/>, so the
/// composer's live preview is a loadable profile. Nulls are skipped for a clean file.
/// </summary>
public static class ProfileWriter
{
    static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new HexBytesConverter() },
    };

    public static byte[] Write(BoardProfile profile) => JsonSerializer.SerializeToUtf8Bytes(profile, Options);
}

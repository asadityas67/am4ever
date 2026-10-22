// SPDX-License-Identifier: GPL-2.0-or-later
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Am4ever.Core.Profile;

/// <summary>
/// Serialises a <see cref="DonorAdapter"/> back to the JSON that <see cref="ProfileLoader.LoadAdapter"/>
/// reads (snake_case, the shared hex byte-string converter), so the adapter-authoring preview is always
/// a loadable adapter. Mirrors <see cref="ProfileWriter"/>.
/// </summary>
public static class AdapterWriter
{
    static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new HexBytesConverter() },
    };

    public static byte[] Write(DonorAdapter adapter) => JsonSerializer.SerializeToUtf8Bytes(adapter, Options);
}

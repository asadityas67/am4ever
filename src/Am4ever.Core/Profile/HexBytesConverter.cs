// SPDX-License-Identifier: GPL-2.0-or-later
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Am4ever.Core.Profile;

/// <summary>Reads a <c>byte[]</c> from a hex string and writes it back as uppercase hex. Shared by the
/// profile reader (<see cref="ProfileLoader"/>) and writer (<see cref="ProfileWriter"/>) so a profile
/// round-trips. The 64-byte cap is enforced after load in <see cref="ProfileLoader"/>, not here.</summary>
internal sealed class HexBytesConverter : JsonConverter<byte[]>
{
    public override byte[]? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var s = reader.GetString();
        return s is null ? null : Convert.FromHexString(s);
    }

    public override void Write(Utf8JsonWriter writer, byte[] value, JsonSerializerOptions options) =>
        writer.WriteStringValue(Convert.ToHexString(value));
}

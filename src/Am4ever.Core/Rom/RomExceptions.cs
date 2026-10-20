// SPDX-License-Identifier: GPL-2.0-or-later
namespace Am4ever.Core.Rom;

/// <summary>The bytes are not a valid AM4 ROM, or a structure inside one is malformed.</summary>
public class RomFormatException : Exception
{
    public RomFormatException(string message) : base(message) { }
}

/// <summary>An edit could not be applied (would not fit, preconditions unmet). The image is left unchanged.</summary>
public class RomEditException : Exception
{
    public RomEditException(string message) : base(message) { }
}

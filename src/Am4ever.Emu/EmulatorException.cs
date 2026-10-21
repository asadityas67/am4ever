// SPDX-License-Identifier: GPL-2.0-or-later
namespace Am4ever.Emu;

/// <summary>The native emulator host failed to run or returned something unparseable.</summary>
public sealed class EmulatorException : Exception
{
    public EmulatorException(string message) : base(message) { }
}

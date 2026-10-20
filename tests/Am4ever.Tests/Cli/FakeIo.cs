// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.Cli;

namespace Am4ever.Tests.Cli;

/// <summary>In-memory console for CLI tests.</summary>
public sealed class FakeIo : IConsoleIo
{
    readonly StringWriter _out = new();
    readonly StringWriter _err = new();

    FakeIo(TextReader input, bool stdinIsTerminal, bool color)
    {
        In = input;
        StdinIsTerminal = stdinIsTerminal;
        SupportsColor = color;
    }

    public TextWriter Out => _out;
    public TextWriter Err => _err;
    public TextReader In { get; }
    public bool StdinIsTerminal { get; }
    public bool SupportsColor { get; }

    public string OutText => _out.ToString();
    public string ErrText => _err.ToString();

    /// <summary>When stdin is not a terminal, any read throws: the CLI must never wait on it.</summary>
    public static FakeIo Create(bool color = false, bool stdinIsTerminal = false, string stdin = "") =>
        new(stdinIsTerminal ? new StringReader(stdin) : new ThrowingReader(), stdinIsTerminal, color);

    sealed class ThrowingReader : TextReader
    {
        public override int Peek() => throw new InvalidOperationException("stdin read while not a terminal");
        public override int Read() => throw new InvalidOperationException("stdin read while not a terminal");
        public override string? ReadLine() => throw new InvalidOperationException("stdin read while not a terminal");
    }
}

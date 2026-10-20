// SPDX-License-Identifier: GPL-2.0-or-later
namespace Am4ever.Cli;

/// <summary>The console the CLI talks to; faked in tests.</summary>
public interface IConsoleIo
{
    TextWriter Out { get; }
    TextWriter Err { get; }
    TextReader In { get; }
    bool StdinIsTerminal { get; }
    bool SupportsColor { get; }
}

/// <summary>The real process console.</summary>
public sealed class SystemConsoleIo : IConsoleIo
{
    public TextWriter Out => Console.Out;
    public TextWriter Err => Console.Error;
    public TextReader In => Console.In;
    public bool StdinIsTerminal => !Console.IsInputRedirected;
    public bool SupportsColor =>
        !Console.IsErrorRedirected && Environment.GetEnvironmentVariable("NO_COLOR") is null;
}

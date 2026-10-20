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
    const int StdErrorHandle = -12;
    const uint EnableVirtualTerminalProcessing = 0x0004;

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr GetStdHandle(int handle);

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    static extern bool GetConsoleMode(IntPtr handle, out uint mode);

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    static extern bool SetConsoleMode(IntPtr handle, uint mode);

    static readonly Lazy<bool> VirtualTerminal = new(() =>
    {
        if (!OperatingSystem.IsWindows())
            return true;
        try
        {
            var h = GetStdHandle(StdErrorHandle);
            return GetConsoleMode(h, out var mode)
                && ((mode & EnableVirtualTerminalProcessing) != 0
                    || SetConsoleMode(h, mode | EnableVirtualTerminalProcessing));
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
        {
            return false;
        }
    });

    public TextWriter Out => Console.Out;
    public TextWriter Err => Console.Error;
    public TextReader In => Console.In;
    public bool StdinIsTerminal => !Console.IsInputRedirected;
    public bool SupportsColor =>
        DecideColor(Console.IsErrorRedirected, Environment.GetEnvironmentVariable("NO_COLOR") is not null,
            !Console.IsErrorRedirected && VirtualTerminal.Value);

    /// <summary>Colour only on a real terminal that understands ANSI escapes, and never with NO_COLOR.</summary>
    public static bool DecideColor(bool errorRedirected, bool noColor, bool virtualTerminal) =>
        !errorRedirected && !noColor && virtualTerminal;
}

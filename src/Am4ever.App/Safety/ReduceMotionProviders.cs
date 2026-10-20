// SPDX-License-Identifier: GPL-2.0-or-later
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Am4ever.App.Safety;

/// <summary>Windows: "Show animations in Windows" off means reduce motion.</summary>
public sealed class WindowsReduceMotionProvider : IReduceMotionProvider
{
    const uint SPI_GETCLIENTAREAANIMATION = 0x1042;

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool SystemParametersInfo(uint action, uint param, out int value, uint winIni);

    public bool ReduceMotion
    {
        get
        {
            try
            {
                return SystemParametersInfo(SPI_GETCLIENTAREAANIMATION, 0, out var animations, 0) && animations == 0;
            }
            catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
            {
                return false;
            }
        }
    }
}

/// <summary>Linux (GNOME and compatible): <c>enable-animations false</c> means reduce motion.
/// Missing gsettings, timeouts and odd output all mean "no preference".</summary>
public sealed class LinuxReduceMotionProvider(string gsettingsPath = "gsettings") : IReduceMotionProvider
{
    static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    public bool ReduceMotion
    {
        get
        {
            try
            {
                var psi = new ProcessStartInfo(gsettingsPath, "get org.gnome.desktop.interface enable-animations")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                };
                using var p = Process.Start(psi);
                if (p is null) return false;
                if (!p.WaitForExit(Timeout))
                {
                    try { p.Kill(); } catch (InvalidOperationException) { }
                    return false;
                }
                return p.ExitCode == 0 && p.StandardOutput.ReadToEnd().Trim() == "false";
            }
            catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
            {
                return false;
            }
        }
    }
}

/// <summary><c>AM4EVER_REDUCE_MOTION=1</c> forces reduce motion; otherwise defers to the platform.</summary>
public sealed class EnvReduceMotionProvider(IReduceMotionProvider platform, Func<string, string?>? env = null) : IReduceMotionProvider
{
    readonly Func<string, string?> _env = env ?? Environment.GetEnvironmentVariable;

    public bool ReduceMotion => _env("AM4EVER_REDUCE_MOTION") == "1" || platform.ReduceMotion;

    /// <summary>The provider for the OS this process runs on.</summary>
    public static IReduceMotionProvider ForCurrentPlatform() =>
        new EnvReduceMotionProvider(OperatingSystem.IsWindows()
            ? new WindowsReduceMotionProvider()
            : new LinuxReduceMotionProvider());
}

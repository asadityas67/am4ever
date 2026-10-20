// SPDX-License-Identifier: GPL-2.0-or-later
namespace Am4ever.App.Safety;

/// <summary>Whether the user asked the OS to reduce motion (no blinking/animation).</summary>
public interface IReduceMotionProvider
{
    bool ReduceMotion { get; }
}

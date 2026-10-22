// SPDX-License-Identifier: GPL-2.0-or-later
namespace Am4ever.App.Feedback;

/// <summary>One toast notification: a short message and its kind, with the glyph that leads it.</summary>
public sealed record Toast(string Message, ToastKind Kind)
{
    public string Glyph => Kind switch { ToastKind.Success => "✓", ToastKind.Error => "✕", _ => "ℹ" };
}

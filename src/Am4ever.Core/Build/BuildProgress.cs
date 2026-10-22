// SPDX-License-Identifier: GPL-2.0-or-later
namespace Am4ever.Core.Build;

/// <summary>
/// A progress report from <see cref="BuildPipeline.Build"/>: <paramref name="Done"/> of
/// <paramref name="Total"/> build phases are complete, with a short human-readable label for the
/// phase that just finished. Reports are monotonic and the final one has <c>Done == Total</c>.
/// </summary>
public sealed record BuildProgress(int Done, int Total, string Phase);

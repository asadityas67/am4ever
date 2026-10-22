// SPDX-License-Identifier: GPL-2.0-or-later
namespace Am4ever.Tests;

/// <summary>
/// A synchronous <see cref="IProgress{T}"/>: <see cref="Report"/> runs the callback inline, so every
/// report is recorded by the time the reporting call returns. Unlike <see cref="Progress{T}"/>, it
/// never marshals to a synchronization context or the thread pool, which keeps progress tests
/// deterministic.
/// </summary>
public sealed class SyncProgress<T> : IProgress<T>
{
    readonly Action<T> _onReport;
    public SyncProgress(Action<T> onReport) => _onReport = onReport;
    public void Report(T value) => _onReport(value);
}

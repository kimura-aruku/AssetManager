using AssetManager.Application.History;

namespace AssetManager.UnitTests.Testing;

internal sealed class TestUndoHistoryPersistence : IUndoHistoryPersistence
{
    public Guid SessionId { get; } = Guid.CreateVersion7();

    public SortedDictionary<int, UndoableDataChange> Entries { get; } = [];

    public int Cursor { get; private set; }

    public bool SessionDeleted { get; private set; }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task SaveEntryAsync(
        int index,
        UndoableDataChange change,
        CancellationToken cancellationToken = default)
    {
        Entries[index] = change;
        return Task.CompletedTask;
    }

    public Task DeleteEntriesFromAsync(
        int startIndex,
        CancellationToken cancellationToken = default)
    {
        foreach (var index in Entries.Keys.Where(index => index >= startIndex).ToArray())
        {
            _ = Entries.Remove(index);
        }

        return Task.CompletedTask;
    }

    public Task SaveCursorAsync(int cursor, CancellationToken cancellationToken = default)
    {
        Cursor = cursor;
        return Task.CompletedTask;
    }

    public Task DeleteSessionAsync(CancellationToken cancellationToken = default)
    {
        SessionDeleted = true;
        Entries.Clear();
        Cursor = 0;
        return Task.CompletedTask;
    }
}

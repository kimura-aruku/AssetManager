using AssetManager.Application.History;
using AssetManager.Infrastructure.Persistence;
using AssetManager.Infrastructure.Persistence.Json;
using AssetManager.Infrastructure.Persistence.Repositories;

namespace AssetManager.Infrastructure.History;

public sealed class FileUndoHistoryPersistence : IUndoHistoryPersistence
{
    private readonly AtomicJsonFileStore _store;
    private readonly string _sessionDirectory;

    public FileUndoHistoryPersistence(
        DataRootLayout layout,
        AtomicJsonFileStore? store = null,
        Guid? sessionId = null)
    {
        ArgumentNullException.ThrowIfNull(layout);
        _store = store ?? new AtomicJsonFileStore();
        SessionId = sessionId ?? Guid.CreateVersion7();
        _sessionDirectory = Path.Combine(layout.UndoDirectory, SessionId.ToString("D"));
    }

    public Guid SessionId { get; }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = Directory.CreateDirectory(_sessionDirectory);
        return SaveCursorAsync(0, cancellationToken);
    }

    public async Task SaveEntryAsync(
        int index,
        UndoableDataChange change,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentNullException.ThrowIfNull(change);
        var entryDirectory = GetEntryDirectory(index);
        _ = Directory.CreateDirectory(entryDirectory);
        var summary = new HistoryEntryDocument(
            JsonDefaults.CurrentSchemaVersion,
            index,
            change.Description,
            change.RecordChanges.Select(item => new HistoryRecordChangeDocument(
                item.RecordId.ToString(),
                item.Before is not null,
                item.After is not null)).ToArray(),
            change.FieldsBefore is not null);
        await _store.SaveAsync(
            Path.Combine(entryDirectory, "entry.json"),
            summary,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (change.FieldsBefore is not null)
        {
            await SaveFieldsAsync(
                entryDirectory,
                "before",
                change.FieldsBefore,
                cancellationToken).ConfigureAwait(false);
            await SaveFieldsAsync(
                entryDirectory,
                "after",
                change.FieldsAfter!,
                cancellationToken).ConfigureAwait(false);
        }

        foreach (var recordChange in change.RecordChanges)
        {
            if (recordChange.Before is not null)
            {
                await SaveRecordAsync(
                    entryDirectory,
                    "before",
                    recordChange.Before,
                    cancellationToken).ConfigureAwait(false);
            }

            if (recordChange.After is not null)
            {
                await SaveRecordAsync(
                    entryDirectory,
                    "after",
                    recordChange.After,
                    cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public Task DeleteEntriesFromAsync(
        int startIndex,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(startIndex);
        cancellationToken.ThrowIfCancellationRequested();
        if (!Directory.Exists(_sessionDirectory))
        {
            return Task.CompletedTask;
        }

        foreach (var path in Directory.EnumerateDirectories(_sessionDirectory, "entry-*"))
        {
            var name = Path.GetFileName(path);
            if (int.TryParse(name.AsSpan("entry-".Length), out var index) && index >= startIndex)
            {
                Directory.Delete(path, recursive: true);
            }
        }

        return Task.CompletedTask;
    }

    public Task SaveCursorAsync(int cursor, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(cursor);
        _ = Directory.CreateDirectory(_sessionDirectory);
        return _store.SaveAsync(
            Path.Combine(_sessionDirectory, "session.json"),
            new HistorySessionDocument(
                JsonDefaults.CurrentSchemaVersion,
                SessionId.ToString("D"),
                cursor),
            cancellationToken: cancellationToken);
    }

    public Task DeleteSessionAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Directory.Exists(_sessionDirectory))
        {
            Directory.Delete(_sessionDirectory, recursive: true);
        }

        return Task.CompletedTask;
    }

    private Task SaveFieldsAsync(
        string entryDirectory,
        string state,
        IReadOnlyList<Domain.Fields.FieldDefinition> fields,
        CancellationToken cancellationToken)
    {
        var directory = Path.Combine(entryDirectory, state);
        _ = Directory.CreateDirectory(directory);
        return _store.SaveAsync(
            Path.Combine(directory, "fields.json"),
            FieldDefinitionRepository.CreateDocument(fields),
            cancellationToken: cancellationToken);
    }

    private Task SaveRecordAsync(
        string entryDirectory,
        string state,
        Domain.Records.AssetRecord record,
        CancellationToken cancellationToken)
    {
        var directory = Path.Combine(entryDirectory, state, "records");
        _ = Directory.CreateDirectory(directory);
        return _store.SaveAsync(
            Path.Combine(directory, $"{record.Id}.json"),
            RecordRepository.CreateDocument(new PersistedAssetRecord(record)),
            cancellationToken: cancellationToken);
    }

    private string GetEntryDirectory(int index)
    {
        return Path.Combine(_sessionDirectory, $"entry-{index:D8}");
    }

    private sealed record HistorySessionDocument(int SchemaVersion, string SessionId, int Cursor);

    private sealed record HistoryEntryDocument(
        int SchemaVersion,
        int Index,
        string Description,
        IReadOnlyList<HistoryRecordChangeDocument> Records,
        bool IncludesFieldDefinitions);

    private sealed record HistoryRecordChangeDocument(
        string RecordId,
        bool HasBefore,
        bool HasAfter);
}

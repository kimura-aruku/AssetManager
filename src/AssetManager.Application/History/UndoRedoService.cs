using AssetManager.Application.Data;
using AssetManager.Domain.Common;
using AssetManager.Domain.Fields;
using AssetManager.Domain.Records;
using AssetManager.Domain.Validation;

namespace AssetManager.Application.History;

public sealed class UndoRedoService : IAsyncDisposable
{
    private readonly IAssetManagerDataStore _store;
    private readonly IUndoHistoryPersistence _persistence;
    private readonly List<UndoableDataChange> _entries = [];
    private readonly SemaphoreSlim _gate = new(1, 1);
    private int _cursor;
    private bool _disposed;

    public UndoRedoService(
        IAssetManagerDataStore store,
        IUndoHistoryPersistence persistence)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
    }

    public UndoRedoState State => new(
        _cursor > 0,
        _cursor < _entries.Count,
        _cursor > 0 ? _entries[_cursor - 1].Description : null,
        _cursor < _entries.Count ? _entries[_cursor].Description : null);

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return _persistence.InitializeAsync(cancellationToken);
    }

    public async Task ExecuteAsync(
        UndoableDataChange change,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(change);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ValidateAsync(change, useAfter: true, cancellationToken).ConfigureAwait(false);
            await _store.ApplyDataChangeAsync(change, useAfter: true, cancellationToken).ConfigureAwait(false);
            try
            {
                if (_cursor < _entries.Count)
                {
                    await _persistence.DeleteEntriesFromAsync(_cursor, cancellationToken).ConfigureAwait(false);
                    _entries.RemoveRange(_cursor, _entries.Count - _cursor);
                }

                await _persistence.SaveEntryAsync(_cursor, change, cancellationToken).ConfigureAwait(false);
                await _persistence.SaveCursorAsync(_cursor + 1, cancellationToken).ConfigureAwait(false);
                _entries.Add(change);
                _cursor++;
            }
            catch (Exception)
            {
                await _store.ApplyDataChangeAsync(
                    change,
                    useAfter: false,
                    CancellationToken.None).ConfigureAwait(false);
                throw;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> UndoAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_cursor == 0)
            {
                return false;
            }

            var change = _entries[_cursor - 1];
            await ValidateAsync(change, useAfter: false, cancellationToken).ConfigureAwait(false);
            await _store.ApplyDataChangeAsync(change, useAfter: false, cancellationToken).ConfigureAwait(false);
            try
            {
                await _persistence.SaveCursorAsync(_cursor - 1, cancellationToken).ConfigureAwait(false);
                _cursor--;
            }
            catch (Exception)
            {
                await _store.ApplyDataChangeAsync(
                    change,
                    useAfter: true,
                    CancellationToken.None).ConfigureAwait(false);
                throw;
            }

            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> RedoAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_cursor >= _entries.Count)
            {
                return false;
            }

            var change = _entries[_cursor];
            await ValidateAsync(change, useAfter: true, cancellationToken).ConfigureAwait(false);
            await _store.ApplyDataChangeAsync(change, useAfter: true, cancellationToken).ConfigureAwait(false);
            try
            {
                await _persistence.SaveCursorAsync(_cursor + 1, cancellationToken).ConfigureAwait(false);
                _cursor++;
            }
            catch (Exception)
            {
                await _store.ApplyDataChangeAsync(
                    change,
                    useAfter: false,
                    CancellationToken.None).ConfigureAwait(false);
                throw;
            }

            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task CompleteSessionAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _persistence.DeleteSessionAsync(cancellationToken).ConfigureAwait(false);
            _entries.Clear();
            _cursor = 0;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await CompleteSessionAsync().ConfigureAwait(false);
        _gate.Dispose();
        _disposed = true;
    }

    private async Task ValidateAsync(
        UndoableDataChange change,
        bool useAfter,
        CancellationToken cancellationToken)
    {
        var snapshot = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
        var fields = (useAfter ? change.FieldsAfter : change.FieldsBefore)
            ?? snapshot.FieldDefinitions;
        var records = snapshot.Records.ToDictionary(record => record.Id);
        foreach (var recordChange in change.RecordChanges)
        {
            var state = useAfter ? recordChange.After : recordChange.Before;
            if (state is null)
            {
                _ = records.Remove(recordChange.RecordId);
            }
            else
            {
                records[recordChange.RecordId] = state;
            }
        }

        var fieldIssues = DomainModelValidator.ValidateFieldDefinitions(fields);
        if (fieldIssues.Count > 0)
        {
            throw new HistoryConstraintException(string.Join(
                Environment.NewLine,
                fieldIssues.Select(issue => issue.Message)));
        }

        foreach (var record in records.Values)
        {
            var result = DomainModelValidator.ValidateRecord(
                record,
                fields,
                snapshot.AssetTypes,
                snapshot.Tags);
            if (!result.IsValid)
            {
                throw new HistoryConstraintException(string.Join(
                    Environment.NewLine,
                    result.Issues.Select(issue => issue.Message)));
            }
        }

        var duplicate = records.Values
            .Where(record => record.TargetPath is not null)
            .GroupBy(record => NormalizePathKey(record.TargetPath!.Path), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new HistoryConstraintException(
                $"対象パス'{duplicate.Key}'は別のレコードですでに使用されています。");
        }
    }

    private static string NormalizePathKey(string path)
    {
        return path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
            .TrimEnd(Path.DirectorySeparatorChar);
    }
}

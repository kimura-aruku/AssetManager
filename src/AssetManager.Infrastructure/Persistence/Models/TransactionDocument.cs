namespace AssetManager.Infrastructure.Persistence.Models;

public enum TransactionState
{
    Preparing,
    Prepared,
    Applying,
    Committed,
}

public sealed record TransactionEntryDocument(
    string RelativePath,
    bool ExistedBeforeTransaction);

public sealed record TransactionDocument(
    int SchemaVersion,
    string Id,
    TransactionState State,
    IReadOnlyList<TransactionEntryDocument> Entries);

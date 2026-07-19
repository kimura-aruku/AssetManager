using System.Text.Json;

namespace AssetManager.Infrastructure.Persistence.Models;

public sealed record RecordDocument(
    int SchemaVersion,
    string Id,
    Dictionary<string, JsonElement> Values,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

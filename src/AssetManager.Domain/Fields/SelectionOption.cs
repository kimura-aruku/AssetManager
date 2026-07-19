using AssetManager.Domain.Common;
using AssetManager.Domain.Identifiers;

namespace AssetManager.Domain.Fields;

public sealed record SelectionOption
{
    public SelectionOption(SelectionOptionId id, string label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            throw new DomainValidationException("選択肢名を指定してください。", nameof(label));
        }

        Id = id;
        Label = label;
    }

    public SelectionOptionId Id { get; }

    public string Label { get; }
}

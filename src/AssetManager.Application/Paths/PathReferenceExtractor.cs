using AssetManager.Domain.Records;
using AssetManager.Domain.Values;

namespace AssetManager.Application.Paths;

public static class PathReferenceExtractor
{
    public static IReadOnlyList<string> Extract(IEnumerable<AssetRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in records)
        {
            foreach (var value in record.Values.Values)
            {
                switch (value)
                {
                    case TargetPathFieldValue target:
                        Add(paths, target.Path);
                        break;
                    case FilePathFieldValue file:
                        Add(paths, file.Value);
                        break;
                    case FolderPathFieldValue folder:
                        Add(paths, folder.Value);
                        break;
                    case RelatedDocumentListFieldValue documents:
                        foreach (var document in documents.Values)
                        {
                            Add(paths, document.Path);
                        }

                        break;
                }
            }
        }

        return paths.ToArray();
    }

    private static void Add(HashSet<string> paths, string path)
    {
        try
        {
            _ = paths.Add(WindowsPathNormalizer.CreateComparisonKey(path));
        }
        catch (WindowsPathValidationException)
        {
            _ = paths.Add(path);
        }
    }
}

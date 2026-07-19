namespace AssetManager.Application.Paths;

public static class WindowsPathNormalizer
{
    public static string NormalizeForStorage(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new WindowsPathValidationException("パスを指定してください。");
        }

        var value = RemovePairedOuterQuotes(input);
        if (value.Contains("://", StringComparison.Ordinal))
        {
            throw new WindowsPathValidationException("URLはローカルパスとして登録できません。");
        }

        if (value.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
        {
            throw new WindowsPathValidationException("UNCパスは登録できません。");
        }

        if (value.StartsWith(@"\\?\", StringComparison.Ordinal))
        {
            value = value[4..];
        }
        else if (value.StartsWith(@"\\", StringComparison.Ordinal))
        {
            throw new WindowsPathValidationException("UNCパスは登録できません。");
        }

        if (!Path.IsPathFullyQualified(value))
        {
            throw new WindowsPathValidationException("Windowsの絶対パスを指定してください。");
        }

        string normalized;
        try
        {
            normalized = Path.GetFullPath(value)
                .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new WindowsPathValidationException($"パスの形式が正しくありません: {exception.Message}");
        }

        var root = Path.GetPathRoot(normalized)
            ?? throw new WindowsPathValidationException("ドライブルートを判定できません。");
        normalized = TrimTrailingSeparatorExceptRoot(normalized, root);
        if (normalized.Length >= 2 && normalized[1] == ':')
        {
            normalized = char.ToUpperInvariant(normalized[0]) + normalized[1..];
        }

        return normalized;
    }

    public static string CreateComparisonKey(string input)
    {
        return NormalizeForStorage(input);
    }

    private static string RemovePairedOuterQuotes(string value)
    {
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            return value[1..^1];
        }

        if (value.StartsWith('"') || value.EndsWith('"'))
        {
            throw new WindowsPathValidationException("パスを囲む引用符が対応していません。");
        }

        return value;
    }

    private static string TrimTrailingSeparatorExceptRoot(string path, string root)
    {
        return path.Length == root.Length
            ? path
            : path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}

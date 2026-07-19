namespace AssetManager.Domain.Common;

public sealed class DomainValidationException : ArgumentException
{
    public DomainValidationException(string message, string? parameterName = null)
        : base(message, parameterName)
    {
    }
}

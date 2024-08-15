namespace Shared;

public record ServiceInfo
{
    public required string Namespace { get; init; }
    public required string Service { get; init; }
    public required string LocalPort { get; init; }
    public required int ParsedPort { get; init; }
}
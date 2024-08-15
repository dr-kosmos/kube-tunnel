namespace Shared;

public record PortForwardConfig
{
    public required string Namespace { get; init; }
    public required string Service { get; init; }
    public required int LocalPort { get; init; }
    public required int RemotePort { get; init; }
}
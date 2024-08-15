using System.Text.Json.Serialization;

namespace Shared;

public class Config
{
    public const string DefaultConfigName = "default";
    public string CurrentProfile { get; set; } = DefaultConfigName;
}
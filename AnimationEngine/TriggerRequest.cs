using System.Text.Json.Serialization;

namespace AnimationEngine;

public class TriggerRequest
{
    private string? _style;
    private string? _speed;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("style")]
    public string Style
    {
        get => _style ?? "plane";
        set => _style = value;
    }

    [JsonPropertyName("speed")]
    public string Speed
    {
        get => _speed ?? "normal";
        set => _speed = value;
    }
}

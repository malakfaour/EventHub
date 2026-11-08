using System.Text.Json;
using System.Text.Json.Serialization;

namespace EventHub.Contracts;

/// <summary>One JSON configuration used by both apps so the wire format never drifts.</summary>
public static class MessageSerializer
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static byte[] Serialize(object message) => JsonSerializer.SerializeToUtf8Bytes(message, message.GetType(), Options);

    public static string SerializeToString(object message) => JsonSerializer.Serialize(message, message.GetType(), Options);

    public static T? Deserialize<T>(ReadOnlySpan<byte> body) => JsonSerializer.Deserialize<T>(body, Options);

    public static object? Deserialize(string json, Type type) => JsonSerializer.Deserialize(json, type, Options);
}

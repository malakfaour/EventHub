namespace EventHub.Web.Models;

/// <summary>Kinds of event shown to clients. Each type has its own cover colors and icon (no image needed).</summary>
public static class EventTypes
{
    public static readonly string[] All = ["Music", "Conference", "Party", "Wedding", "Food & Drink", "Sports", "Other"];

    public static string Css(string type) => type switch
    {
        "Music" => "cover-music",
        "Conference" => "cover-conference",
        "Party" => "cover-party",
        "Wedding" => "cover-wedding",
        "Food & Drink" => "cover-food",
        "Sports" => "cover-sports",
        _ => "cover-other"
    };

    public static string Icon(string type) => type switch
    {
        "Music" => "🎵",
        "Conference" => "🎤",
        "Party" => "🎉",
        "Wedding" => "💍",
        "Food & Drink" => "🍷",
        "Sports" => "⚽",
        _ => "✨"
    };
}

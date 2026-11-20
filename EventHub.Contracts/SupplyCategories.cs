namespace EventHub.Contracts;

/// <summary>
/// The fixed supply categories. Each supplier has exactly one specialty. The slug is used in routing keys
/// and queue names, so it stays lower-case and free of dots.
/// </summary>
public static class SupplyCategories
{
    public const string Catering = "catering";
    public const string Decoration = "decoration";
    public const string Sound = "sound";

    public static readonly IReadOnlyList<string> All = [Catering, Decoration, Sound];

    public static string DisplayName(string category) => category switch
    {
        Catering => "Catering",
        Decoration => "Decoration",
        Sound => "Sound and Lighting",
        _ => category
    };

    public static bool IsValid(string? category) => category is not null && All.Contains(category);
}

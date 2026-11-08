namespace EventHub.Contracts;

/// <summary>Starting category list seeded into BOTH databases; later changes travel as CategoryChanged messages.</summary>
public static class SeedData
{
    public static readonly IReadOnlyList<(int Id, string Name)> Categories =
    [
        (1, "Music"), (2, "Sports"), (3, "Technology"), (4, "Arts & Culture"),
        (5, "Food & Drink"), (6, "Education"), (7, "Community")
    ];
}

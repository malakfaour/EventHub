namespace EventHub.Contracts;

public sealed record DemoProduct(Guid Id, string Name, string Unit, decimal Price, int Stock, int MinLevel);

public sealed record DemoSupplier(Guid Id, string Key, string Name, string Category, string Email, IReadOnlyList<DemoProduct> Products);

/// <summary>
/// Fake data shared by both seeders so the demo orders in the Event Hub point at real products of the Supplier System.
/// Fixed ids are used on purpose; a real system would never share ids like this.
/// </summary>
public static class DemoData
{
    private static Guid SupplierId(int n) => Guid.Parse($"a1000000-0000-0000-0000-{n:D12}");
    private static Guid ProductId(int s, int n) => Guid.Parse($"b1000000-0000-0000-{s:D4}-{n:D12}");

    public static readonly DemoSupplier GoldenFork = new(SupplierId(1), "golden", "Golden Fork Catering", SupplyCategories.Catering, "orders@goldenfork.example",
    [
        new(ProductId(1, 1), "Buffet lunch", "person", 22m, 400, 40),
        new(ProductId(1, 2), "Finger food platter", "platter", 55m, 120, 15),
        new(ProductId(1, 3), "Soft drinks package", "person", 6m, 600, 50),
        new(ProductId(1, 4), "Dessert table", "table", 180m, 20, 3)
    ]);

    public static readonly DemoSupplier RoyalFeast = new(SupplierId(2), "royal", "Royal Feast Catering", SupplyCategories.Catering, "orders@royalfeast.example",
    [
        new(ProductId(2, 1), "Gala dinner (3 courses)", "person", 48m, 250, 25),
        new(ProductId(2, 2), "Coffee & pastry break", "person", 9m, 500, 40),
        new(ProductId(2, 3), "Cocktail reception", "person", 30m, 300, 30)
    ]);

    public static readonly DemoSupplier BloomCo = new(SupplierId(3), "bloom", "Bloom & Co Decoration", SupplyCategories.Decoration, "hello@bloomco.example",
    [
        new(ProductId(3, 1), "Flower centerpiece", "piece", 35m, 80, 10),
        new(ProductId(3, 2), "Stage backdrop", "piece", 400m, 5, 1),
        new(ProductId(3, 3), "Balloon arch", "piece", 120m, 10, 2),
        new(ProductId(3, 4), "Table linen set", "set", 12m, 200, 20)
    ]);

    public static readonly DemoSupplier SoundWave = new(SupplierId(4), "soundwave", "SoundWave Pro", SupplyCategories.Sound, "booking@soundwave.example",
    [
        new(ProductId(4, 1), "PA system (small hall)", "day", 300m, 6, 1),
        new(ProductId(4, 2), "Wireless microphone", "piece", 25m, 30, 4),
        new(ProductId(4, 3), "DJ set with equipment", "evening", 650m, 3, 1),
        new(ProductId(4, 4), "Stage lighting kit", "day", 220m, 5, 1)
    ]);

    public static readonly IReadOnlyList<DemoSupplier> Suppliers = [GoldenFork, RoyalFeast, BloomCo, SoundWave];
}

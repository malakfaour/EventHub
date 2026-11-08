namespace EventHub.Admin.Models;

/// <summary>Master category list. Changes are published to the Client App as CategoryChanged.</summary>
public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

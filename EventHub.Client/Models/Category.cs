namespace EventHub.Client.Models;

/// <summary>Local copy of the Admin category list, kept in sync by CategoryChanged messages.</summary>
public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

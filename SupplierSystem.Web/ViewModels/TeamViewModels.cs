using System.ComponentModel.DataAnnotations;

namespace SupplierSystem.Web.ViewModels;

public record TeamMemberRow(Guid Id, string FullName, string Email, string Role, bool IsLocked);

public record SupplierRow(Guid Id, string Name, string Category, string ContactEmail, bool IsActive, int Products, int Staff);

public class NewStaffViewModel
{
    [Required, StringLength(100), Display(Name = "Full name")]
    public string FullName { get; set; } = "";

    [Required, EmailAddress, StringLength(200)]
    public string Email { get; set; } = "";

    [Required, StringLength(100, MinimumLength = 8), DataType(DataType.Password), Display(Name = "Temporary password")]
    public string Password { get; set; } = "";
}

public class NewSupplierViewModel
{
    [Required, StringLength(150, MinimumLength = 2), Display(Name = "Company name")]
    public string Name { get; set; } = "";

    [Required, Display(Name = "Specialty")]
    public string Category { get; set; } = "";

    [Required, EmailAddress, StringLength(200), Display(Name = "Company contact email")]
    public string ContactEmail { get; set; } = "";

    [Required, StringLength(100), Display(Name = "Manager full name")]
    public string ManagerName { get; set; } = "";

    [Required, EmailAddress, StringLength(200), Display(Name = "Manager email (login)")]
    public string ManagerEmail { get; set; } = "";

    [Required, StringLength(100, MinimumLength = 8), DataType(DataType.Password), Display(Name = "Temporary password")]
    public string ManagerPassword { get; set; } = "";
}

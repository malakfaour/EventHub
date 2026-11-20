using System.ComponentModel.DataAnnotations;

namespace EventHub.Web.ViewModels;

public class RegisterViewModel
{
    [Required, StringLength(100), Display(Name = "Display name")]
    public string DisplayName { get; set; } = "";

    [StringLength(150)]
    public string? Organization { get; set; }

    /// <summary>"Client" buys tickets, "Organizer" plans events. Admin accounts can never be created here.</summary>
    [Required, RegularExpression("^(Client|Organizer)$")]
    public string AccountType { get; set; } = "Client";

    [Required, EmailAddress]
    public string Email { get; set; } = "";

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [DataType(DataType.Password), Display(Name = "Confirm password"), Compare(nameof(Password))]
    public string ConfirmPassword { get; set; } = "";
}

public class LoginViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = "";

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [Display(Name = "Remember me")]
    public bool RememberMe { get; set; }
}

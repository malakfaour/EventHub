namespace EventHub.Web.Models;

public static class Roles
{
    public const string Organizer = "Organizer";
    public const string Client = "Client";
    public const string Admin = "Admin";
    public static readonly string[] All = [Organizer, Client, Admin];
}

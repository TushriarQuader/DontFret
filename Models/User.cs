using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace DontFret.Models;
/// <summary>
/// User roles:
/// - User: Basic role for all registered users, including customers and staff. Has access to general features like browsing the catalogue and managing their profile.
/// - Customer: Role for users who can place orders and write reviews. Inherits all permissions of the User role.
/// - OfficeClerk: Role for staff responsible for processing orders and managing inventory. Has permissions to view and update all orders, but cannot manage user accounts or site settings.
/// - Admin: Role for site administrators with full permissions, including managing user accounts, viewing and updating all orders, and configuring site settings.
/// </summary>
enum Roles
{
    User,
    Customer,
    OfficeClerk,
    Admin
}
/// <summary>
/// User class extending IdentityUser with additional properties
/// </summary>
public class User : IdentityUser
{
    /// <summary>
    /// Stuff that IdentityUser doesn't have but we want to store about the user 
    /// Important for delivery/invoice
    /// </summary>
    [Required]
    public string Address { get; set; } = string.Empty;

    [Required]
    [StringLength(8)]// Postcode only needs VARCHAR2(8)
    public string Postcode { get; set; } = string.Empty;
}

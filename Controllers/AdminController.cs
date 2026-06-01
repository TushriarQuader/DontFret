using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DontFret.Data;
using DontFret.Models;
using DontFret.Services;

namespace DontFret.Controllers;
/// <summary>
/// Controller that provides administrative dashboards and management actions for orders, products, inventory, and
/// users. Actions are restricted to users in the Admin and OfficeClerk roles, with additional Admin-only operations.
/// </summary>
/// <remarks>Many actions synchronously block on asynchronous Identity APIs (GetRolesAsync, GetUsersInRoleAsync,
/// FindByIdAsync, CreateAsync, UpdateAsync, DeleteAsync), which can cause thread-pool starvation; consider making
/// controller actions asynchronous and using await. Several actions populate ViewData keys such as "TotalOrders",
/// "TotalRevenue", "LowStockCount", "PendingOrders", "TotalProducts", "TotalUsers", "AdminCount", "ClerkCount",
/// "CustomerCount", "TotalStockValue", "OutOfStockCount", "GuitarCount", "AmpCount", and "AccessoryCount".</remarks>
/// <param name="db">Entity Framework Core database context used to query and update orders, products, and related entities.</param>
/// <param name="userManager">ASP.NET Core Identity UserManager used to create, update, delete, and manage users and their roles.</param>
[Authorize(Roles = "Admin,OfficeClerk")]
public class AdminController(ApplicationDbContext db, UserManager<User> userManager, WishlistAlertService wishlistAlertService) : Controller
{
    public IActionResult Index()
    {
        var totalOrders = db.Orders.Count();
        var totalRevenue = db.Orders.Sum(o => o.TotalAmount);
        var lowStockCount = db.Products.Count(p => p.StockQuantity < 5);
        var pendingOrders = db.Orders.Count(o => o.Status == OrderStatus.Pending);

        ViewData["TotalOrders"] = totalOrders;
        ViewData["TotalRevenue"] = totalRevenue;
        ViewData["LowStockCount"] = lowStockCount;
        ViewData["PendingOrders"] = pendingOrders;

        return View();
    }
    /// <summary>
    /// Show the admin/officeclerk dashboard with key metrics: total orders, total revenue, low stock count, pending orders, total products,
    /// </summary>
    /// <returns>Returns the actual dashboard view</returns>
    public IActionResult DashBoard()
    {
        var totalOrders = db.Orders.Count();
        var totalRevenue = db.Orders.Sum(o => o.TotalAmount);
        var lowStockCount = db.Products.Count(p => p.StockQuantity < 5);
        var pendingOrders = db.Orders.Count(o => o.Status == OrderStatus.Pending);
        var totalProducts = db.Products.Count();
        var totalUsers = userManager.Users.Count();

        ViewData["TotalOrders"] = totalOrders;
        ViewData["TotalRevenue"] = totalRevenue;
        ViewData["LowStockCount"] = lowStockCount;
        ViewData["PendingOrders"] = pendingOrders;
        ViewData["TotalProducts"] = totalProducts;
        ViewData["TotalUsers"] = totalUsers;

        return View();
    }
    /// <summary>
    /// Products Management
    /// </summary>
    /// <returns></returns>
    public IActionResult ManageProducts()
    {
        return RedirectToAction("Index", "Products");
    }
    /// <summary>
    /// Inventory Management
    /// </summary>
    /// <returns></returns>
    public IActionResult ManageInventory()
    {
        var products = db.Products.OrderBy(p => p.Name).ToList();
        return View(products);
    }

    [HttpPost]
    public async Task<IActionResult> UpdateStock(int productId, int newStock)
    {
        var product = db.Products.Find( productId );
        if( product is null )
            return NotFound();

        var oldStock = product.StockQuantity;
        product.StockQuantity = newStock;
        db.SaveChanges();

        if (oldStock == 0 && newStock > 0)
            await wishlistAlertService.SendStockAlertAsync(productId);

        return RedirectToAction(nameof(ManageInventory));
    }
    /// <summary>
    /// User Management (Admin-only CRUD)
    /// </summary>
    /// <returns></returns>
    [Authorize(Roles = "Admin")]
    public IActionResult ManageUsers()
    {
        var users = userManager.Users.ToList();
        var userRoles = new List<(User user, IList<string> roles)>();

        foreach( var user in users )
        {
            var roles = userManager.GetRolesAsync(user).Result;
            userRoles.Add( ( user, roles ) );
        }

        return View(userRoles);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public IActionResult ChangeRole(string userId, string role)
    {
        var user = userManager.FindByIdAsync(userId).Result;
        if (user is null)
            return NotFound();

        var currentRoles = userManager.GetRolesAsync(user).Result;
        userManager.RemoveFromRolesAsync(user, currentRoles).Wait();
        userManager.AddToRoleAsync(user, role).Wait();

        return RedirectToAction(nameof(ManageUsers));
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public IActionResult DeleteUser(string userId)
    {
        var user = userManager.FindByIdAsync( userId ).Result;
        if( user is null )
            return NotFound();

        var adminUsers = userManager.GetUsersInRoleAsync("Admin").Result;
        if( adminUsers.Count <= 1 && adminUsers.Any( u => u.Id == userId ) )
            return RedirectToAction( nameof( ManageUsers ) );

        userManager.DeleteAsync(user).Wait();

        return RedirectToAction( nameof( ManageUsers ) );
    }
    /// <summary>
    /// Returns a view displaying each user with their assigned roles and exposes user counts (TotalUsers, AdminCount,
    /// ClerkCount, CustomerCount) via ViewData.
    /// </summary>
    /// <remarks>Synchronously blocks on asynchronous Identity calls (GetRolesAsync, GetUsersInRoleAsync),
    /// which can cause thread-pool starvation; consider making the action asynchronous and using await. Populates
    /// ViewData keys: "TotalUsers", "AdminCount", "ClerkCount", "CustomerCount".</remarks>
    /// <returns>An IActionResult that renders a view with a model of List<(User user, IList<string> roles)> representing users
    /// and their roles.</returns>
    public IActionResult ViewUsers()
    {
        var users = userManager.Users.ToList();
        var userRoles = new List<(User user, IList<string> roles)>();

        foreach( var user in users )
        {
            var roles = userManager.GetRolesAsync(user).Result;
            userRoles.Add( ( user, roles ) );
        }

        int
            adminCount = userManager.GetUsersInRoleAsync( "Admin" ).Result.Count, 
            clerkCount = userManager.GetUsersInRoleAsync( "OfficeClerk" ).Result.Count,
            customerCount = userManager.GetUsersInRoleAsync( "Customer" ).Result.Count;
        // Need to include this in the view to show the counts of each
        ViewData["TotalUsers"] = users.Count;
        ViewData["AdminCount"] = adminCount;
        ViewData["ClerkCount"] = clerkCount;
        ViewData["CustomerCount"] = customerCount;

        return View( userRoles );
    }

    [Authorize(Roles = "Admin")]
    public IActionResult CreateUser()
    {
        ViewData["Roles"] = new List<string> { "Admin", "OfficeClerk", "Customer" };
        return View();
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public IActionResult CreateUser(string userName, string email, string phoneNumber, string address, string postcode, string password, string role)
    {
        if( string.IsNullOrWhiteSpace( email ) || string.IsNullOrWhiteSpace( password ) )
        {
            ModelState.AddModelError( "", "Email and password are required." );
            ViewData["Roles"] = new List<string> { "Admin", "OfficeClerk", "Customer" };

            return View();
        }

        User user = role switch
        {   //!-TODO-!: maybe Admins and OfficeClerks should have basket and wishlist set? Might cause problems later on
            "Admin" => new Admin(),
            "OfficeClerk" => new OfficeClerk(),
            _ => new Customer { Basket = new Basket(), Wishlist = new Wishlist() }
        };

        user.UserName = userName ?? email;
        user.Email = email;
        user.PhoneNumber = phoneNumber;
        user.Address = address ?? "";
        user.Postcode = postcode ?? "";
        user.EmailConfirmed = true;

        var result = userManager.CreateAsync(user, password).Result;
        if(result.Succeeded)
        {
            if (!string.IsNullOrWhiteSpace(role))
                userManager.AddToRoleAsync(user, role).Wait();

            return RedirectToAction(nameof(ViewUsers));
        }

        foreach (var error in result.Errors)
            ModelState.AddModelError("", error.Description);

        ViewData["Roles"] = new List<string> { "Admin", "OfficeClerk", "Customer" };
        return View();
    }

    [Authorize(Roles = "Admin")]
    public IActionResult EditUser(string id)
    {
        var user = userManager.FindByIdAsync(id).Result;
        if( user is null )
            return NotFound();

        var roles = userManager.GetRolesAsync(user).Result;
        ViewData["CurrentRole"] = roles.FirstOrDefault() ?? "";
        ViewData["Roles"] = new List<string> { "Admin", "OfficeClerk", "Customer" };

        return View(user);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public IActionResult EditUser(string id, string userName, string email, string phoneNumber, string address, string postcode, string role)
    {
        var user = userManager.FindByIdAsync(id).Result;
        if( user is null )
            return NotFound();

        user.UserName = userName ?? email;
        user.Email = email;
        user.PhoneNumber = phoneNumber;
        user.Address = address ?? "";
        user.Postcode = postcode ?? "";

        var result = userManager.UpdateAsync(user).Result;
        if( result.Succeeded )
        {
            var currentRoles = userManager.GetRolesAsync(user).Result;
            if( !string.IsNullOrWhiteSpace(role) && !currentRoles.Contains(role))
            {
                userManager.RemoveFromRolesAsync(user, currentRoles).Wait();
                userManager.AddToRoleAsync(user, role).Wait();
            }

            return RedirectToAction(nameof(ViewUsers));
        }

        foreach (var error in result.Errors)
            ModelState.AddModelError("", error.Description);

        ViewData["Roles"] = new List<string> { "Admin", "OfficeClerk", "Customer" };
        ViewData["CurrentRole"] = userManager.GetRolesAsync(user).Result.FirstOrDefault() ?? "";
        return View(user);
    }

    public IActionResult UserDetails(string id)
    {
        var user = userManager.FindByIdAsync(id).Result;
        if (user is null)
            return NotFound();

        var roles = userManager.GetRolesAsync(user).Result;
        ViewData["Roles"] = roles;

        return View(user);
    }

    [Authorize(Roles = "Admin")]
    public IActionResult AssignRole(string id)
    {
        var user = userManager.FindByIdAsync(id).Result;
        if (user is null)
            return NotFound();

        var roles = userManager.GetRolesAsync(user).Result;
        ViewData["CurrentRole"] = roles.FirstOrDefault() ?? "";
        ViewData["Roles"] = new List<string> { "Admin", "OfficeClerk", "Customer" };

        return View(user);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public IActionResult AssignRole(string id, string role)
    {
        var user = userManager.FindByIdAsync(id).Result;
        if (user is null)
            return NotFound();

        var currentRoles = userManager.GetRolesAsync(user).Result;
        userManager.RemoveFromRolesAsync(user, currentRoles).Wait();
        userManager.AddToRoleAsync(user, role).Wait();

        return RedirectToAction(nameof(ViewUsers));
    }

    [Authorize(Roles = "Admin")]
    public IActionResult Ban(string id)
    {
        var user = userManager.FindByIdAsync(id).Result;
        if (user is null)
            return NotFound();

        return View(user);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ActionName("Ban")]
    public IActionResult BanConfirmed(string id)
    {
        var user = userManager.FindByIdAsync(id).Result;
        if (user is null)
            return NotFound();

        var adminUsers = userManager.GetUsersInRoleAsync("Admin").Result;
        if (adminUsers.Count <= 1 && adminUsers.Any(u => u.Id == id))
            return RedirectToAction(nameof(ViewUsers));

        userManager.DeleteAsync(user).Wait();

        return RedirectToAction(nameof(ViewUsers));
    }
    /// <summary>
    /// Inventory Dashboard (Admin & OfficeClerk)
    /// </summary>
    /// <returns></returns>
    public IActionResult InventoryDashboard()
    {
        var products = db.Products.OrderBy(p => p.Name).ToList();
        var totalValue = products.Sum(p => p.Price * p.StockQuantity);
        var lowStockCount = products.Count(p => p.StockQuantity > 0 && p.StockQuantity < 5);
        var outOfStockCount = products.Count(p => p.StockQuantity == 0);

        ViewData["TotalProducts"] = products.Count;
        ViewData["TotalStockValue"] = totalValue;
        ViewData["LowStockCount"] = lowStockCount;
        ViewData["OutOfStockCount"] = outOfStockCount;

        return View(products);
    }
    /// <summary>
    /// Product Dashboard (Admin only)
    /// </summary>
    /// <returns></returns>
    [Authorize(Roles = "Admin")]
    public IActionResult ProductDashboard()
    {
        var products = db.Products.OrderBy(p => p.Name).ToList();
        var guitarCount = products.Count(p => p.Category == Category.Guitars);
        var ampCount = products.Count(p => p.Category == Category.Amplifiers);
        var accessoryCount = products.Count(p => p.Category == Category.Accessories);

        ViewData["TotalProducts"] = products.Count;
        ViewData["GuitarCount"] = guitarCount;
        ViewData["AmpCount"] = ampCount;
        ViewData["AccessoryCount"] = accessoryCount;

        return View(products);
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using DontFret.Data;
using DontFret.Models;

namespace DontFret.Controllers;
/// <summary>
/// Customers can manage their own wishlist, but office clerks and admins can view all wishlists for customer service purposes
/// </summary>
[Authorize(Roles = "Customer,OfficeClerk,Admin")]
public class WishlistController(ApplicationDbContext db) : Controller
{
    /// <summary>
    /// GET: Wishlist - Show wishlist page 
    /// </summary>
    /// <returns></returns>
    public IActionResult Index()
    {
        var userId = User.FindFirstValue( ClaimTypes.NameIdentifier );
        var customer = db.Users
            .OfType<Customer>()
            .Include( c => c.Wishlist )
            .ThenInclude( w => w.Items )
            .ThenInclude( wi => wi.Product )
        .FirstOrDefault( c => c.Id == userId );
        // Put the wishlist in the Page
        return View( customer?.Wishlist?.Items.ToList() ?? [] );
    }
    /// <summary>
    /// Adds products to wishlist. 
    /// If the user doesn't have a wishlist, it will be created automatically. 
    /// If the product is already in the wishlist, it won't be added again.
    /// </summary>
    /// <param name="productId"></param>
    /// <returns></returns>
    [HttpPost]
    public IActionResult AddToWishlist(int productId)
    {
        var userId = User.FindFirstValue( ClaimTypes.NameIdentifier );
        var customer = db.Users
            .OfType<Customer>()
            .Include( c => c.Wishlist )
            .ThenInclude( w => w.Items )
        .FirstOrDefault( c => c.Id == userId );

        if( customer is null )
            return RedirectToAction( nameof( Index ) );

        if( customer.Wishlist is null )
        {
            customer.Wishlist = new Wishlist { CustomerId = customer.Id };
            db.Wishlists.Add( customer.Wishlist );
        }
        // Only add to wishlist if it's not already there
        if ( !customer.Wishlist.Items.Any( i => i.ProductId == productId ) )
        {
            customer.Wishlist.Items.Add( new WishlistItem
            {
                ProductId = productId,
                DateAdded = DateTime.UtcNow
            });

            db.SaveChanges();
        }

        TempData["SuccessMessage"] = "Item added to wishlist successfully!";
        return RedirectToAction( "Details", "Products", new { id = productId } );
    }

    [HttpGet]
    [ActionName("AddToWishlist")]// this just lets me remap the GET request to the same URL as the POST handler,
                                 // so that if a user tries to access the POST URL via GET (e.g. after login redirect),
                                 // it won't 405 but will just redirect back to the wishlist index page
    public IActionResult AddToWishlistGet(int productId)
    {
        _ = productId;
        return RedirectToAction( nameof( Index ) );
    }

    [HttpPost]
    public IActionResult RemoveFromWishlist(int id)
    {
        var userId = User.FindFirstValue( ClaimTypes.NameIdentifier );
        var item = db.WishlistItems.Find( id );

        if( item is null )
            return RedirectToAction( nameof( Index ) );

        var wishlist = db.Wishlists
            .FirstOrDefault( w => w.Id == item.WishlistId && w.CustomerId == userId );

        if( wishlist is null )
            return NotFound();

        db.WishlistItems.Remove( item );
        db.SaveChanges();

        return RedirectToAction( nameof( Index ) );
    }

    [HttpPost]
    public IActionResult ToggleStockAlert(int id)
    {
        var userId = User.FindFirstValue( ClaimTypes.NameIdentifier );
        var item = db.WishlistItems
            .Include(wi => wi.Wishlist)
            .FirstOrDefault(wi => wi.Id == id && wi.Wishlist.CustomerId == userId);

        if( item is null )
            return NotFound();

        item.StockAlerts = !item.StockAlerts;
        db.SaveChanges();

        TempData["SuccessMessage"] = item.StockAlerts
            ? "Stock alerts enabled for this item."
            : "Stock alerts disabled for this item.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public IActionResult ToggleDiscountAlert(int id)
    {
        var userId = User.FindFirstValue( ClaimTypes.NameIdentifier );
        var item = db.WishlistItems
            .Include(wi => wi.Wishlist)
            .FirstOrDefault(wi => wi.Id == id && wi.Wishlist.CustomerId == userId);

        if( item is null )
            return NotFound();

        item.DiscountAlerts = !item.DiscountAlerts;
        db.SaveChanges();

        TempData["SuccessMessage"] = item.DiscountAlerts
            ? "Discount alerts enabled for this item."
            : "Discount alerts disabled for this item.";
        return RedirectToAction(nameof(Index));
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using DontFret.Data;
using DontFret.Models;

namespace DontFret.Controllers;
/// <summary>
/// Basket manager class
/// </summary>
/// <param name="db"></param>
[Authorize(Roles = "Customer,OfficeClerk,Admin")]
public class BasketController(ApplicationDbContext db) : Controller
{
    /// <summary>
    /// Shows the basket contents for the current user. If the user has no basket, an empty list is shown.
    /// </summary>
    /// <returns></returns>
    public IActionResult Index()
    {
        var userId = User.FindFirstValue( ClaimTypes.NameIdentifier );
        var customer = db.Users
            .OfType<Customer>()
            .Include( c => c.Basket )
            .ThenInclude( b => b.Items )
            .ThenInclude( bi => bi.Product )
        .FirstOrDefault( c => c.Id == userId );

        if( customer?.Basket is null )
            return View( new List<BasketItem>() );

        return View( customer.Basket.Items.ToList() );
    }
    /// <summary>
    /// Add a product to the basket (or update quantity if it already exists)
    /// </summary>
    /// <param name="productId"></param>
    /// <param name="quantity"></param>
    /// <returns></returns>
    [HttpPost]
    public IActionResult AddToBasket(int productId, int quantity = 1)
    {
        var userId = User.FindFirstValue( ClaimTypes.NameIdentifier );
        var customer = db.Users
            .OfType<Customer>()
            .Include( c => c.Basket )
            .ThenInclude( b => b.Items )
            .FirstOrDefault( c => c.Id == userId );

        if( customer is null )
            return RedirectToAction( nameof( Index ) );

        var product = db.Products.Find( productId );
        if( product is null )
            return NotFound();
        // Out of stock products can't be added to the basket
        if( product.StockQuantity == 0 )
        {   // Show an error message and redirect back to the product details page
            TempData["ErrorMessage"] = "Sorry, this item is currently out of stock.";
            return RedirectToAction( "Details", "Products", new { id = productId } );
        }
        // If the user doesn't have a basket yet, create one
        if ( customer.Basket is null )
        {
            customer.Basket = new Basket { CustomerId = customer.Id };
            db.Baskets.Add( customer.Basket );
        }

        var existingItem = customer.Basket.Items
            .FirstOrDefault( i => i.ProductId == productId );

        var newQuantity = existingItem is not null ? existingItem.Quantity + quantity : quantity;
        // Greedy customers might try to add more items to their basket than are in stock
        // so prevent this and show an error message if they do
        if( newQuantity > product.StockQuantity )
        {
            TempData["ErrorMessage"] = $"Sorry, only {product.StockQuantity} of '{product.Name}' are available. You have {existingItem?.Quantity ?? 0} in your basket.";
            return RedirectToAction( "Details", "Products", new { id = productId } );
        }

        if( existingItem is not null )
            existingItem.Quantity += quantity;
        else
        {
            customer.Basket.Items.Add( new BasketItem
            {
                ProductId = productId,
                Quantity = quantity
            });
        }

        db.SaveChanges();
        TempData["SuccessMessage"] = "Item added to basket successfully!";
        return RedirectToAction( "Details", "Products", new { id = productId } );
    }
    /// <summary>
    /// This is just a GET version of AddToBasket that redirects to the Index action.
    /// Avoids the analyser being annoying about unused parms
    /// </summary>
    /// <param name="productId"></param>
    /// <param name="quantity"></param>
    /// <returns></returns>
    [HttpGet]
    [ActionName("AddToBasket")]
    public IActionResult AddToBasketGet(int productId, int quantity = 1)
    {
        // Use/discard the parameters so analyzers don't report them as unused.
        _ = productId;
        _ = quantity;
        return RedirectToAction( nameof( Index ) );
    }
    /// <summary>
    /// Remove an item from the basket
    /// </summary>
    /// <param name="id"></param>
    /// <returns>Goes back to the basket view</returns>
    [HttpPost]
    public IActionResult RemoveFromBasket(int id)
    {
        var userId = User.FindFirstValue( ClaimTypes.NameIdentifier );
        var item = db.BasketItems.Find( id );

        if( item is null ) 
            return RedirectToAction( nameof( Index ) );

        var basket = db.Baskets
            .FirstOrDefault( b => b.Id == item.BasketId && b.CustomerId == userId );

        if( basket is null )
            return NotFound();

        db.BasketItems.Remove( item );
        db.SaveChanges();

        return RedirectToAction( nameof( Index ) );
    }
    /// <summary>
    /// Updates the quantity of a basket item for the current authenticated user; if quantity is less than or equal to
    /// zero, redirects to RemoveFromBasket.
    /// </summary>
    /// <remarks>Changes are persisted to the database. Requires an authenticated user; redirects to Index if
    /// the specified item is not found.</remarks>
    /// <param name="id">The identifier of the basket item to update.</param>
    /// <param name="quantity">The new quantity; if less than or equal to zero the item is removed from the basket.</param>
    /// <returns>A redirect to the basket index or to RemoveFromBasket, or NotFound if the basket does not belong to the current
    /// user.</returns>
    [HttpPost]
    public IActionResult UpdateQuantity(int id, int quantity)
    {
        if( quantity <= 0 ) 
            return RedirectToAction( nameof( RemoveFromBasket ), new { id } );

        var userId = User.FindFirstValue( ClaimTypes.NameIdentifier );
        var item = db.BasketItems.Find( id );

        if( item is null ) 
            return RedirectToAction( nameof( Index ) );

        var basket = db.Baskets
            .FirstOrDefault( b => b.Id == item.BasketId && b.CustomerId == userId );

        if( basket is null ) 
            return NotFound();

        var product = db.Products.Find( item.ProductId );
        if( product is null )
            return NotFound();
        // Clamp the basket quanity to the stock as the maximum
        if( quantity > product.StockQuantity )
        {
            TempData["ErrorMessage"] = $"Sorry, only {product.StockQuantity} of '{product.Name}' are available.";
            return RedirectToAction( nameof( Index ) );
        }

        item.Quantity = quantity;
        db.SaveChanges();

        return RedirectToAction( nameof( Index ) );
    }
}

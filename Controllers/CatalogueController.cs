using DontFret.Data;
using DontFret.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DontFret.Controllers;
/// <summary>
/// Catalogue controller for showing the Products and their Details
/// </summary>
/// <param name="db"></param>
public class CatalogueController(ApplicationDbContext db) : Controller
{
    /// <summary>
    /// GET: Catalogue, offers a search bar and filters to for product browsing
    /// </summary>
    /// <param name="searchTerm"></param>
    /// <param name="minPrice"></param>
    /// <param name="maxPrice"></param>
    /// <param name="inStock"></param>
    /// <param name="minRating"></param>
    /// <param name="category"></param>
    /// <returns></returns>
    public IActionResult Index(string? searchTerm, decimal? minPrice, decimal? maxPrice, bool? inStock, int? minRating, Category? category)
    {
        var query = db.Products.Include(p => p.Reviews).AsQueryable();

        if( minPrice.HasValue )
            query = query.Where( p => p.Price >= minPrice );

        if( maxPrice.HasValue )
            query = query.Where( p => p.Price <= maxPrice );

        if( inStock.HasValue && inStock.Value )
            query = query.Where( p => p.StockQuantity > 0 );

        if( category.HasValue )
            query = query.Where( p => p.Category == category);

        if( minRating.HasValue && minRating.Value > 0 )
            query = query.Where( p => p.Reviews.Average(r => (int)r.Score) >= minRating );
        // Search bar filter
        if( !string.IsNullOrWhiteSpace(searchTerm) )
            query = query.Where( p => p.Name.Contains(searchTerm) || p.Description.Contains(searchTerm) );

        var products = query.ToList();

        ViewData["SearchTerm"] = searchTerm;
        ViewData["MinPrice"] = minPrice;
        ViewData["MaxPrice"] = maxPrice;
        ViewData["InStock"] = inStock;
        ViewData["MinRating"] = minRating;
        ViewData["SelectedCategory"] = category;

        return View( products );
    }
    /// <summary>
    /// Shows the product details page which includes reviews and purchasing options
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public IActionResult Details(int id)
    {
        var product = db.Products
            .Include( p => p.Reviews)
                .ThenInclude( r => r.Customer)
            .FirstOrDefault( p => p.Id == id);

        if( product is null )
            return NotFound();

        if( User.Identity?.IsAuthenticated == true )
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            ViewData["HasPurchased"] = db.Orders
                .Where(o => o.CustomerId == userId && o.Status != OrderStatus.Cancelled && o.Status != OrderStatus.Returned)
                .Any(o => o.Items.Any(i => i.ProductId == id));
            ViewData["AlreadyReviewed"] = db.Reviews
                .Any(r => r.CustomerId == userId && r.ProductId == id);
        }
        else
        {
            ViewData["HasPurchased"] = false;
            ViewData["AlreadyReviewed"] = false;
        }

        return View(product);
    }
}

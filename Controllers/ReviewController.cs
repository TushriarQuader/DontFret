using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DontFret.Data;
using DontFret.Models;
using System.Security.Claims;

namespace DontFret.Controllers;
/// <summary>
/// Review controller for customers to create reviews for products they have purchased. 
/// Customers can only review a product once, and only if they have purchased it (and the order is not cancelled or returned).
/// </summary>
/// <param name="db"></param>
[Authorize(Roles = "Customer")]// only customers can write reviews (and they have to have purchased the product)
public class ReviewController(ApplicationDbContext db) : Controller
{
    public IActionResult Create(int productId)
    {
        var product = db.Products.FirstOrDefault( p => p.Id == productId );
        if( product is null )
            return NotFound();

        var userId = User.FindFirstValue( ClaimTypes.NameIdentifier )!;

        var purchased = db.Orders
            .Where( o => o.CustomerId == userId && o.Status != OrderStatus.Cancelled && o.Status != OrderStatus.Returned )
            .Any( o => o.Items.Any( i => i.ProductId == productId ) );

        if( !purchased )
            return RedirectToAction( "Details", "Catalogue", new { id = productId } );

        var alreadyReviewed = db.Reviews.Any( r => r.CustomerId == userId && r.ProductId == productId );

        if( alreadyReviewed )
            return RedirectToAction( "Details", "Catalogue", new { id = productId } );

        ViewData["ProductId"] = productId;
        ViewData["ProductName"] = product.Name;

        return View();
    }

    [HttpPost]
    public IActionResult Create(int productId, StarRating score, string? title, string? body)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var purchased = db.Orders
            .Where(o => o.CustomerId == userId && o.Status != OrderStatus.Cancelled && o.Status != OrderStatus.Returned)
            .Any(o => o.Items.Any(i => i.ProductId == productId));

        if( !purchased )
            return RedirectToAction( "Details", "Catalogue", new { id = productId } );

        var alreadyReviewed = db.Reviews.Any( r => r.CustomerId == userId && r.ProductId == productId );
        if( alreadyReviewed )
            return RedirectToAction( "Details", "Catalogue", new { id = productId } );

        db.Reviews.Add( new Review
        {
            ProductId = productId,
            CustomerId = userId,
            Score = score,
            Title = title,
            Body = body,
            CreatedAt = DateTime.UtcNow
        });

        db.SaveChanges();

        return RedirectToAction( "Details", "Catalogue", new { id = productId } );
    }
}

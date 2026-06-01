using DontFret.Data;
using DontFret.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace DontFret.Controllers;
/// <summary>
/// Landing page controller, does not do anything special
/// </summary>
/// <param name="logger"></param>
/// <param name="db"></param>
public class HomeController(ILogger<HomeController> logger, ApplicationDbContext db) : Controller
{
    /// <summary>
    /// Landing page
    /// </summary>
    /// <returns>Shows landing page that showcases products</returns>
    public IActionResult Index()
    {
        // Need to get all the products to show them on the homepage
        var products = db.Products
            .Include( p => p.Reviews ) // For rating calculation (since AverageRating was removed)
            .ToList();

        return View( products ); // Pass List<Product> as the model to the view
    }
    /// <summary>
    /// Show privacy statement
    /// </summary>
    /// <returns></returns>
    public IActionResult Privacy() => View();
    /// <summary>
    /// Show error page
    /// </summary>
    /// <returns></returns>
    [ResponseCache( Duration = 0, Location = ResponseCacheLocation.None, NoStore = true )]
    public IActionResult Error() => View( new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier } );
}


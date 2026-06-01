using DontFret.Data;
using DontFret.Models;
using DontFret.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
namespace DontFret.Controllers;
/// <summary>
/// Products manager class that lets users view products and product details, and allows administrators to create, edit, and delete products. The controller uses the application's database context to query and update product data. 
/// Role-based authorization is enforced: anyone can view products and details, but only users in the Admin role can create, edit, or delete products.
/// </summary>
/// <param name="context"></param>
public class ProductsController(ApplicationDbContext db, WishlistAlertService wishlistAlertService) : Controller
{
    /// <summary>
    /// GET: Products - anyone can view the list of products
    /// </summary>
    /// <returns>Returns the page with the list of products</returns>
    public IActionResult Index()
    {
        var products = db.Products;
        return View( products.ToList() );
    }
    /// <summary>
    /// GET: Products/Details/5 - anyone can view product details
    /// </summary>
    /// <param name="id"></param>
    /// <returns>Go to the Catalogue Details page, for that chosen product</returns>
    public IActionResult Details(int? id)
    {
        if( id is null )
            return NotFound();

        var product = db.Products.FirstOrDefault( p => p.Id == id );

        if( product is null )
            return NotFound();

        return RedirectToAction( "Details", "Catalogue", new { id } );
    }
    /// <summary>
    /// GET: Products/Create - only admins can create new products
    /// </summary>
    /// <returns>Returns the paghe for creating a new product</returns>
    [Authorize( Roles = "Admin" )]
    public IActionResult Create()
    {
        ViewData["Category"] = Enum.GetValues<Category>()
            .Select(c => new SelectListItem(c.ToString(), c.ToString())).ToList();
        return View();
    }
    /// <summary>
    ///  POST: Products/Create - only admins can create new products. 
    /// <param name="product"></param>
    /// <returns>Shows the page for the new product</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize( Roles = "Admin" )]
    public IActionResult Create( [Bind( "Id,Name,Description,Price,StockQuantity,Category")] Product product)
    {
        if( ModelState.IsValid )
        {
            db.Add(product);
            db.SaveChanges();

            return RedirectToAction( nameof( Index ) );
        }
        // The categories need to converted to a SelectList for the dropdown in the view
        // and we also need to set the selected value to the product's category
        ViewData["Category"] = Enum.GetValues<Category>()
            .Select(c => new SelectListItem(c.ToString(), c.ToString(), c == product.Category)).ToList();
        return View( product );
    }
    /// <summary>
    /// GET: Products/Edit/5 - only admins can edit products
    /// </summary>
    /// <param name="id"></param>
    /// <returns>Returns the page for editing a product</returns>
    [Authorize( Roles = "Admin" )]
    public IActionResult Edit(int? id)
    {
        if( id is null )
            return NotFound();

        var product = db.Products.Find( id );

        if( product is null )
            return NotFound();

        ViewData["Category"] = Enum.GetValues<Category>()
            .Select(c => new SelectListItem(c.ToString(), c.ToString(), c == product.Category)).ToList();
        return View(product);
    }
    /// <summary>
    /// POST: Products/Edit/5 - only admins can edit products
    /// </summary>
    /// <param name="id"></param>
    /// <param name="product"></param>
    /// <returns>Goes back to the product list page (Index)</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize( Roles = "Admin" )]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Description,Price,StockQuantity,Category")] Product product)
    {
        if( id != product.Id )
            return NotFound();

        if( ModelState.IsValid )
        {
            var oldProduct = db.Products.AsNoTracking().FirstOrDefault(p => p.Id == id);
            var oldPrice = oldProduct?.Price;

            try
            {
                db.Update( product );
                db.SaveChanges();

                if (oldPrice.HasValue && product.Price < oldPrice.Value)
                    await wishlistAlertService.SendDiscountAlertAsync(product.Id, oldPrice.Value, product.Price);
            }
            catch( DbUpdateConcurrencyException )
            {
                if( !ProductExists( product.Id ) )
                    return NotFound();
                else
                    throw;
            }

            return RedirectToAction( nameof( Index ) );
        }

        ViewData["Category"] = Enum.GetValues<Category>()
            .Select(c => new SelectListItem(c.ToString(), c.ToString(), c == product.Category)).ToList();
        return View( product );
    }
    /// <summary>
    /// GET: Products/Delete/5 - only admins can delete products
    /// </summary>
    /// <param name="id"></param>
    /// <returns>Shows the page for the product to be deleted</returns>
    [Authorize( Roles = "Admin" )]
    public IActionResult Delete(int? id)
    {
        if( id is null )
            return NotFound();

        var product = db.Products
            .FirstOrDefault( p => p.Id == id );

        if( product is null )
            return NotFound();

        return View( product );
    }
    /// <summary>
    /// POST: Products/Delete/5 - only admins can delete products
    /// </summary>
    /// <param name="id"></param>
    /// <returns>Goes back to the product list page (Index)</returns>
    [HttpPost, ActionName( "Delete" ) ]
    [Authorize( Roles = "Admin" )]
    [ValidateAntiForgeryToken]
    public IActionResult DeleteConfirmed(int id)
    {
        var product = db.Products.Find( id );

        if( product is not null )
            db.Products.Remove(product);

        db.SaveChanges();
        return RedirectToAction( nameof( Index ) );
    }
    /// <summary>
    /// Checks if a product with the given ID exists in the database
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    private bool ProductExists(int id) => db.Products.Any( p => p.Id == id );
}

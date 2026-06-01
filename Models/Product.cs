namespace DontFret.Models;
/// <summary>
/// Product class
/// Represents a item a customer can buy
/// - Id: Primary key
/// - Name: Product name
/// </summary>
public class Product
{
    public int Id { get; set; }// PK
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
    public decimal Price { get; set; }

    public string? ImageUrl { get; set; }// online or local path to product image
    public int StockQuantity { get; set; }

    public Category Category { get; set; }

    public ICollection<Review> Reviews { get; set; } = [];// could have 0 to many reviews
    public ICollection<BasketItem> BasketItems { get; set; } = [];
    public ICollection<WishlistItem> WishlistItems { get; set; } = [];
    public ICollection<OrderItem> OrderItems { get; set; } = [];
}

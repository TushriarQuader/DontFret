namespace DontFret.Models;
/// <summary>
/// WishlistItem - represents one of 0-many customer saved items
/// </summary>
public class WishlistItem
{
    public int Id { get; set; }// PK

    public int WishlistId { get; set; }
    public Wishlist Wishlist { get; set; } = null!;

    public int ProductId { get; set; }// FK to Product
    public Product Product { get; set; } = null!;

    public DateTime DateAdded { get; set; }
    // Customer can choose to be alerted if a wishlist item is in stock
    public bool StockAlerts { get; set; }
    // Customer can choose to be alerted for discounts
    public bool DiscountAlerts { get; set; }
}

namespace DontFret.Models;
/// <summary>
/// Wishlist class
/// Stores a customer's wishlist items (as a collection). Each customer has one wishlist, and a wishlist can have many items.
/// </summary>
public class Wishlist
{
    public int Id { get; set; }// PK

    public string CustomerId { get; set; } = null!;// FK to Customer
    public Customer Customer { get; set; } = null!;// Actual Customer object (navigation property)

    public ICollection<WishlistItem> Items { get; set; } = [];// A wishlist can have 0 to many items
}

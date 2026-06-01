namespace DontFret.Models;

public sealed class Customer : User
{
    public ICollection<Order> Orders { get; set; } = [];
    public ICollection<Review> Reviews { get; set; } = [];
    // Customer-specific properties
    public Basket? Basket { get; set; }
    public Wishlist Wishlist { get; set; } = new();
}

namespace DontFret.Models;
/// <summary>
/// BasketItem
/// This is a single Basket line item which has the product and quantity for a specific customer's basket.
/// </summary>
public class BasketItem
{
    public int Id { get; set; }// PK
    public int BasketId { get; set; }// FK to Basket
    public Basket Basket { get; set; } = null!;// Actual Basket object (navigation property)
    public int ProductId { get; set; }// FK to Product
    public Product Product { get; set; } = null!;// Actual Product object (navigation property)
    public int Quantity { get; set; }
}

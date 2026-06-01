namespace DontFret.Models;

public class Basket
{
    public int Id { get; set; }
    public string CustomerId { get; set; } = null!;
    public Customer Customer { get; set; } = null!;
    public ICollection<BasketItem> Items { get; set; } = [];
}

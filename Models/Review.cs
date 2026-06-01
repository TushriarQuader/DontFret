namespace DontFret.Models;
/// <summary>
/// Review class
/// stores info about a Product's review left by a Customer (who purchased the product)
/// Has an enum for the star rating (1 to 5) and optional title and body text for the review
/// </summary>
public enum StarRating
{   
    One = 1,
    Two,
    Three,
    Four,
    Five
}

public class Review
{
    public int Id { get; set; }
    public StarRating Score { get; set; }
    public string CustomerId { get; set; } = null!;
    public Customer Customer { get; set; } = null!;
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public string? Title { get; set; }
    public string? Body { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

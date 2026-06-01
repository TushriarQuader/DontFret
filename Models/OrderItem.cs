namespace DontFret.Models;

/// <summary>
/// RefundRequestStatus 
/// This enum represents the status of a refund request for an order item. 
/// It can be Pending (the request has been made but not yet reviewed), Approved (the request has been reviewed and approved), or Rejected (the request has been reviewed and rejected). 
/// This allows the application to track the state of refund requests for each order item.
/// </summary>
public enum RefundRequestStatus
{
    Pending,
    Approved,
    Rejected
}

/// <summary>
/// OrderItem
/// This is a order line which has the product, quantity, and price for a specific order. 
/// It also tracks refund requests and  status
/// </summary>

public class OrderItem
{
    public int Id { get; set; }// PK

    public int OrderId { get; set; }// FK to Order
    public Order Order { get; set; } = null!;// Actual Order object (navigation property)

    public int ProductId { get; set; }// FK to Product
    public Product Product { get; set; } = null!;// Actual Product object (navigation property)

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }
    // Stuff for refunds
    public RefundRequestStatus? RefundStatus { get; set; }
    public string? RefundReason { get; set; }
    public DateTime? RefundRequestedAt { get; set; }
    public DateTime? RefundReviewedAt { get; set; }
    public string? RefundReviewedById { get; set; }
    public User? RefundReviewedBy { get; set; }
}

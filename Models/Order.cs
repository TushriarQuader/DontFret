namespace DontFret.Models;
/// <summary>
/// OrderStatus enum
/// This tracks the current status of an order, allowing for better order management and customer communication.
/// </summary>
public enum OrderStatus
{
    None,
    Pending,
    Dispatched,
    OutForDelivery,
    Delivered,
    Cancelled,
    Returned
}
/// <summary>
/// Order model representing a customer's order, including details such as order date, status, total amount, and associated customer information. 
/// It also includes optional fields for shipping method and delivery date, as well as a collection of order items and an optional invoice.
/// </summary>
public class Order
{
    public int Id { get; set; }
    public DateTime OrderDate { get; set; }
    public OrderStatus Status { get; set; }
    public decimal TotalAmount { get; set; }

    public string CustomerId { get; set; } = null!;
    public Customer Customer { get; set; } = null!;

    public string? ShippingMethod { get; set; }
    public DateTime? DeliveryDate { get; set; }

    public ICollection<OrderItem> Items { get; set; } = [];

    public Invoice? Invoice { get; set; }
}

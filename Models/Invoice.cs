namespace DontFret.Models;
/// <summary>
/// Invoice class for storing invoice details
/// </summary>
public class Invoice
{
    public int Id { get; set; }// Primary key
    public string InvoiceNumber { get; set; } = null!;// Its string because it can contain letters and numbers, e.g., "INV-1001"
    public decimal Amount { get; set; }
    public decimal SalesTax { get; set; }
    public decimal TotalDiscount { get; set; }

    public int OrderId { get; set; }// Foreign key to Order
    public Order Order { get; set; } = null!;// Actual navigation property to Order
}

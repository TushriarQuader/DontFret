namespace DontFret.Models;
/// <summary>
/// This keeps track of customer payments through Stripe
/// </summary>
public class PaymentRecord
{
    public int Id { get; set; }// PK
    public string StripeSessionId { get; set; } = string.Empty;
    public DateTime Created { get; set; }
    public string Status { get; set; } = string.Empty;
}

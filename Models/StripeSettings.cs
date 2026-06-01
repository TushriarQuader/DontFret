namespace DontFret.Models;
/// <summary>
/// Settings for Stripe payment processing
/// </summary>
public class StripeSettings
{
    public string PublishableKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
}

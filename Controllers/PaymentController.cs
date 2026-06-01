using DontFret.Data;
using DontFret.Models;
using DontFret.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stripe.Checkout;
using System.Security.Claims;

namespace DontFret.Controllers;

// This controller provides an API endpoint that can be called via AJAX
// from the frontend to create a Stripe Checkout Session.
// The main checkout flow goes through CheckoutController.PlaceOrder instead.
public class PaymentController(StripePaymentService paymentService, ApplicationDbContext db) : ControllerBase
{
    // POST: /create-checkout-session
    // Called via AJAX from the checkout page to get a Stripe Checkout URL.
    // Uses the authenticated customer's basket items to build the session.
    [HttpPost("create-checkout-session")]
    public ActionResult CreateCheckoutSession()
    {
        // Get the currently logged-in user's ID
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        // Load the customer with their basket and products
        var customer = db.Users
            .OfType<Customer>()
            .Include( c => c.Basket )
                .ThenInclude( b => b.Items )
                    .ThenInclude( bi => bi.Product )
            .FirstOrDefault( c => c.Id == userId );

        if( customer?.Basket?.Items.Count == 0 )
            return BadRequest( new { error = "Basket is empty" } );

        // Build the line items list from the customer's basket
        var items = customer!.Basket!.Items.Select(bi =>
            (bi.Product.Name, (long)(bi.Product.Price * 100), bi.Quantity)).ToList();

        var session = paymentService.CreateCheckoutSession(
            // Dynamically generate the success URL with Stripe's {CHECKOUT_SESSION_ID} template variable
            Url.Action("PaymentSuccess", "Checkout", null, Request.Scheme) + "?session_id={CHECKOUT_SESSION_ID}",
            Url.Action( "PaymentCancel", "Checkout", null, Request.Scheme ),
            customer.Email,
            items,
            shippingAmountInPence: 1500 // £15.00
        );

        // Record the session in our database so we can track its status
        db.PaymentRecords.Add(new PaymentRecord
        {
            StripeSessionId = session.Id,
            Created = DateTime.UtcNow,
            Status = "Created"
        });

        db.SaveChanges();
        // Return the session ID and URL to the frontend so it can redirect
        return Ok(new { sessionId = session.Id, url = session.Url });
    }
}

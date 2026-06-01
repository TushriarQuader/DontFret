using DontFret.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using DontFret.Data;
using DontFret.Models;

namespace DontFret.Controllers;
/// <summary>
/// Only registered customers can purchase products.
/// </summary>
/// <param name="db"></param>
/// <param name="emailSender"></param>
/// <param name="userManager"></param>
[Authorize(Roles = "Customer")]
public class CheckoutController(ApplicationDbContext db, IEmailService emailSender, UserManager<User> userManager, IInvoiceService invoiceService) : Controller
{
    /// <summary>
    /// GET: /Checkout - Shows the checkout form with delivery details and order summary
    /// </summary>
    /// <returns></returns>
    public IActionResult Index()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var customer = db.Users
            .OfType<Customer>()
            .Include(c => c.Basket)
                .ThenInclude(b => b.Items)
                    .ThenInclude(bi => bi.Product)
            .FirstOrDefault(c => c.Id == userId);

        if( customer?.Basket?.Items.Count == 0 )
            return RedirectToAction( "Index", "Basket" );

        var items = customer?.Basket?.Items.ToList() ?? [];
        var subtotal = items.Sum( i => i.Product.Price * i.Quantity);
        var shipping = 15.00m;
        var total = subtotal + shipping;
        // Need to put this in ViewData since the checkout form is outside of the Order model (we don't want to create a separate view model just for this)
        ViewData["Subtotal"] = subtotal;
        ViewData["Shipping"] = shipping;
        ViewData["Total"] = total;
        ViewData["Address"] = customer?.Address;
        ViewData["Postcode"] = customer?.Postcode;

        return View( items );
    }
    /// <summary>
    /// POST: /Checkout/PlaceOrder
    /// 1. Saves the delivery address
    /// 2. Creates a Stripe Checkout Session with the customer's basket items
    /// 3. Records a PaymentRecord in the database (status: "Created")
    /// 4. Redirects the customer to Stripe's hosted payment page
    /// </summary>
    /// <param name="deliveryAddress"></param>
    /// <param name="deliveryPostcode"></param>
    /// <param name="courier"></param>
    /// <returns></returns>
    [HttpPost]
    public IActionResult PlaceOrder(string deliveryAddress, string deliveryPostcode, string courier)
    {
        var userId = User.FindFirstValue( ClaimTypes.NameIdentifier );
        var customer = db.Users
            .OfType<Customer>()
            .Include( c => c.Basket )
            .ThenInclude( b => b.Items )
            .ThenInclude( bi => bi.Product )
            .FirstOrDefault( c => c.Id == userId );

        if( customer?.Basket?.Items.Count == 0 )
            return RedirectToAction( "Index", "Basket" );
        // Save delivery details to the customer's profile (so it's pre-filled next time)
        customer!.Address = deliveryAddress;
        customer.Postcode = deliveryPostcode;
        db.SaveChanges();
        // Stock validation before initiating payment
        foreach( var bi in customer.Basket.Items )
        {
            if( bi.Quantity > bi.Product.StockQuantity )
            {
                TempData["ErrorMessage"] = $"Sorry, '{bi.Product.Name}' only has {bi.Product.StockQuantity} in stock, but you have {bi.Quantity} in your basket.";
                return RedirectToAction( "Index", "Basket" );
            }
        }
        // Build the basket items into Stripe's expected format:
        // (product name, price in pence, quantity)
        var items = customer.Basket.Items.Select(bi =>
            (bi.Product.Name, (long)(bi.Product.Price * 100), bi.Quantity)).ToList();
        // Resolve StripePaymentService from DI since it's not injected in this controller
        var paymentService = HttpContext.RequestServices
            .GetRequiredService<StripePaymentService>();
        // Create the Stripe Checkout Session
        // The {CHECKOUT_SESSION_ID} template variable is automatically replaced by Stripe
        var session = paymentService.CreateCheckoutSession(
            Url.Action("PaymentSuccess", "Checkout", null, Request.Scheme) + "?session_id={CHECKOUT_SESSION_ID}",
            Url.Action( "PaymentCancel", "Checkout", null, Request.Scheme ),
            customer.Email,
            items,
            shippingAmountInPence: 1500);
        // Log the Stripe session in our PaymentRecords table for tracking
        db.PaymentRecords.Add(new PaymentRecord
        {
            StripeSessionId = session.Id,
            Created = DateTime.UtcNow,
            Status = "Created"
        });

        db.SaveChanges();
        // Redirect the customer to the Stripe hosted checkout page
        return Redirect(session.Url);
    }
    /// <summary>
    /// Called by Stripe after a successful payment.
    /// </summary>
    /// <param name="session_id"></param>
    /// <returns></returns>
    public async Task<IActionResult> PaymentSuccess(string session_id)
    {
        // Update the PaymentRecord status to "Success"
        var paymentRecord = db.PaymentRecords
            .FirstOrDefault(p => p.StripeSessionId == session_id);
        if( paymentRecord != null )
        {
            paymentRecord.Status = "Success";
            db.SaveChanges();
        }

        // Re-load the customer with their basket items
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var customer = db.Users
            .OfType<Customer>()
            .Include(c => c.Basket)
                .ThenInclude(b => b.Items)
                    .ThenInclude(bi => bi.Product)
            .FirstOrDefault( c => c.Id == userId );

        if( customer?.Basket?.Items.Count == 0 )
            return RedirectToAction( "Index", "Basket" );

        // Re-check stock sufficiency (may have changed since Stripe session was created)
        foreach( var item in customer.Basket.Items )
        {
            var product = db.Products.Find( item.ProductId );
            if( product is null || item.Quantity > product.StockQuantity )
            {
                TempData["ErrorMessage"] = $"Sorry, '{item.Product.Name}' is no longer available in the requested quantity. Only {product?.StockQuantity ?? 0} in stock.";
                return RedirectToAction( "Index", "Basket" );
            }
        }

        // --- Create the Order ---
        var order = new Order
        {
            CustomerId = userId,
            OrderDate = DateTime.UtcNow,
            Status = OrderStatus.Pending,
            Items = customer.Basket.Items.Select(bi => new OrderItem
            {
                ProductId = bi.ProductId,
                Quantity = bi.Quantity,
                UnitPrice = bi.Product.Price
            }).ToList()
        };

        order.TotalAmount = order.Items.Sum( i => i.UnitPrice * i.Quantity );
        db.Orders.Add( order );
        // Decrement stock quantities for each purchased item
        foreach( var item in customer.Basket.Items )
        {
            var product = db.Products.Find( item.ProductId );

            if( product != null )
                product.StockQuantity -= item.Quantity;
        }
        // Generate an invoice for the order
        db.Invoices.Add(new Invoice
        {
            Order = order,
            InvoiceNumber = "INV-" + DateTime.UtcNow.Ticks,
            Amount = order.TotalAmount
        });
        // Clear the basket now that the order has been placed
        db.BasketItems.RemoveRange(customer.Basket.Items);
        db.SaveChanges();
        // Send order confirmation email with invoice PDF attached
        try
        {
            var pdfBytes = invoiceService.GenerateInvoice(order.Id);
            await emailSender.SendEmailWithAttachmentAsync(
                customer.Email!,
                $"Order Confirmation - #{order.Id}",
                $"<p>Thanks for your order!</p><p>Your order <strong>#{order.Id}</strong> has been confirmed and is being processed.</p><p>Your invoice is attached.</p>",
                pdfBytes,
                $"Invoice_{order.Invoice?.InvoiceNumber ?? order.Id.ToString()}.pdf");
        }
        catch
        {
            // Email failed - do nothing I guess?
            // The order is still created so the customer can still see the invoice
        }

        // Show the order confirmation page
        return RedirectToAction( nameof( Confirmation ), new { orderId = order.Id });
    }
    /// <summary>
    /// GET: /Checkout/PaymentCancel
    /// Called by Stripe if the customer cancels or the payment fails.
    /// Shows a simple cancellation page with a link back to checkout.
    /// </summary>
    /// <returns>Shows the cancellation page</returns>
    public IActionResult PaymentCancel()
    {
        return View();
    }
    /// <summary>
    /// /Checkout/Confirmation/5 - Shows the order confirmation after a successful purchase
    /// </summary>
    /// <param name="orderId"></param>
    /// <returns></returns>
    public IActionResult Confirmation(int orderId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var order = db.Orders
            .Include( o => o.Items )
                .ThenInclude( oi => oi.Product )
            .Include( o => o.Customer )
            .Include( o => o.Invoice )
            .FirstOrDefault( o => o.Id == orderId && o.CustomerId == userId );

        if( order is null )
            return NotFound();

        return View( order );
    }
}

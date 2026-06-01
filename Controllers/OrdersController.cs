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
/// Controller for managing orders, including viewing order history, order details, managing orders (for clerks), generating invoices, and handling refund requests.
/// </summary>
/// <param name="db"></param>
/// <param name="emailSender"></param>
/// <param name="userManager"></param>
/// <param name="invoiceService"></param>
public class OrdersController(ApplicationDbContext db, IEmailService emailSender, UserManager<User> userManager, IInvoiceService invoiceService) : Controller
{
    /// <summary>
    /// Shows the order history for the currently logged in customer. 
    /// Admins and clerks can also see this, but it will only show their own orders.
    /// </summary>
    /// <returns></returns>
    [Authorize( Roles = "Customer" )]
    public IActionResult Index()
    {
        var userId = User.FindFirstValue( ClaimTypes.NameIdentifier );
        var orders = db.Orders
            .Where( o => o.CustomerId == userId )
            .Include( o => o.Items )
            .ThenInclude( oi => oi.Product )
            .Include( o => o.Invoice )
            .OrderByDescending( o => o.OrderDate )
        .ToList();

        return View( orders );
    }
    /// <summary>
    /// Shows the details of a specific order, including the items, their status, and the invoice if it exists.
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    [Authorize( Roles = "Customer" )]
    public IActionResult Details(int id)
    {
        var userId = User.FindFirstValue( ClaimTypes.NameIdentifier );
        var order = db.Orders
            .Include( o => o.Items )
            .ThenInclude( oi => oi.Product )
            .Include( o => o.Customer )
            .Include( o => o.Invoice )
        .FirstOrDefault( o => o.Id == id && o.CustomerId == userId );

        if ( order is null )
            return NotFound();

        return View( order );
    }
    /// <summary>
    /// Shows a list of all orders for clerks and admins, with optional filtering by order status.
    /// This allows them to manage and update orders as needed.
    /// </summary>
    /// <param name="status"></param>
    /// <returns></returns>
    [Authorize( Roles = "Admin,OfficeClerk" )]
    public IActionResult Manage(OrderStatus? status = null)
    {
        var orders = db.Orders
            .Include( o => o.Items )
            .ThenInclude( oi => oi.Product )
            .Include( o => o.Customer )
            .OrderByDescending( o => o.OrderDate )
            .AsQueryable();

        if( status.HasValue )
            orders = orders.Where( o => o.Status == status );

        return View( orders.ToList() );
    }
    /// <summary>
    /// Creates a PDF invoice for the specified order. Customers can only generate invoices for their own orders, while clerks and admins can generate invoices for any order. 
    /// If the order does not have an invoice yet, it will be created automatically. If the order is not eligible for invoicing (e.g. cancelled), an error message will be shown instead.
    /// </summary>
    /// <param name="orderId"></param>
    /// <returns></returns>
    [Authorize(Roles = "Customer,Admin,OfficeClerk")]
    public async Task<IActionResult> GenerateInvoice(int orderId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isAdmin = User.IsInRole("Admin") || User.IsInRole("OfficeClerk");

        var order = db.Orders
            .Include( o => o.Customer )
        .FirstOrDefault( o => o.Id == orderId );

        if( order is null )
            return NotFound();
        // Cant let randomers generate invoices for other people's orders, but clerks and admins can generate invoices for any order
        if ( !isAdmin && order.CustomerId != userId )
            return Forbid();

        try
        {
            var pdfBytes = invoiceService.GenerateInvoice( orderId );
            return File(pdfBytes, "application/pdf", $"Invoice_{order.Invoice?.InvoiceNumber ?? orderId.ToString()}.pdf");
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
            return RedirectToAction(nameof(Details), new { id = orderId });
        }
    }
    /// <summary>
    /// Updates the status of an order. This is only accessible to clerks and admins. 
    /// When the status is updated, an email notification is sent to the customer informing them of the change.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="status"></param>
    /// <returns></returns>
    [HttpPost]
    [Authorize( Roles = "Admin,OfficeClerk" )]
    public async Task<IActionResult> UpdateStatus(int id, OrderStatus status)
    {
        var order = db.Orders.Find( id );

        if( order is null )
            return NotFound();

        order.Status = status;
        db.SaveChanges();

        var user = await userManager.FindByIdAsync(order.CustomerId);
        if( user is not null )
        {
            await emailSender.SendEmailAsync(user.Email!, $"Order #{order.Id} Status Updated",
                $"<p>Your order <strong>#{order.Id}</strong> status has been updated to: <strong>{status}</strong>.</p>");
        }

        return RedirectToAction( nameof( Manage ) );
    }
    /// <summary>
    /// Shows the refund request form for a specific order item.
    /// Customers can only request refunds for their own orders, and only if there is no existing refund request for that item and the order is not cancelled or returned.
    /// </summary>
    /// <param name="orderItemId"></param>
    /// <returns></returns>
    [Authorize( Roles = "Customer" )]
    public IActionResult RequestRefund(int orderItemId)
    {
        var userId = User.FindFirstValue( ClaimTypes.NameIdentifier );
        var item = db.OrderItems
            .Include( oi => oi.Product )
            .Include( oi => oi.Order )
            .FirstOrDefault( oi => oi.Id == orderItemId && oi.Order.CustomerId == userId );

        if( item is null )
            return NotFound();
        // Customer cant request a refund if there is already a refund request for this item, or if the order is cancelled or returned
        if ( item.RefundStatus is not null )
        {
            TempData["ErrorMessage"] = "A refund request already exists for this item.";
            return RedirectToAction( nameof( Details ), new { id = item.OrderId } );
        }

        if( item.Order.Status is OrderStatus.Cancelled or OrderStatus.Returned )
        {
            TempData["ErrorMessage"] = "Cannot request a refund for a cancelled or returned order.";
            return RedirectToAction( nameof( Details ), new { id = item.OrderId } );
        }

        return View( item );
    }
    /// <summary>
    /// Handles the submission of a refund request for a specific order item.
    /// </summary>
    /// <param name="orderItemId"></param>
    /// <param name="reason"></param>
    /// <returns></returns>
    [HttpPost]
    [Authorize( Roles = "Customer" )]
    public IActionResult RequestRefund(int orderItemId, string reason)
    {
        var userId = User.FindFirstValue( ClaimTypes.NameIdentifier );
        var item = db.OrderItems
            .Include( oi => oi.Order )
            .FirstOrDefault( oi => oi.Id == orderItemId && oi.Order.CustomerId == userId );

        if( item is null )
            return NotFound();
        // Customer cant request a refund if there is already a refund request for this item, or if the order is cancelled or returned
        if ( item.RefundStatus is not null )
        {
            TempData["ErrorMessage"] = "A refund request already exists for this item.";
            return RedirectToAction( nameof( Details ), new { id = item.OrderId } );
        }

        if( string.IsNullOrWhiteSpace( reason ) )
        {
            TempData["ErrorMessage"] = "Please provide a reason for the refund.";
            return RedirectToAction( nameof( RequestRefund ), new { orderItemId } );
        }

        item.RefundStatus = RefundRequestStatus.Pending;
        item.RefundReason = reason;
        item.RefundRequestedAt = DateTime.UtcNow;
        db.SaveChanges();

        TempData["SuccessMessage"] = "Refund request submitted successfully.";
        return RedirectToAction( nameof( Details ), new { id = item.OrderId } );
    }

    /// <summary>
    /// Shows a list of all refund requests for clerks and admins, with optional filtering by refund status.
    /// </summary>
    /// <param name="status"></param>
    /// <returns></returns>
    [Authorize( Roles = "Admin,OfficeClerk" )]
    public IActionResult RefundRequests(RefundRequestStatus? status = null)
    {
        var items = db.OrderItems
            .Include( oi => oi.Product )
            .Include( oi => oi.Order ).ThenInclude( o => o.Customer )
            .Where( oi => oi.RefundStatus != null )
            .AsQueryable();

        if( status.HasValue )
            items = items.Where( oi => oi.RefundStatus == status );

        return View( items.OrderByDescending( oi => oi.RefundRequestedAt ).ToList() );
    }
    /// <summary>
    /// Handles the acception for a specific order item. 
    /// This is only accessible to clerks and admins.
    /// </summary>
    /// <param name="orderItemId"></param>
    /// <returns></returns>
    [HttpPost]
    [Authorize( Roles = "Admin,OfficeClerk" )]
    public async Task<IActionResult> ApproveRefund(int orderItemId)
    {
        var clerkId = User.FindFirstValue( ClaimTypes.NameIdentifier );
        var item = db.OrderItems
            .Include( oi => oi.Product )
            .Include( oi => oi.Order )
            .FirstOrDefault( oi => oi.Id == orderItemId );

        if( item is null )
            return NotFound();

        if( item.RefundStatus != RefundRequestStatus.Pending )
        {
            TempData["ErrorMessage"] = "This refund request is not pending.";
            return RedirectToAction( nameof( RefundRequests ) );
        }

        item.RefundStatus = RefundRequestStatus.Approved;
        item.RefundReviewedAt = DateTime.UtcNow;
        item.RefundReviewedById = clerkId;

        var product = db.Products.Find( item.ProductId );
        if( product is not null )
            product.StockQuantity += item.Quantity;

        db.SaveChanges();

        var user = await userManager.FindByIdAsync( item.Order.CustomerId );
        if( user is not null )
        {
            await emailSender.SendEmailAsync( user.Email!, $"Refund Approved - Order #{item.OrderId}",
                $"<p>Your refund request for <strong>{item.Product.Name}</strong> in order <strong>#{item.OrderId}</strong> has been approved.</p>" +
                $"<p>You can now print your return label from your order details page.</p>" );
        }

        TempData["SuccessMessage"] = "Refund approved. Stock has been restored and the customer has been notified.";
        return RedirectToAction( nameof( RefundRequests ) );
    }
    /// <summary>
    /// Handles the rejection of a refund request for a specific order item.
    /// </summary>
    /// <param name="orderItemId"></param>
    /// <returns></returns>
    [HttpPost]
    [Authorize( Roles = "Admin,OfficeClerk" )]
    public async Task<IActionResult> RejectRefund(int orderItemId)
    {
        var clerkId = User.FindFirstValue( ClaimTypes.NameIdentifier );
        var item = db.OrderItems
            .Include( oi => oi.Product )
            .Include( oi => oi.Order )
            .FirstOrDefault( oi => oi.Id == orderItemId );

        if( item is null )
            return NotFound();

        if( item.RefundStatus != RefundRequestStatus.Pending )
        {
            TempData["ErrorMessage"] = "This refund request is not pending.";
            return RedirectToAction( nameof( RefundRequests ) );
        }

        item.RefundStatus = RefundRequestStatus.Rejected;
        item.RefundReviewedAt = DateTime.UtcNow;
        item.RefundReviewedById = clerkId;
        db.SaveChanges();

        var user = await userManager.FindByIdAsync( item.Order.CustomerId );
        if( user is not null )
        {
            await emailSender.SendEmailAsync( user.Email!, $"Refund Request Rejected - Order #{item.OrderId}",
                $"<p>Your refund request for <strong>{item.Product.Name}</strong> in order <strong>#{item.OrderId}</strong> has been rejected.</p>" +
                $"<p>If you have any questions, please contact our support team.</p>" );
        }

        TempData["SuccessMessage"] = "Refund rejected. The customer has been notified.";
        return RedirectToAction( nameof( RefundRequests ) );
    }
    /// <summary>
    /// Generates a PDF return label for a specific order item that has an approved refund request.
    /// This uses the InvoiceService to generate the return label, which includes a unique return authorization number and a QR code for tracking the return.
    /// </summary>
    /// <param name="orderItemId"></param>
    /// <returns></returns>
    [Authorize( Roles = "Customer" )]
    public IActionResult ReturnLabel(int orderItemId)
    {
        var userId = User.FindFirstValue( ClaimTypes.NameIdentifier );
        var item = db.OrderItems
            .Include( oi => oi.Order )
            .FirstOrDefault( oi => oi.Id == orderItemId && oi.Order.CustomerId == userId );

        if( item is null )
            return NotFound();

        if( item.RefundStatus != RefundRequestStatus.Approved )
        {
            TempData["ErrorMessage"] = "Return label is only available for approved refunds.";
            return RedirectToAction( nameof( Details ), new { id = item.OrderId } );
        }

        try
        {
            var pdfBytes = invoiceService.GenerateReturnLabel( orderItemId );
            return File( pdfBytes, "application/pdf", $"ReturnLabel_Order{item.OrderId}_Item{item.Id}.pdf" );
        }
        catch( InvalidOperationException ex )
        {
            TempData["ErrorMessage"] = ex.Message;
            return RedirectToAction( nameof( Details ), new { id = item.OrderId } );
        }
    }
}

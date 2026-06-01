using DontFret.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using static System.Net.WebRequestMethods;

namespace DontFret.Data;
/// <summary>
/// Entity Framework Core DbContext that derives from IdentityDbContext<User> and exposes DbSet properties for the
/// application's domain entities (products, orders, invoices, reviews, baskets, wishlists, and related items).
/// </summary>
/// <remarks>Configures the model with the Fluent API in OnModelCreating. Adds a discriminator for the User
/// hierarchy (Admin, OfficeClerk, Customer); defines one-to-one relationships (Order–Invoice, Customer–Basket,
/// Customer–Wishlist); enforces unique composite indexes for BasketItem, WishlistItem, OrderItem, and Review; sets
/// decimal precision for monetary properties; converts Product.Category to string. Calls base.OnModelCreating to apply
/// Identity mappings.</remarks>
/// <param name="options">The DbContextOptions<ApplicationDbContext> used to configure the context (database provider, connection string, and
/// other EF Core options).</param>
public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<User>( options )
{
    public DbSet<Product> Products { get; set; }// Set of all products in the store
    public DbSet<Order> Orders { get; set; }// Set of all customer orders
    public DbSet<Invoice> Invoices { get; set; }// Set of all invoices generated for orders
    public DbSet<Review> Reviews { get; set; }// Set of all product reviews submitted by customers
    public DbSet<Basket> Baskets { get; set; }// Set of all shopping baskets
    public DbSet<BasketItem> BasketItems { get; set; }// Set of all items in shopping baskets
    public DbSet<Wishlist> Wishlists { get; set; }// Set of all wishlists
    public DbSet<WishlistItem> WishlistItems { get; set; }// Set of all items in wishlists
    public DbSet<OrderItem> OrderItems { get; set; }// Set of all items in customer orders
    public DbSet<PaymentRecord> PaymentRecords { get; set; }
    public DbSet<EmailVerificationCode> EmailVerificationCodes { get; set; }
    /// <summary>
    /// Data annotations are confusing for relationships, so I'm using the Fluent API in OnModelCreating instead.
    /// </summary>
    /// <param name="modelBuilder"></param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {   // The new way of configuring relationships using the Fluent API in Entity Framework Core
        base.OnModelCreating( modelBuilder );

        modelBuilder.Entity<User>()// First put in Admin, OfficeClerk and Customer as users in the DB
            .HasDiscriminator<string>( "Discriminator" )
            .HasValue<Admin>( "Admin" )
            .HasValue<OfficeClerk>( "OfficeClerk" )
            .HasValue<Customer>( "Customer" );

        modelBuilder.Entity<Order>() // 1 Order, 1 Invoice
            .HasOne( o => o.Invoice )
            .WithOne( i => i.Order )
            .HasForeignKey<Invoice>( i => i.OrderId );

        modelBuilder.Entity<Customer>()// 1 Customer, 1 Basket
            .HasOne( c => c.Basket )
            .WithOne( b => b.Customer )
            .HasForeignKey<Basket>( b => b.CustomerId );

        modelBuilder.Entity<Customer>()// 1 Customer, 1 Wishlist
            .HasOne( c => c.Wishlist )
            .WithOne( w => w.Customer )
            .HasForeignKey<Wishlist>( w => w.CustomerId );

        modelBuilder.Entity<BasketItem>()// A Basket can only have one entry for each Product
            .HasIndex( bi => new { bi.BasketId, bi.ProductId } )
            .IsUnique();

        modelBuilder.Entity<WishlistItem>()// A Wishlist can only have one entry for each Product
            .HasIndex( wi => new { wi.WishlistId, wi.ProductId } )
            .IsUnique();

        modelBuilder.Entity<OrderItem>()// An Order can only have one entry for each Product
            .HasIndex( oi => new { oi.OrderId, oi.ProductId } )
            .IsUnique();

        modelBuilder.Entity<OrderItem>()
            .HasOne( oi => oi.RefundReviewedBy )
            .WithMany()
            .HasForeignKey( oi => oi.RefundReviewedById )
            .OnDelete( DeleteBehavior.NoAction );

        modelBuilder.Entity<Review>()// A Customer can only review a Product once (if they purchased it)
            .HasIndex( r => new { r.CustomerId, r.ProductId } )
            .IsUnique();

        modelBuilder.Entity<EmailVerificationCode>()
            .HasIndex( evc => evc.UserId );

        modelBuilder.Entity<EmailVerificationCode>()
            .HasOne( evc => evc.User )
            .WithMany()
            .HasForeignKey( evc => evc.UserId )
            .OnDelete( DeleteBehavior.Cascade );
        // Configure decimal precision for monetary values
        modelBuilder.Entity<Product>()
            .Property( p => p.Price )
            .HasColumnType( "decimal(18,2)" );

        modelBuilder.Entity<Order>()
            .Property( o => o.TotalAmount )
            .HasColumnType( "decimal(18,2)" );

        modelBuilder.Entity<OrderItem>()
            .Property( oi => oi.UnitPrice )
            .HasColumnType( "decimal(18,2)" );

        modelBuilder.Entity<Invoice>()
            .Property( i => i.Amount )
            .HasColumnType( "decimal(18,2)" );

        modelBuilder.Entity<Invoice>()
            .Property( i => i.SalesTax )
            .HasColumnType( "decimal(18,2)" );

        modelBuilder.Entity<Invoice>()
            .Property( i => i.TotalDiscount )
            .HasColumnType( "decimal(18,2)" );

        modelBuilder.Entity<Product>()
            .Property( p => p.Category )
            .HasConversion<string>();
    }
}
/// <summary>
/// Seeds the application's database with initial data including roles, administrative and customer accounts, sample
/// products, orders, invoices, and product reviews.
/// </summary>
public static class DbInitialiser
{
    /// <summary>
    /// Seeds the database with initial data. Creates roles (Admin, OfficeClerk, Customer) and default users for each role with credentials from configuration 
    /// (or defaults if not provided). Adds sample products across categories (Guitars, Amplifiers, Accessories).
    /// Creates sample orders for customers with associated invoices. Adds product reviews from customers. 
    /// Uses the application's DbContext and Identity services to perform these operations. Designed to be idempotent, checking for existing data before seeding to avoid duplicates.
    /// </summary>
    /// <param name="services"></param>
    public static void Seed(IServiceProvider services)
    {
        var userManager = services.GetRequiredService<UserManager<User>>();// use project User type
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var config = services.GetRequiredService<IConfiguration>();
        // Admin
        var adminUserName = config["IdentitySeed:AdminUserName"] ?? "admin";
        var adminEmail = config["IdentitySeed:AdminEmail"] ?? "admin@email.com";
        var adminPassword = config["IdentitySeed:AdminPassword"] ?? "Admin@123";

        if( !roleManager.RoleExistsAsync( Roles.Admin.ToString() ).Result )
            roleManager.CreateAsync( new IdentityRole( Roles.Admin.ToString() ) ).Wait();

        if( userManager.FindByEmailAsync( adminEmail ).Result == null )
        {
            var admin = new Admin
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true
            };

            var result = userManager.CreateAsync( admin, adminPassword ).Result;
            if( result.Succeeded )
                userManager.AddToRoleAsync( admin, Roles.Admin.ToString() ).Wait();
        }
        // OfficeClerk
        var clerkEmail = config["IdentitySeed:OfficeClerkEmail"] ?? "clerk@email.com";
        var clerkPassword = config["IdentitySeed:OfficeClerkPassword"] ?? "Clerk@123";

        if( !roleManager.RoleExistsAsync( Roles.OfficeClerk.ToString() ).Result )
            roleManager.CreateAsync( new IdentityRole( Roles.OfficeClerk.ToString() ) ).Wait();

        if( userManager.FindByEmailAsync( clerkEmail ).Result == null )
        {
            var clerk = new OfficeClerk
            {
                UserName = clerkEmail,
                Email = clerkEmail,
                EmailConfirmed = true
            };

            var result = userManager.CreateAsync( clerk, clerkPassword ).Result;
            if( result.Succeeded )
                userManager.AddToRoleAsync( clerk, Roles.OfficeClerk.ToString() ).Wait();
        }
        // Customer
        var _ = config["IdentitySeed:CustomerUserName"] ?? "customer";// Don't need username right now
        var customerEmail = config["IdentitySeed:CustomerEmail"] ?? "customer@email.com";
        var customerPassword = config["IdentitySeed:CustomerPassword"] ?? "Customer@123";

        if( !roleManager.RoleExistsAsync( Roles.Customer.ToString() ).Result )
            roleManager.CreateAsync( new IdentityRole( Roles.Customer.ToString() ) ).Wait();

        if( userManager.FindByEmailAsync( customerEmail ).Result == null )
        {
            var customer = new Customer
            {
                UserName = customerEmail,
                Email = customerEmail,
                EmailConfirmed = true,
                Basket = new Basket(),
                Wishlist = new Wishlist()
            };

            var result = userManager.CreateAsync( customer, customerPassword ).Result;
            if( result.Succeeded )
                userManager.AddToRoleAsync( customer, Roles.Customer.ToString() ).Wait();
        }
        // Products
        var context = services.GetRequiredService<ApplicationDbContext>();
        var guitarProduct = context.Products.FirstOrDefault( p => p.Name == "Guitar" );

        if( !context.Products.Any() )
        {
            var imageRoot = "/images/products/";// "wwwroot/images/products/" is the actual path
            var products = new List<Product>
            {
                // Guitars
                new() { Name = "Guitar", Description = "Standard 8-string electric guitar", Price = 199.99m, StockQuantity = 10, Category = Category.Guitars, ImageUrl = $"{imageRoot}guitar.webp" },
                new() { Name = "Bass", Description = "Back to bass-ics", Price = 199.99m, StockQuantity = 10, Category = Category.Guitars, ImageUrl = $"{imageRoot}bass.jpg" },
                new() { Name = "Banjo", Description = "Hillbilly's will love it", Price = 149.99m, StockQuantity = 7, Category = Category.Guitars, ImageUrl = $"{imageRoot}banjo.jpg" },
                new() { Name = "Ukelele", Description = "A small string instrument", Price = 89.99m, StockQuantity = 15, Category = Category.Guitars, ImageUrl = $"{imageRoot}ukelele.jpg" },
                new() { Name = "Pedal Guitar", Description = "A guitar with built-in effects", Price = 299.99m, StockQuantity = 5, Category = Category.Guitars, ImageUrl = $"{imageRoot}pedalguitar.jpg" },
                new() { Name = "Acoustic Guitar", Description = "Classic acoustic with warm tone", Price = 249.99m, StockQuantity = 8, Category = Category.Guitars, ImageUrl = $"{imageRoot}acoustic.jpg" },
                new() { Name = "Sitar", Description = "Rich, warm sound with full harmonics", Price = 349.99m, StockQuantity = 3, Category = Category.Guitars, ImageUrl = $"{imageRoot}sitar.jpg" },
                new() { Name = "Lute", Description = "Melodic and historical string instrument", Price = 399.99m, StockQuantity = 4, Category = Category.Guitars, ImageUrl = $"{imageRoot}lute.jpg" },
                // Amplifiers
                new() { Name = "Amplifier", Description = "An amplifier", Price = 499.99m, StockQuantity = 5, Category = Category.Amplifiers, ImageUrl = "/images/products/amplifier.jpg" },
                // Accessories (reclassified from Electronics)
                new() { Name = "Keyboard Synthesizer", Description = "Professional synth with 61 keys", Price = 599.99m, StockQuantity = 4, Category = Category.Accessories, ImageUrl = "/images/products/keyboard.jpg" },
                new() { Name = "Guitar Tuner", Description = "Digital clip-on tuner", Price = 24.99m, StockQuantity = 20, Category = Category.Accessories, ImageUrl = "/images/products/tuner.jpg" },
                // Accessories
                new() { Name = "Pick", Description = "Guitar pick", Price = 2.99m, StockQuantity = 30, Category = Category.Accessories, ImageUrl = "https://fbi.cults3d.com/uploaders/27944275/illustration-file/fea79099-2156-4882-a46e-11ad80189824/guitar_pick.JPG" },
                new() { Name = "Guitar Strap", Description = "Comfortable adjustable strap", Price = 19.99m, StockQuantity = 25, Category = Category.Accessories, ImageUrl = "https://external-content.duckduckgo.com/iu/?u=https%3A%2F%2Fthumbs.dreamstime.com%2Fb%2Fguitar-strap-isolated-white-40257241.jpg&f=1&nofb=1&ipt=15f86754711344673c59e791b815df2225c70604edb42cc0189716e5f6a83b8e" },
                new() { Name = "Guitar Case", Description = "Hard shell case for protection", Price = 89.99m, StockQuantity = 10, Category = Category.Accessories, ImageUrl = "https://external-content.duckduckgo.com/iu/?u=https%3A%2F%2Fsc1.musik-produktiv.com%2Fpic-010018327xl%2Fskb-18-acoustic-dreadnought-deluxe-guitar-case.jpg&f=1&nofb=1&ipt=f784048ded89c8920f24ea37a1ca86e12160e63b4671edba3d2c4d89e8330b8a" },
                new() { Name = "Guitar Cable 10ft", Description = "High-quality instrument cable", Price = 14.99m, StockQuantity = 18, Category = Category.Accessories, ImageUrl = "https://external-content.duckduckgo.com/iu/?u=https%3A%2F%2Fi5.walmartimages.com%2Fseo%2FGuitar-Cable-6ft-1-8m-Instrument-Cable-AMP-Cord-6-35mm-1-4-TRS-Stereo-Audio-Straight-to-Straight-Electric-Guitar-Bass-Mandolin-Keyboards-Amps-Mixers_ecead347-4807-4799-9d45-fe03d6b735bd.a659a5e955713988c65345782dbabee0.jpeg&f=1&nofb=1&ipt=9c6673aa27983b3925a7867e9a0046043548e58bfcaf16e6b83a55941b1d2425" },
                new() { Name = "Metronome", Description = "Digital metronome with tempo range", Price = 29.99m, StockQuantity = 12, Category = Category.Accessories, ImageUrl = "https://external-content.duckduckgo.com/iu/?u=https%3A%2F%2Fthumbs.dreamstime.com%2Fb%2Fmetronome-8098941.jpg&f=1&nofb=1&ipt=1357a8261a1226d3b7bb4b46734bf0ecf87c3c1bc1e2ae4e00cd71ebccc2993e" },
                new() { Name = "Capo", Description = "Guitar clamp", Price = 16.99m, StockQuantity = 15, Category = Category.Accessories, ImageUrl = "/images/products/clamp.jpg" }
            };

            context.Products.AddRange(products);
            context.SaveChanges();
        }

        // Additional customers
        var jimiEmail = "jimi@email.com";
        if( userManager.FindByEmailAsync( jimiEmail ).Result == null )
        {
            var jimi = new Customer
            {
                UserName = jimiEmail,
                Email = jimiEmail,
                EmailConfirmed = true,
                Address = "10 Electric Avenue, London",
                Postcode = "SW9 1JJ",
                Basket = new Basket(),
                Wishlist = new Wishlist()
            };

            var result = userManager.CreateAsync( jimi, "Customer@123" ).Result;
            if( result.Succeeded )
                userManager.AddToRoleAsync( jimi, Roles.Customer.ToString() ).Wait();
        }

        var sarahEmail = "sarah@email.com";
        if( userManager.FindByEmailAsync( sarahEmail ).Result == null )
        {
            var sarah = new Customer
            {
                UserName = sarahEmail,
                Email = sarahEmail,
                EmailConfirmed = true,
                Address = "42 Music Street, Manchester",
                Postcode = "M1 2WD",
                Basket = new Basket(),
                Wishlist = new Wishlist()
            };

            var result = userManager.CreateAsync( sarah, "Customer@123" ).Result;
            if( result.Succeeded )
                userManager.AddToRoleAsync( sarah, Roles.Customer.ToString() ).Wait();
        }
        // Seed sample orders
        if( !context.Orders.Any() )
        {
            Customer?
                cust1 = context.Users.OfType<Customer>().FirstOrDefault( c => c.Email == customerEmail ),
                cust2 = context.Users.OfType<Customer>().FirstOrDefault( c => c.Email == jimiEmail ),
                cust3 = context.Users.OfType<Customer>().FirstOrDefault( c => c.Email == sarahEmail );

            Product?
                guitar = context.Products.FirstOrDefault( p => p.Name == "Guitar" ),
                bass = context.Products.FirstOrDefault( p => p.Name == "Bass" ),
                amp = context.Products.FirstOrDefault( p => p.Name == "Amplifier" ),
                ukelele = context.Products.FirstOrDefault( p => p.Name == "Ukelele" ),
                pedal = context.Products.FirstOrDefault( p => p.Name == "Pedal Guitar" ),
                strap = context.Products.FirstOrDefault( p => p.Name == "Guitar Strap" ),
                pick = context.Products.FirstOrDefault( p => p.Name == "Pick" ),
                cable = context.Products.FirstOrDefault(p => p.Name == "Guitar Cable 10ft"),
                caseProduct = context.Products.FirstOrDefault(p => p.Name == "Guitar Case");

            if( cust1 is not null && guitar is not null && pick is not null )
            {
                var order1 = new Order
                {
                    CustomerId = cust1.Id,
                    OrderDate = DateTime.UtcNow.AddDays( -30 ),
                    Status = OrderStatus.Delivered,
                    Items =
                   [
                        new() { ProductId = guitar.Id, Quantity = 1, UnitPrice = guitar.Price },
                        new() { ProductId = pick.Id, Quantity = 2, UnitPrice = pick.Price }
                    ]
                };
                order1.TotalAmount = order1.Items.Sum( i => i.UnitPrice * i.Quantity );
                context.Orders.Add( order1 );

                var invoice1 = new Invoice
                {
                    Order = order1,
                    InvoiceNumber = "INV-001",
                    Amount = order1.TotalAmount
                };
                context.Invoices.Add( invoice1 );
            }

            if( cust2 is not null && amp is not null && cable is not null )
            {
                var order2 = new Order
                {
                    CustomerId = cust2.Id,
                    OrderDate = DateTime.UtcNow.AddDays(-15),
                    Status = OrderStatus.OutForDelivery,
                    Items =
                    [
                        new() { ProductId = amp.Id, Quantity = 1, UnitPrice = amp.Price },
                        new() { ProductId = cable.Id, Quantity = 1, UnitPrice = cable.Price }
                    ]
                };

                order2.TotalAmount = order2.Items.Sum( i => i.UnitPrice * i.Quantity );
                context.Orders.Add( order2 );

                var invoice2 = new Invoice
                {
                    Order = order2,
                    InvoiceNumber = "INV-002",
                    Amount = order2.TotalAmount
                };

                context.Invoices.Add( invoice2 );
            }

            if( cust1 is not null && pedal is not null )
            {
                var order3 = new Order
                {
                    CustomerId = cust1.Id,
                    OrderDate = DateTime.UtcNow.AddDays( -5 ),
                    Status = OrderStatus.Pending,
                    Items =
                    [
                        new() { ProductId = pedal.Id, Quantity = 1, UnitPrice = pedal.Price }
                    ]
                };
                order3.TotalAmount = order3.Items.Sum( i => i.UnitPrice * i.Quantity );
                context.Orders.Add( order3 );

                var invoice3 = new Invoice
                {
                    Order = order3,
                    InvoiceNumber = "INV-003",
                    Amount = order3.TotalAmount
                };
                context.Invoices.Add( invoice3 );
            }

            if( cust3 is not null && ukelele is not null && strap is not null && pick is not null )
            {
                var order4 = new Order
                {
                    CustomerId = cust3.Id,
                    OrderDate = DateTime.UtcNow.AddDays(-10),
                    Status = OrderStatus.Dispatched,
                    Items =
                    [
                        new() { ProductId = ukelele.Id, Quantity = 1, UnitPrice = ukelele.Price },
                        new() { ProductId = strap.Id, Quantity = 1, UnitPrice = strap.Price },
                        new() { ProductId = pick.Id, Quantity = 5, UnitPrice = pick.Price }
                    ]
                };

                order4.TotalAmount = order4.Items.Sum(i => i.UnitPrice * i.Quantity);
                context.Orders.Add( order4 );

                var invoice4 = new Invoice
                {
                    Order = order4,
                    InvoiceNumber = "INV-004",
                    Amount = order4.TotalAmount
                };

                context.Invoices.Add( invoice4 );
            }

            if( cust2 is not null && bass is not null && caseProduct is not null )
            {
                var order5 = new Order
                {
                    CustomerId = cust2.Id,
                    OrderDate = DateTime.UtcNow.AddDays(-20),
                    Status = OrderStatus.Delivered,
                    Items =
                    [
                        new() { ProductId = bass.Id, Quantity = 1, UnitPrice = bass.Price },
                        new() { ProductId = caseProduct.Id, Quantity = 1, UnitPrice = caseProduct.Price }
                    ]
                };
                order5.TotalAmount = order5.Items.Sum( i => i.UnitPrice * i.Quantity );
                context.Orders.Add( order5 );

                var invoice5 = new Invoice
                {
                    Order = order5,
                    InvoiceNumber = "INV-005",
                    Amount = order5.TotalAmount
                };
                context.Invoices.Add( invoice5 );
            }

            context.SaveChanges();
        }
        // Seed sample reviews (only if no reviews exist)
        if( !context.Reviews.Any() )
        {
            Customer? 
                cust1 = context.Users.OfType<Customer>().FirstOrDefault( c => c.Email == customerEmail ),
                cust2 = context.Users.OfType<Customer>().FirstOrDefault( c => c.Email == jimiEmail ),
                cust3 = context.Users.OfType<Customer>().FirstOrDefault( c => c.Email == sarahEmail );

            Product?
                guitar = context.Products.FirstOrDefault( p => p.Name == "Guitar" ),
                bass = context.Products.FirstOrDefault( p => p.Name == "Bass" ),
                amp = context.Products.FirstOrDefault( p => p.Name == "Amplifier" ),
                ukelele = context.Products.FirstOrDefault( p => p.Name == "Ukelele" ),
                pedal = context.Products.FirstOrDefault( p => p.Name == "Pedal Guitar" );

            if( cust1 is not null && guitar is not null )
            {
                context.Reviews.Add(new Review
                {
                    CustomerId = cust1.Id,
                    ProductId = guitar.Id,
                    Title = "Great guitar for beginners!",
                    Body = "The acoustic guitar has amazing sound quality for the price.",
                    Score = StarRating.Five,
                    CreatedAt = DateTime.UtcNow.AddDays( -28 )// 4 weeks ago
                });
            }

            if( cust2 is not null && amp is not null )
            {
                context.Reviews.Add(new Review
                {
                    CustomerId = cust2.Id,
                    ProductId = amp.Id,
                    Title = "Powerful amp!",
                    Body = "Great sound, perfect for small gigs.",
                    Score = StarRating.Four,
                    CreatedAt = DateTime.UtcNow.AddDays( -13 )// about 2 weeks ago
                });
            }

            if( cust3 is not null && ukelele is not null )
            {
                context.Reviews.Add(new Review
                {
                    CustomerId = cust3.Id,
                    ProductId = ukelele.Id,
                    Title = "Perfect travel instrument",
                    Body = "Small and sounds great, ideal for practicing on the go.",
                    Score = StarRating.Five,
                    CreatedAt = DateTime.UtcNow.AddDays( -8 )
                });
            }

            if( cust2 is not null && bass is not null )
            {
                context.Reviews.Add( new Review
                {
                    CustomerId = cust2.Id,
                    ProductId = bass.Id,
                    Title = "Solid bass guitar",
                    Body = "Good build quality and great tone.",
                    Score = StarRating.Four,
                    CreatedAt = DateTime.UtcNow.AddDays( -18 )
                });
            }

            if( cust1 is not null && pedal is not null )
            {
                context.Reviews.Add( new Review
                {
                    CustomerId = cust1.Id,
                    ProductId = pedal.Id,
                    Title = "Awesome effects built-in",
                    Body = "Love the variety of effects. Great for experimenting.",
                    Score = StarRating.Five,
                    CreatedAt = DateTime.UtcNow.AddDays( -3 )
                });
            }

            context.SaveChanges();
        }
    }
}

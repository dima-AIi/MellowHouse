using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace MellowHouse.Web.Data;

public sealed class Category
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int SortOrder { get; set; }
    public bool Visible { get; set; } = true;
    public bool Archived { get; set; }
    public string Image { get; set; } = "";
    public bool OnHome { get; set; }
    public List<Product> Products { get; set; } = [];
}

public sealed class Product
{
    public string Id { get; set; } = "";
    public string CategoryId { get; set; } = "";
    public Category Category { get; set; } = null!;
    public string Name { get; set; } = "";
    public int Price { get; set; }
    public string Portion { get; set; } = "";
    public string Description { get; set; } = "";
    public string Ingredients { get; set; } = "";
    public string Image { get; set; } = "";
    public bool Available { get; set; } = true;
    public bool Popular { get; set; }
    public int SortOrder { get; set; }
    public string GroupsJson { get; set; } = "[]";
    public string AllergensJson { get; set; } = "[]";
    public bool Archived { get; set; }
    public bool Draft { get; set; }
    public bool Recommended { get; set; }
    public int? Cost { get; set; }
    public decimal? Serving { get; set; }
    public string Unit { get; set; } = "";
    public string Nutrition { get; set; } = "";
    public ProductView View() => new(Id, CategoryId, Name, Price, Portion, Description, Ingredients, Image,
        Available && !Archived && !Draft && (Category is null || (!Category.Archived && Category.Visible)), Popular,
        JsonSerializer.Deserialize<OptionGroup[]>(GroupsJson, JsonDefaults.Options) ?? [],
        JsonSerializer.Deserialize<string[]>(AllergensJson, JsonDefaults.Options) ?? [], Recommended, Nutrition);
}

public sealed record ProductView(string Id, string CategoryId, string Name, int Price, string Portion, string Description,
    string Ingredients, string Image, bool Available, bool Popular, OptionGroup[] Groups, string[] Allergens, bool Recommended = false, string Nutrition = "");
public sealed record OptionGroup(string Id, string Label, ProductOption[] Options, string[] Defaults, bool Multiple, bool Required, int? Min = null, int? Max = null);
public sealed record ProductOption(string Id, string Label, int Price, string[]? AddAllergens = null, string[]? RemoveAllergens = null, int? Cost = null);
public static class JsonDefaults { public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web); }

public sealed class Customer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string LoginKey { get; set; } = "";
    [MaxLength(80)] public string Name { get; set; } = "";
    [MaxLength(140)] public string Contact { get; set; } = "";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public string? BirthDate { get; set; }
    public string? DeliveryPhone { get; set; }
    public string? EmailNormalized { get; set; }
    public string? PasswordHash { get; set; }
    public string Role { get; set; } = "Customer";
    public bool Blocked { get; set; }
    public string BlockReason { get; set; } = "";
    public int SessionVersion { get; set; }
    public DateTime? LastLoginUtc { get; set; }
    public DateTime? LastActiveUtc { get; set; }
    public int FailedLogins { get; set; }
    public DateTime? LockedUntilUtc { get; set; }
}
public sealed class ShoppingCart
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Owner { get; set; } = "";
    public int Revision { get; set; }
    public List<CartLine> Lines { get; set; } = [];
}
public sealed class CartLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ShoppingCartId { get; set; }
    public ShoppingCart ShoppingCart { get; set; } = null!;
    public string ProductId { get; set; } = "";
    public string Configuration { get; set; } = "{}";
    public int Quantity { get; set; }
}
public sealed class Favorite
{
    public string Owner { get; set; } = "";
    public string ProductId { get; set; } = "";
}
public sealed class AuthChallenge
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Owner { get; set; } = "";
    public string Contact { get; set; } = "";
    public string CodeHash { get; set; } = "";
    public DateTime ExpiresUtc { get; set; }
    public int Attempts { get; set; }
    public bool Consumed { get; set; }
}
public sealed class Order
{
    public long Id { get; set; }
    public Guid PublicId { get; set; } = Guid.NewGuid();
    public Guid CustomerId { get; set; }
    public string RequestKey { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public int Revision { get; set; }
    public string LinesJson { get; set; } = "[]";
    public string CheckoutJson { get; set; } = "{}";
    public int Subtotal { get; set; }
    public int Delivery { get; set; }
    public int Total { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? PaidUtc { get; set; }
    public string PaymentMethod { get; set; } = "card";
    public string Fulfillment { get; set; } = "New";
    public int Refunded { get; set; }
    public string Source { get; set; } = "Web";
    public string CostsJson { get; set; } = "[]";
}
public sealed class PaymentAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public long OrderId { get; set; }
    public string AttemptKey { get; set; } = "";
    public string Result { get; set; } = "";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
public sealed class CheckoutDraft
{
    public string Owner { get; set; } = "";
    public string Json { get; set; } = "{}";
}

public sealed class SiteSetting { public string Key { get; set; } = ""; public string Json { get; set; } = "{}"; }
public sealed class PasswordReset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CustomerId { get; set; }
    public string TokenHash { get; set; } = "";
    public DateTime ExpiresUtc { get; set; }
    public bool Consumed { get; set; }
}
public sealed class AuditEntry
{
    public long Id { get; set; }
    public Guid ActorId { get; set; }
    public string Action { get; set; } = "";
    public string Target { get; set; } = "";
    public string Detail { get; set; } = "";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
public sealed class OrderEvent
{
    public long Id { get; set; }
    public long OrderId { get; set; }
    public Guid? ActorId { get; set; }
    public string Status { get; set; } = "";
    public string Note { get; set; } = "";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
public sealed class OrderRefund
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public long OrderId { get; set; }
    public string RequestKey { get; set; } = "";
    public Guid ActorId { get; set; }
    public string Reason { get; set; } = "";
    public int Amount { get; set; }
    public int Delivery { get; set; }
    public string LinesJson { get; set; } = "[]";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

public class ShopDbContext(DbContextOptions options) : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<ShoppingCart> Carts => Set<ShoppingCart>();
    public DbSet<CartLine> CartLines => Set<CartLine>();
    public DbSet<Favorite> Favorites => Set<Favorite>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<AuthChallenge> AuthChallenges => Set<AuthChallenge>();
    public DbSet<PaymentAttempt> PaymentAttempts => Set<PaymentAttempt>();
    public DbSet<CheckoutDraft> CheckoutDrafts => Set<CheckoutDraft>();
    public DbSet<SiteSetting> SiteSettings => Set<SiteSetting>();
    public DbSet<PasswordReset> PasswordResets => Set<PasswordReset>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<OrderEvent> OrderEvents => Set<OrderEvent>();
    public DbSet<OrderRefund> OrderRefunds => Set<OrderRefund>();
    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Category>().Property(x => x.Id).HasMaxLength(40);
        model.Entity<Category>().Property(x => x.Visible).HasDefaultValue(true);
        model.Entity<Customer>().Property(x => x.Role).HasDefaultValue("Customer");
        model.Entity<Order>().Property(x => x.Fulfillment).HasDefaultValue("New");
        model.Entity<Product>().HasOne(x => x.Category).WithMany(x => x.Products).HasForeignKey(x => x.CategoryId);
        model.Entity<Customer>().HasIndex(x => x.LoginKey).IsUnique();
        model.Entity<Customer>().HasIndex(x => x.EmailNormalized).IsUnique();
        model.Entity<SiteSetting>().HasKey(x => x.Key);
        model.Entity<PasswordReset>().HasIndex(x => x.TokenHash).IsUnique();
        model.Entity<OrderEvent>().HasIndex(x => new { x.OrderId, x.CreatedUtc });
        model.Entity<OrderRefund>().HasIndex(x => new { x.OrderId, x.RequestKey }).IsUnique();
        model.Entity<ShoppingCart>().HasIndex(x => x.Owner).IsUnique();
        model.Entity<ShoppingCart>().Property(x => x.Revision).IsConcurrencyToken();
        model.Entity<CartLine>().HasOne(x => x.ShoppingCart).WithMany(x => x.Lines).HasForeignKey(x => x.ShoppingCartId).OnDelete(DeleteBehavior.Cascade);
        model.Entity<CartLine>().HasIndex(x => new { x.ShoppingCartId, x.ProductId, x.Configuration }).IsUnique();
        model.Entity<Favorite>().HasKey(x => new { x.Owner, x.ProductId });
        model.Entity<Order>().HasIndex(x => x.PublicId).IsUnique();
        model.Entity<Order>().HasIndex(x => new { x.CustomerId, x.RequestKey }).IsUnique();
        model.Entity<Order>().Property(x => x.Revision).IsConcurrencyToken();
        model.Entity<PaymentAttempt>().HasIndex(x => new { x.OrderId, x.AttemptKey }).IsUnique();
        model.Entity<CheckoutDraft>().HasKey(x => x.Owner);
    }
}
public sealed class SqliteShopDbContext(DbContextOptions<SqliteShopDbContext> options) : ShopDbContext(options);
public sealed class PostgresShopDbContext(DbContextOptions<PostgresShopDbContext> options) : ShopDbContext(options);

using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MellowHouse.Web.Data.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class StudioV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Archived",
                table: "Products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Cost",
                table: "Products",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Draft",
                table: "Products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Nutrition",
                table: "Products",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "Recommended",
                table: "Products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "Serving",
                table: "Products",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Unit",
                table: "Products",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CostsJson",
                table: "Orders",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Fulfillment",
                table: "Orders",
                type: "text",
                nullable: false,
                defaultValue: "New");

            migrationBuilder.AddColumn<int>(
                name: "Refunded",
                table: "Orders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "Orders",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BlockReason",
                table: "Customers",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "Blocked",
                table: "Customers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "EmailNormalized",
                table: "Customers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FailedLogins",
                table: "Customers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastActiveUtc",
                table: "Customers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastLoginUtc",
                table: "Customers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LockedUntilUtc",
                table: "Customers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PasswordHash",
                table: "Customers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "Customers",
                type: "text",
                nullable: false,
                defaultValue: "Customer");

            migrationBuilder.AddColumn<int>(
                name: "SessionVersion",
                table: "Customers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "Archived",
                table: "Categories",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Image",
                table: "Categories",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "OnHome",
                table: "Categories",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Visible",
                table: "Categories",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "AuditEntries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    Target = table.Column<string>(type: "text", nullable: false),
                    Detail = table.Column<string>(type: "text", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrderEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrderId = table.Column<long>(type: "bigint", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrderRefunds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<long>(type: "bigint", nullable: false),
                    RequestKey = table.Column<string>(type: "text", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    Amount = table.Column<int>(type: "integer", nullable: false),
                    Delivery = table.Column<int>(type: "integer", nullable: false),
                    LinesJson = table.Column<string>(type: "text", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderRefunds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PasswordResets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "text", nullable: false),
                    ExpiresUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Consumed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasswordResets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SiteSettings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "text", nullable: false),
                    Json = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteSettings", x => x.Key);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_EmailNormalized",
                table: "Customers",
                column: "EmailNormalized",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderEvents_OrderId_CreatedUtc",
                table: "OrderEvents",
                columns: new[] { "OrderId", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderRefunds_OrderId_RequestKey",
                table: "OrderRefunds",
                columns: new[] { "OrderId", "RequestKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResets_TokenHash",
                table: "PasswordResets",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditEntries");

            migrationBuilder.DropTable(
                name: "OrderEvents");

            migrationBuilder.DropTable(
                name: "OrderRefunds");

            migrationBuilder.DropTable(
                name: "PasswordResets");

            migrationBuilder.DropTable(
                name: "SiteSettings");

            migrationBuilder.DropIndex(
                name: "IX_Customers_EmailNormalized",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Archived",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Cost",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Draft",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Nutrition",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Recommended",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Serving",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Unit",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "CostsJson",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Fulfillment",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Refunded",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "BlockReason",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Blocked",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "EmailNormalized",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "FailedLogins",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "LastActiveUtc",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "LastLoginUtc",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "LockedUntilUtc",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "PasswordHash",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "SessionVersion",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Archived",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "Image",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "OnHome",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "Visible",
                table: "Categories");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DontFret.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRefundFieldsToOrderItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RefundReason",
                table: "OrderItems",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RefundRequestedAt",
                table: "OrderItems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RefundReviewedAt",
                table: "OrderItems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RefundReviewedById",
                table: "OrderItems",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RefundStatus",
                table: "OrderItems",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_RefundReviewedById",
                table: "OrderItems",
                column: "RefundReviewedById");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderItems_AspNetUsers_RefundReviewedById",
                table: "OrderItems",
                column: "RefundReviewedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrderItems_AspNetUsers_RefundReviewedById",
                table: "OrderItems");

            migrationBuilder.DropIndex(
                name: "IX_OrderItems_RefundReviewedById",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "RefundReason",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "RefundRequestedAt",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "RefundReviewedAt",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "RefundReviewedById",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "RefundStatus",
                table: "OrderItems");
        }
    }
}

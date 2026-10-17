using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace software_elviejomadero.Migrations
{
    /// <inheritdoc />
    public partial class AddKitchenPreparationHU09 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PreparationStartedAt",
                table: "Orders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreparedByUserId",
                table: "Orders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_PreparedByUserId",
                table: "Orders",
                column: "PreparedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_AspNetUsers_PreparedByUserId",
                table: "Orders",
                column: "PreparedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_AspNetUsers_PreparedByUserId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_PreparedByUserId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PreparationStartedAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PreparedByUserId",
                table: "Orders");
        }
    }
}

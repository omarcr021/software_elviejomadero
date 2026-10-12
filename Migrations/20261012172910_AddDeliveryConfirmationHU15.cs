using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace software_elviejomadero.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryConfirmationHU15 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeliveredAt",
                table: "Orders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryNote",
                table: "Orders",
                type: "TEXT",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeliveredAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DeliveryNote",
                table: "Orders");
        }
    }
}

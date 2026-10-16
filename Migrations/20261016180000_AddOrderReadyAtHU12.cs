using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using software_elviejomadero.Data;

#nullable disable

namespace software_elviejomadero.Migrations
{
    /// <summary>HU-12: guarda la hora en que el pedido salió de cocina.</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261016180000_AddOrderReadyAtHU12")]
    public partial class AddOrderReadyAtHU12 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ReadyAt",
                table: "Orders",
                type: "TEXT",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReadyAt",
                table: "Orders");
        }
    }
}

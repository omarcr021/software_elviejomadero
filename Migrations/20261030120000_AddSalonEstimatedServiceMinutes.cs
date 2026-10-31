using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using software_elviejomadero.Data;

#nullable disable

namespace software_elviejomadero.Migrations
{
    // HU-05: columna opcional para respetar pedidos existentes.
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261030120000_AddSalonEstimatedServiceMinutes")]
    public partial class AddSalonEstimatedServiceMinutes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EstimatedServiceMinutes",
                table: "Orders",
                type: "INTEGER",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EstimatedServiceMinutes",
                table: "Orders");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace software_elviejomadero.Migrations
{
    /// <inheritdoc />
    public partial class AddRestaurantTableIsBilled : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsBilled",
                table: "RestaurantTables",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsBilled",
                table: "RestaurantTables");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using software_elviejomadero.Data;

#nullable disable

namespace software_elviejomadero.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261008100000_AddSalonAndDeliveryRoutes")]
    public partial class AddSalonAndDeliveryRoutes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RestaurantTables",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Number = table.Column<string>(type: "TEXT", maxLength: 12, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table => { table.PrimaryKey("PK_RestaurantTables", x => x.Id); });

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantTables_Number", table: "RestaurantTables",
                column: "Number", unique: true);

            // SQLite no permite agregar claves foráneas a una tabla existente
            // mediante AddForeignKeyOperation. En cambio, SQLite sí admite
            // una columna nueva nullable con REFERENCES directamente.
            // Así conservamos los pedidos y las restricciones referenciales.
            migrationBuilder.Sql(@"ALTER TABLE ""Orders"" ADD COLUMN ""TableId"" INTEGER NULL REFERENCES ""RestaurantTables""(""Id"") ON DELETE RESTRICT;");
            migrationBuilder.Sql(@"ALTER TABLE ""Orders"" ADD COLUMN ""DeliveryDriverId"" TEXT NULL REFERENCES ""AspNetUsers""(""Id"") ON DELETE RESTRICT;");
            migrationBuilder.AddColumn<DateTime>(
                name: "RouteStartedAt", table: "Orders", type: "TEXT", nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_TableId", table: "Orders", column: "TableId");
            migrationBuilder.CreateIndex(
                name: "IX_Orders_DeliveryDriverId", table: "Orders", column: "DeliveryDriverId");

        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_Orders_TableId", table: "Orders");
            migrationBuilder.DropIndex(name: "IX_Orders_DeliveryDriverId", table: "Orders");
            // DROP COLUMN elimina también las referencias declaradas dentro
            // de cada columna (compatible con la versión SQLite de EF Core 10).
            migrationBuilder.Sql(@"ALTER TABLE ""Orders"" DROP COLUMN ""TableId"";");
            migrationBuilder.Sql(@"ALTER TABLE ""Orders"" DROP COLUMN ""DeliveryDriverId"";");
            migrationBuilder.Sql(@"ALTER TABLE ""Orders"" DROP COLUMN ""RouteStartedAt"";");
            migrationBuilder.DropTable(name: "RestaurantTables");
        }
    }
}

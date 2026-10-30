using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace software_elviejomadero.Migrations
{
    /// <inheritdoc />
    public partial class AddKitchenNotices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KitchenNotices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OrderId = table.Column<int>(type: "INTEGER", nullable: false),
                    Message = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KitchenNotices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KitchenNotices_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "KitchenNoticeReads",
                columns: table => new
                {
                    NoticeId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    ReadAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KitchenNoticeReads", x => new { x.NoticeId, x.UserId });
                    table.ForeignKey(
                        name: "FK_KitchenNoticeReads_KitchenNotices_NoticeId",
                        column: x => x.NoticeId,
                        principalTable: "KitchenNotices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KitchenNotices_CreatedAt",
                table: "KitchenNotices",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_KitchenNotices_OrderId",
                table: "KitchenNotices",
                column: "OrderId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KitchenNoticeReads");

            migrationBuilder.DropTable(
                name: "KitchenNotices");
        }
    }
}

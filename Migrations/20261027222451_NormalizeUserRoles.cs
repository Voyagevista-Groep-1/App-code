using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace tui.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeUserRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Name = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Name);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "UserRoles",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    RoleName = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => new { x.UserId, x.RoleName });
                    table.ForeignKey(
                        name: "FK_UserRoles_AppUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AppUsers",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserRoles_Roles_RoleName",
                        column: x => x.RoleName,
                        principalTable: "Roles",
                        principalColumn: "Name",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.InsertData(
                table: "Roles",
                column: "Name",
                values: new object[]
                {
                    "Beheerder",
                    "Customerservice",
                    "Finance",
                    "IT",
                    "Marketing",
                    "Productdevelopment",
                    "Sales"
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleName",
                table: "UserRoles",
                column: "RoleName");

            // Preserve all existing assignments before replacing the legacy text column.
            migrationBuilder.Sql("""
                INSERT INTO UserRoles (UserId, RoleName)
                SELECT u.UserId, r.Name
                FROM AppUsers u CROSS JOIN Roles r
                WHERE FIND_IN_SET(LOWER(r.Name),
                    LOWER(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(u.UserRole, ';', ','), ' ', ''), CHAR(9), ''), CHAR(10), ''), CHAR(13), ''))) > 0
                   OR (r.Name = 'Beheerder' AND FIND_IN_SET('admin',
                    LOWER(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(u.UserRole, ';', ','), ' ', ''), CHAR(9), ''), CHAR(10), ''), CHAR(13), ''))) > 0);
                """);
            migrationBuilder.DropColumn(name: "UserRole", table: "AppUsers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UserRole",
                table: "AppUsers",
                type: "varchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.Sql("""
                UPDATE AppUsers u SET UserRole = COALESCE(
                    (SELECT GROUP_CONCAT(ur.RoleName ORDER BY ur.RoleName SEPARATOR ',') FROM UserRoles ur WHERE ur.UserId = u.UserId), '');
                """);
            migrationBuilder.DropTable(name: "UserRoles");
            migrationBuilder.DropTable(name: "Roles");
        }
    }
}

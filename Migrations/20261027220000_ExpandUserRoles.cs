using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace tui.Migrations;

public partial class ExpandUserRoles : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "UserRole", table: "AppUsers", type: "varchar(255)", maxLength: 255, nullable: false,
            oldClrType: typeof(string), oldType: "varchar(20)", oldMaxLength: 20)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "UserRole", table: "AppUsers", type: "varchar(20)", maxLength: 20, nullable: false,
            oldClrType: typeof(string), oldType: "varchar(255)", oldMaxLength: 255)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");
    }
}

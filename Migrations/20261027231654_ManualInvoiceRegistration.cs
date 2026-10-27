using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace tui.Migrations
{
    /// <inheritdoc />
    public partial class ManualInvoiceRegistration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsManualRegistration",
                table: "Invoices",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<decimal>(
                name: "Quantity",
                table: "InvoiceLines",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(10,2)",
                oldPrecision: 10,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "Price",
                table: "InvoiceLines",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(10,2)",
                oldPrecision: 10,
                oldScale: 2);

            migrationBuilder.AlterColumn<int>(
                name: "AccountId",
                table: "InvoiceLines",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<decimal>(
                name: "BaseAmount",
                table: "InvoiceLines",
                type: "decimal(14,4)",
                precision: 14,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "InvoiceLines",
                type: "decimal(14,4)",
                precision: 14,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountFixedAmount",
                table: "InvoiceLines",
                type: "decimal(14,4)",
                precision: 14,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountPercentage",
                table: "InvoiceLines",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GrossAmount",
                table: "InvoiceLines",
                type: "decimal(14,4)",
                precision: 14,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "NetAmount",
                table: "InvoiceLines",
                type: "decimal(14,4)",
                precision: 14,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "Position",
                table: "InvoiceLines",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Unit",
                table: "InvoiceLines",
                type: "varchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "VatAmount",
                table: "InvoiceLines",
                type: "decimal(14,4)",
                precision: 14,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "VatInformation",
                table: "InvoiceLines",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "VatPercentage",
                table: "InvoiceLines",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "VatTreatment",
                table: "InvoiceLines",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ManualInvoiceRegistrations",
                columns: table => new
                {
                    InvoiceId = table.Column<int>(type: "int", nullable: false),
                    OriginalInvoiceNumber = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SourceIdentityKey = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SenderName = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SenderAddress = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SenderKvkNumber = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SenderVatNumber = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RecipientName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RecipientAddress = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RecipientVatNumber = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DeliveryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DeliveryPeriodStart = table.Column<DateOnly>(type: "date", nullable: true),
                    DeliveryPeriodEnd = table.Column<DateOnly>(type: "date", nullable: true),
                    Subject = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PaymentReference = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PaymentTermDays = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SubtotalBeforeDiscount = table.Column<decimal>(type: "decimal(14,4)", precision: 14, scale: 4, nullable: false),
                    TotalDiscount = table.Column<decimal>(type: "decimal(14,4)", precision: 14, scale: 4, nullable: false),
                    TotalExcludingVat = table.Column<decimal>(type: "decimal(14,4)", precision: 14, scale: 4, nullable: false),
                    TotalVat = table.Column<decimal>(type: "decimal(14,4)", precision: 14, scale: 4, nullable: false),
                    SourceTotalAmount = table.Column<decimal>(type: "decimal(14,4)", precision: 14, scale: 4, nullable: true),
                    CalculationPolicy = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManualInvoiceRegistrations", x => x.InvoiceId);
                    table.ForeignKey(
                        name: "FK_ManualInvoiceRegistrations_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "InvoiceId",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLines_InvoiceId_Position",
                table: "InvoiceLines",
                columns: new[] { "InvoiceId", "Position" });

            // MariaDB needs a supporting index throughout the FK index replacement.
            migrationBuilder.DropIndex(name: "IX_InvoiceLines_InvoiceId", table: "InvoiceLines");

            migrationBuilder.CreateIndex(
                name: "IX_ManualInvoiceRegistrations_SourceIdentityKey_OriginalInvoice~",
                table: "ManualInvoiceRegistrations",
                columns: new[] { "SourceIdentityKey", "OriginalInvoiceNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(name: "IX_InvoiceLines_InvoiceId", table: "InvoiceLines", column: "InvoiceId");
            migrationBuilder.DropTable(
                name: "ManualInvoiceRegistrations");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceLines_InvoiceId_Position",
                table: "InvoiceLines");

            migrationBuilder.DropColumn(
                name: "IsManualRegistration",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "BaseAmount",
                table: "InvoiceLines");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "InvoiceLines");

            migrationBuilder.DropColumn(
                name: "DiscountFixedAmount",
                table: "InvoiceLines");

            migrationBuilder.DropColumn(
                name: "DiscountPercentage",
                table: "InvoiceLines");

            migrationBuilder.DropColumn(
                name: "GrossAmount",
                table: "InvoiceLines");

            migrationBuilder.DropColumn(
                name: "NetAmount",
                table: "InvoiceLines");

            migrationBuilder.DropColumn(
                name: "Position",
                table: "InvoiceLines");

            migrationBuilder.DropColumn(
                name: "Unit",
                table: "InvoiceLines");

            migrationBuilder.DropColumn(
                name: "VatAmount",
                table: "InvoiceLines");

            migrationBuilder.DropColumn(
                name: "VatInformation",
                table: "InvoiceLines");

            migrationBuilder.DropColumn(
                name: "VatPercentage",
                table: "InvoiceLines");

            migrationBuilder.DropColumn(
                name: "VatTreatment",
                table: "InvoiceLines");

            migrationBuilder.AlterColumn<decimal>(
                name: "Quantity",
                table: "InvoiceLines",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,6)",
                oldPrecision: 18,
                oldScale: 6);

            migrationBuilder.AlterColumn<decimal>(
                name: "Price",
                table: "InvoiceLines",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,6)",
                oldPrecision: 18,
                oldScale: 6);

            migrationBuilder.AlterColumn<int>(
                name: "AccountId",
                table: "InvoiceLines",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventHub.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class InvoiceSupplierNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SupplierNumber",
                table: "Invoices",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SupplierNumber",
                table: "Invoices");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AirSolutions.Migrations
{
    /// <inheritdoc />
    public partial class InvoiceCodeAndSeedFiscalVouchers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InvoiceCode",
                table: "Invoices",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE Invoices
                SET InvoiceCode = 'FACTURA-' + CAST(Id AS nvarchar(20))
                WHERE InvoiceCode IS NULL OR LTRIM(RTRIM(InvoiceCode)) = '';
            ");

            migrationBuilder.AlterColumn<string>(
                name: "InvoiceCode",
                table: "Invoices",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "Name",
                table: "Invoices");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_InvoiceCode",
                table: "Invoices",
                column: "InvoiceCode",
                unique: true);

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM FiscalVouchers WHERE VoucherNumber = 'B0100000001')
                INSERT INTO FiscalVouchers (VoucherNumber, VoucherType, IsUsed, UsedAt, UsedInInvoiceId, CreatedAt)
                VALUES ('B0100000001', 'B01', 0, NULL, NULL, GETUTCDATE());

                IF NOT EXISTS (SELECT 1 FROM FiscalVouchers WHERE VoucherNumber = 'B0100000002')
                INSERT INTO FiscalVouchers (VoucherNumber, VoucherType, IsUsed, UsedAt, UsedInInvoiceId, CreatedAt)
                VALUES ('B0100000002', 'B01', 0, NULL, NULL, GETUTCDATE());

                IF NOT EXISTS (SELECT 1 FROM FiscalVouchers WHERE VoucherNumber = 'B0100000003')
                INSERT INTO FiscalVouchers (VoucherNumber, VoucherType, IsUsed, UsedAt, UsedInInvoiceId, CreatedAt)
                VALUES ('B0100000003', 'B01', 0, NULL, NULL, GETUTCDATE());

                IF NOT EXISTS (SELECT 1 FROM FiscalVouchers WHERE VoucherNumber = 'B0100000004')
                INSERT INTO FiscalVouchers (VoucherNumber, VoucherType, IsUsed, UsedAt, UsedInInvoiceId, CreatedAt)
                VALUES ('B0100000004', 'B01', 0, NULL, NULL, GETUTCDATE());

                IF NOT EXISTS (SELECT 1 FROM FiscalVouchers WHERE VoucherNumber = 'B0100000005')
                INSERT INTO FiscalVouchers (VoucherNumber, VoucherType, IsUsed, UsedAt, UsedInInvoiceId, CreatedAt)
                VALUES ('B0100000005', 'B01', 0, NULL, NULL, GETUTCDATE());

                IF NOT EXISTS (SELECT 1 FROM FiscalVouchers WHERE VoucherNumber = 'B0100000006')
                INSERT INTO FiscalVouchers (VoucherNumber, VoucherType, IsUsed, UsedAt, UsedInInvoiceId, CreatedAt)
                VALUES ('B0100000006', 'B01', 0, NULL, NULL, GETUTCDATE());

                IF NOT EXISTS (SELECT 1 FROM FiscalVouchers WHERE VoucherNumber = 'B0100000007')
                INSERT INTO FiscalVouchers (VoucherNumber, VoucherType, IsUsed, UsedAt, UsedInInvoiceId, CreatedAt)
                VALUES ('B0100000007', 'B01', 0, NULL, NULL, GETUTCDATE());

                IF NOT EXISTS (SELECT 1 FROM FiscalVouchers WHERE VoucherNumber = 'B0100000008')
                INSERT INTO FiscalVouchers (VoucherNumber, VoucherType, IsUsed, UsedAt, UsedInInvoiceId, CreatedAt)
                VALUES ('B0100000008', 'B01', 0, NULL, NULL, GETUTCDATE());

                IF NOT EXISTS (SELECT 1 FROM FiscalVouchers WHERE VoucherNumber = 'B0100000009')
                INSERT INTO FiscalVouchers (VoucherNumber, VoucherType, IsUsed, UsedAt, UsedInInvoiceId, CreatedAt)
                VALUES ('B0100000009', 'B01', 0, NULL, NULL, GETUTCDATE());

                IF NOT EXISTS (SELECT 1 FROM FiscalVouchers WHERE VoucherNumber = 'B0100000010')
                INSERT INTO FiscalVouchers (VoucherNumber, VoucherType, IsUsed, UsedAt, UsedInInvoiceId, CreatedAt)
                VALUES ('B0100000010', 'B01', 0, NULL, NULL, GETUTCDATE());
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Invoices_InvoiceCode",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "InvoiceCode",
                table: "Invoices");

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Invoices",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.Sql(@"
                DELETE FROM FiscalVouchers
                WHERE VoucherNumber IN (
                    'B0100000001','B0100000002','B0100000003','B0100000004','B0100000005',
                    'B0100000006','B0100000007','B0100000008','B0100000009','B0100000010'
                )
                AND IsUsed = 0;
            ");
        }
    }
}

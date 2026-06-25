using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoiterScan.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMaxEpochAgeDays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxEpochAgeDays",
                table: "config_params",
                type: "INTEGER",
                nullable: false,
                defaultValue: 14);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxEpochAgeDays",
                table: "config_params");
        }
    }
}

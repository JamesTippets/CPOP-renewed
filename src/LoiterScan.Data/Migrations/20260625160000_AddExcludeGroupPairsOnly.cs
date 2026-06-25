using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoiterScan.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddExcludeGroupPairsOnly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ExcludeGroupPairsOnly",
                table: "config_params",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExcludeGroupPairsOnly",
                table: "config_params");
        }
    }
}

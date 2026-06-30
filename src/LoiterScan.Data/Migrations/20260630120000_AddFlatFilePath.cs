using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoiterScan.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFlatFilePath : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FlatFilePath",
                table: "config_params",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FlatFilePath",
                table: "config_params");
        }
    }
}

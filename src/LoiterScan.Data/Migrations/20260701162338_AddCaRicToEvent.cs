using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoiterScan.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCaRicToEvent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "CaRicC",
                table: "loitering_events",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "CaRicI",
                table: "loitering_events",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "CaRicR",
                table: "loitering_events",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CaRicC",
                table: "loitering_events");

            migrationBuilder.DropColumn(
                name: "CaRicI",
                table: "loitering_events");

            migrationBuilder.DropColumn(
                name: "CaRicR",
                table: "loitering_events");
        }
    }
}

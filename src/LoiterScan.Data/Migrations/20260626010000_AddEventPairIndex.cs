using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoiterScan.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEventPairIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PairIndex",
                table: "loitering_events",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PairIndex",
                table: "loitering_events");
        }
    }
}

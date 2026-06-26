using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoiterScan.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddExclPairs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "excl_pairs",
                columns: table => new
                {
                    Id          = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PairKeyLow  = table.Column<long>(type: "INTEGER", nullable: false),
                    PairKeyHigh = table.Column<long>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_excl_pairs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_excl_pairs_pair_key",
                table: "excl_pairs",
                columns: new[] { "PairKeyLow", "PairKeyHigh" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "excl_pairs");
        }
    }
}

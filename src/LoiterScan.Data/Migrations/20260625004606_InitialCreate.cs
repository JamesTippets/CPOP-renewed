using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoiterScan.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "catalog_objects",
                columns: table => new
                {
                    NoradId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    IntlDesignator = table.Column<string>(type: "TEXT", nullable: true),
                    MeanMotionRevPerDay = table.Column<double>(type: "REAL", nullable: false),
                    Eccentricity = table.Column<double>(type: "REAL", nullable: false),
                    InclinationDeg = table.Column<double>(type: "REAL", nullable: false),
                    RaanDeg = table.Column<double>(type: "REAL", nullable: false),
                    ArgPerigeeDeg = table.Column<double>(type: "REAL", nullable: false),
                    MeanAnomalyDeg = table.Column<double>(type: "REAL", nullable: false),
                    BStar = table.Column<double>(type: "REAL", nullable: false),
                    EpochUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EphemerisType = table.Column<int>(type: "INTEGER", nullable: false),
                    MeanMotionDotRevPerDay = table.Column<double>(type: "REAL", nullable: false),
                    MeanMotionDdotRevPerDay = table.Column<double>(type: "REAL", nullable: false),
                    Owner = table.Column<string>(type: "TEXT", nullable: true),
                    ObjectType = table.Column<string>(type: "TEXT", nullable: true),
                    IsDebris = table.Column<bool>(type: "INTEGER", nullable: false),
                    DecayDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SemiMajorAxisKm = table.Column<double>(type: "REAL", nullable: false),
                    ApogeeKm = table.Column<double>(type: "REAL", nullable: false),
                    PerigeeKm = table.Column<double>(type: "REAL", nullable: false),
                    Regime = table.Column<string>(type: "TEXT", nullable: false),
                    EpochAgeDays = table.Column<double>(type: "REAL", nullable: false),
                    IngestedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalog_objects", x => x.NoradId);
                });

            migrationBuilder.CreateTable(
                name: "config_params",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HorizonDays = table.Column<int>(type: "INTEGER", nullable: false),
                    CoarseStepMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    CoarseThresholdKm = table.Column<double>(type: "REAL", nullable: false),
                    FineStepMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    FineThresholdKm = table.Column<double>(type: "REAL", nullable: false),
                    DetectionStepMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    DetectionThresholdKm = table.Column<double>(type: "REAL", nullable: false),
                    CoarseToFineMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    FineToDetectionMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    LoiterMinDurationMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    LoiterExcursionAllowanceMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    ExcludeDebris = table.Column<bool>(type: "INTEGER", nullable: false),
                    RegimeScope = table.Column<string>(type: "TEXT", nullable: false),
                    AcquisitionSource = table.Column<string>(type: "TEXT", nullable: false),
                    RefreshBeforeRun = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_config_params", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "excl_countries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Country = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_excl_countries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "excl_groups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GroupName = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_excl_groups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "excl_ids",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    NoradId = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_excl_ids", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "runs",
                columns: table => new
                {
                    RunId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Trigger = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CatalogEpochWindowStart = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CatalogEpochWindowEnd = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ConfigSnapshot = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    TotalPairsChecked = table.Column<int>(type: "INTEGER", nullable: true),
                    EventsDetected = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_runs", x => x.RunId);
                });

            migrationBuilder.CreateTable(
                name: "catalog_groups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    NoradId = table.Column<long>(type: "INTEGER", nullable: false),
                    GroupName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalog_groups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_catalog_groups_catalog_objects_NoradId",
                        column: x => x.NoradId,
                        principalTable: "catalog_objects",
                        principalColumn: "NoradId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "loitering_events",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RunId = table.Column<long>(type: "INTEGER", nullable: false),
                    PairKeyLow = table.Column<long>(type: "INTEGER", nullable: false),
                    PairKeyHigh = table.Column<long>(type: "INTEGER", nullable: false),
                    NoradIdA = table.Column<long>(type: "INTEGER", nullable: false),
                    NoradIdB = table.Column<long>(type: "INTEGER", nullable: false),
                    MinRangeKm = table.Column<double>(type: "REAL", nullable: false),
                    CloseApproachUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LoiterStartUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LoiterEndUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DurationMinutes = table.Column<double>(type: "REAL", nullable: false),
                    Confidence = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loitering_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_loitering_events_runs_RunId",
                        column: x => x.RunId,
                        principalTable: "runs",
                        principalColumn: "RunId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_catalog_groups_NoradId_GroupName",
                table: "catalog_groups",
                columns: new[] { "NoradId", "GroupName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_excl_countries_Country",
                table: "excl_countries",
                column: "Country",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_excl_groups_GroupName",
                table: "excl_groups",
                column: "GroupName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_excl_ids_NoradId",
                table: "excl_ids",
                column: "NoradId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_event_pair_key",
                table: "loitering_events",
                columns: new[] { "PairKeyLow", "PairKeyHigh" });

            migrationBuilder.CreateIndex(
                name: "IX_loitering_events_RunId",
                table: "loitering_events",
                column: "RunId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "catalog_groups");

            migrationBuilder.DropTable(
                name: "config_params");

            migrationBuilder.DropTable(
                name: "excl_countries");

            migrationBuilder.DropTable(
                name: "excl_groups");

            migrationBuilder.DropTable(
                name: "excl_ids");

            migrationBuilder.DropTable(
                name: "loitering_events");

            migrationBuilder.DropTable(
                name: "catalog_objects");

            migrationBuilder.DropTable(
                name: "runs");
        }
    }
}

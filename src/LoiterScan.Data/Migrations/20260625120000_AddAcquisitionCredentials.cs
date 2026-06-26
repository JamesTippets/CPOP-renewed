using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoiterScan.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAcquisitionCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CredentialUsername",
                table: "config_params",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CredentialPassword",
                table: "config_params",
                type: "TEXT",
                nullable: true);

            // Ensure existing installations get the new "off" default for RefreshBeforeRun
            migrationBuilder.Sql("UPDATE config_params SET RefreshBeforeRun = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CredentialUsername",
                table: "config_params");

            migrationBuilder.DropColumn(
                name: "CredentialPassword",
                table: "config_params");
        }
    }
}

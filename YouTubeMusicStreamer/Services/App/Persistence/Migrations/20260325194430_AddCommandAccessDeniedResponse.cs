using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YouTubeMusicStreamer.Services.App.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCommandAccessDeniedResponse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccessDeniedResponse",
                table: "CommandConfigurations",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccessDeniedResponse",
                table: "CommandConfigurations");
        }
    }
}

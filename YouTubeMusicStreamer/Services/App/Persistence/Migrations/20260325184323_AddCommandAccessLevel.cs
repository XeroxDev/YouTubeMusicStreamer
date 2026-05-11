using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YouTubeMusicStreamer.Services.App.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCommandAccessLevel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RequiredAccessLevel",
                table: "CommandConfigurations",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequiredAccessLevel",
                table: "CommandConfigurations");
        }
    }
}

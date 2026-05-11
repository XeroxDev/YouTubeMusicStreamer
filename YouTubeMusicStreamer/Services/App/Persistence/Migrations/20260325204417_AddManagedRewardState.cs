using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YouTubeMusicStreamer.Services.App.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddManagedRewardState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ManagedRewardCompatibilityMessage",
                table: "CommandConfigurations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ManagedRewardCompatibilityState",
                table: "CommandConfigurations",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<uint>(
                name: "ManagedRewardCost",
                table: "CommandConfigurations",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<string>(
                name: "ManagedRewardPrompt",
                table: "CommandConfigurations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ManagedRewardRequiresUserInput",
                table: "CommandConfigurations",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ManagedRewardCompatibilityMessage",
                table: "CommandConfigurations");

            migrationBuilder.DropColumn(
                name: "ManagedRewardCompatibilityState",
                table: "CommandConfigurations");

            migrationBuilder.DropColumn(
                name: "ManagedRewardCost",
                table: "CommandConfigurations");

            migrationBuilder.DropColumn(
                name: "ManagedRewardPrompt",
                table: "CommandConfigurations");

            migrationBuilder.DropColumn(
                name: "ManagedRewardRequiresUserInput",
                table: "CommandConfigurations");
        }
    }
}

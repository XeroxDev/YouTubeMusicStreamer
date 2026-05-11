using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YouTubeMusicStreamer.Services.App.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppConfiguration",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LogLevel = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppConfiguration", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BlacklistEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Url = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlacklistEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommandConfigurations",
                columns: table => new
                {
                    CommandKey = table.Column<string>(type: "TEXT", nullable: false),
                    Trigger = table.Column<string>(type: "TEXT", nullable: false),
                    ChatTriggerMode = table.Column<int>(type: "INTEGER", nullable: false),
                    RewardTriggerMode = table.Column<int>(type: "INTEGER", nullable: false),
                    BitsThreshold = table.Column<uint>(type: "INTEGER", nullable: false),
                    RewardId = table.Column<string>(type: "TEXT", nullable: true),
                    RewardBroadcasterAccountId = table.Column<string>(type: "TEXT", nullable: true),
                    ManagedRewardName = table.Column<string>(type: "TEXT", nullable: true),
                    Cooldown = table.Column<uint>(type: "INTEGER", nullable: false),
                    Response = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommandConfigurations", x => x.CommandKey);
                });

            migrationBuilder.CreateTable(
                name: "QueueItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    VideoId = table.Column<string>(type: "TEXT", nullable: false),
                    Requester = table.Column<string>(type: "TEXT", nullable: false),
                    Message = table.Column<string>(type: "TEXT", nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EmbedJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QueueItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QueueSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    QueueActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    QueueItemAsIFrame = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QueueSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TwitchSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ChatChannel = table.Column<string>(type: "TEXT", nullable: false),
                    SendMessageOnConnect = table.Column<bool>(type: "INTEGER", nullable: false),
                    ConnectMessage = table.Column<string>(type: "TEXT", nullable: false),
                    CommandPrefix = table.Column<string>(type: "TEXT", nullable: false),
                    BroadcasterAccountId = table.Column<string>(type: "TEXT", nullable: true),
                    BroadcasterLogin = table.Column<string>(type: "TEXT", nullable: true),
                    BroadcasterDisplayName = table.Column<string>(type: "TEXT", nullable: true),
                    BroadcasterProfileImageUrl = table.Column<string>(type: "TEXT", nullable: true),
                    BotAccountId = table.Column<string>(type: "TEXT", nullable: true),
                    BotLogin = table.Column<string>(type: "TEXT", nullable: true),
                    BotDisplayName = table.Column<string>(type: "TEXT", nullable: true),
                    BotProfileImageUrl = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TwitchSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "YouTubeSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Host = table.Column<string>(type: "TEXT", nullable: true),
                    Port = table.Column<int>(type: "INTEGER", nullable: true),
                    AutoStartServer = table.Column<bool>(type: "INTEGER", nullable: false),
                    PublicPort = table.Column<int>(type: "INTEGER", nullable: false),
                    AllowAudioCapture = table.Column<bool>(type: "INTEGER", nullable: false),
                    AudioCaptureDevice = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YouTubeSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QueueItems_SortOrder",
                table: "QueueItems",
                column: "SortOrder");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppConfiguration");

            migrationBuilder.DropTable(
                name: "BlacklistEntries");

            migrationBuilder.DropTable(
                name: "CommandConfigurations");

            migrationBuilder.DropTable(
                name: "QueueItems");

            migrationBuilder.DropTable(
                name: "QueueSettings");

            migrationBuilder.DropTable(
                name: "TwitchSettings");

            migrationBuilder.DropTable(
                name: "YouTubeSettings");
        }
    }
}

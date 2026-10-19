using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WatchParty.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class QueueSubtitlesHlsSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HlsKey",
                table: "Videos",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SecurityStamp",
                table: "Users",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "initial");

            migrationBuilder.AddColumn<bool>(
                name: "HostOnlyControl",
                table: "Rooms",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastActivityAt",
                table: "Rooms",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.Sql("UPDATE \"Rooms\" SET \"LastActivityAt\" = \"CreatedAt\";");

            migrationBuilder.CreateTable(
                name: "RoomQueue",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RoomId = table.Column<Guid>(type: "TEXT", nullable: false),
                    VideoId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    AddedById = table.Column<Guid>(type: "TEXT", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomQueue", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoomQueue_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RoomQueue_Users_AddedById",
                        column: x => x.AddedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RoomQueue_Videos_VideoId",
                        column: x => x.VideoId,
                        principalTable: "Videos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Subtitles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    VideoId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Label = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Language = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                    FileKey = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    UploadedById = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subtitles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Subtitles_Users_UploadedById",
                        column: x => x.UploadedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Subtitles_Videos_VideoId",
                        column: x => x.VideoId,
                        principalTable: "Videos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_LastActivityAt",
                table: "Rooms",
                column: "LastActivityAt");

            migrationBuilder.CreateIndex(
                name: "IX_RoomQueue_AddedById",
                table: "RoomQueue",
                column: "AddedById");

            migrationBuilder.CreateIndex(
                name: "IX_RoomQueue_RoomId_Position",
                table: "RoomQueue",
                columns: new[] { "RoomId", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_RoomQueue_VideoId",
                table: "RoomQueue",
                column: "VideoId");

            migrationBuilder.CreateIndex(
                name: "IX_Subtitles_UploadedById",
                table: "Subtitles",
                column: "UploadedById");

            migrationBuilder.CreateIndex(
                name: "IX_Subtitles_VideoId",
                table: "Subtitles",
                column: "VideoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RoomQueue");

            migrationBuilder.DropTable(
                name: "Subtitles");

            migrationBuilder.DropIndex(
                name: "IX_Rooms_LastActivityAt",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "HlsKey",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "SecurityStamp",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "HostOnlyControl",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "LastActivityAt",
                table: "Rooms");
        }
    }
}

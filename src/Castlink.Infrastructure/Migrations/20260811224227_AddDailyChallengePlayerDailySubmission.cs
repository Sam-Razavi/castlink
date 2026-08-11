using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Castlink.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyChallengePlayerDailySubmission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "daily_challenges",
                columns: table => new
                {
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    from_person_id = table.Column<int>(type: "integer", nullable: false),
                    to_person_id = table.Column<int>(type: "integer", nullable: false),
                    optimal_length = table.Column<int>(type: "integer", nullable: false),
                    canonical_path = table.Column<string>(type: "jsonb", nullable: false),
                    generated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_daily_challenges", x => x.date);
                    table.ForeignKey(
                        name: "FK_daily_challenges_people_from_person_id",
                        column: x => x.from_person_id,
                        principalTable: "people",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_daily_challenges_people_to_person_id",
                        column: x => x.to_person_id,
                        principalTable: "people",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "players",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    display_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_players", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "daily_submissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    path = table.Column<string>(type: "jsonb", nullable: false),
                    path_length = table.Column<int>(type: "integer", nullable: false),
                    score = table.Column<int>(type: "integer", nullable: false),
                    duration_ms = table.Column<int>(type: "integer", nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_daily_submissions", x => x.id);
                    table.ForeignKey(
                        name: "FK_daily_submissions_daily_challenges_date",
                        column: x => x.date,
                        principalTable: "daily_challenges",
                        principalColumn: "date",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_daily_submissions_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_daily_challenges_from_person_id",
                table: "daily_challenges",
                column: "from_person_id");

            migrationBuilder.CreateIndex(
                name: "IX_daily_challenges_to_person_id",
                table: "daily_challenges",
                column: "to_person_id");

            migrationBuilder.CreateIndex(
                name: "IX_daily_submissions_player_id",
                table: "daily_submissions",
                column: "player_id");

            migrationBuilder.CreateIndex(
                name: "ux_daily_submissions_date_player",
                table: "daily_submissions",
                columns: new[] { "date", "player_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "daily_submissions");

            migrationBuilder.DropTable(
                name: "daily_challenges");

            migrationBuilder.DropTable(
                name: "players");
        }
    }
}

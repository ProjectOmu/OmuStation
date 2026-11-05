using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class CharacterQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "active",
                table: "profile",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "position",
                table: "profile",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                """
                UPDATE profile
                SET active = 1
                WHERE EXISTS (
                    SELECT 1
                    FROM preference
                    WHERE preference.preference_id = profile.preference_id
                      AND preference.selected_character_slot = profile.slot)
                """);

            migrationBuilder.CreateTable(
                name: "player_job_priority",
                columns: table => new
                {
                    player_job_priority_id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    preference_id = table.Column<int>(type: "INTEGER", nullable: false),
                    job_name = table.Column<string>(type: "TEXT", nullable: false),
                    priority = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_player_job_priority", x => x.player_job_priority_id);
                    table.ForeignKey(
                        name: "FK_player_job_priority_preference_preference_id",
                        column: x => x.preference_id,
                        principalTable: "preference",
                        principalColumn: "preference_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_player_job_priority_preference_id_job_name",
                table: "player_job_priority",
                columns: new[] { "preference_id", "job_name" },
                unique: true);

            migrationBuilder.Sql(
                """
                INSERT INTO player_job_priority (preference_id, job_name, priority)
                SELECT profile.preference_id, job.job_name, job.priority
                FROM job
                INNER JOIN profile ON job.profile_id = profile.profile_id
                INNER JOIN preference ON preference.preference_id = profile.preference_id
                WHERE preference.selected_character_slot = profile.slot
                  AND job.priority > 0
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "player_job_priority");

            migrationBuilder.DropColumn(
                name: "active",
                table: "profile");

            migrationBuilder.DropColumn(
                name: "position",
                table: "profile");
        }
    }
}

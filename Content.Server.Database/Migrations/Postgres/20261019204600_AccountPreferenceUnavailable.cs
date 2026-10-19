using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class AccountPreferenceUnavailable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "preference_unavailable",
                table: "preference",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                """
                UPDATE preference
                SET preference_unavailable = 1
                WHERE EXISTS (
                    SELECT 1
                    FROM profile
                    WHERE profile.preference_id = preference.preference_id
                      AND profile.active)
                  AND NOT EXISTS (
                    SELECT 1
                    FROM profile
                    WHERE profile.preference_id = preference.preference_id
                      AND profile.active
                      AND profile.pref_unavailable <> 1)
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "preference_unavailable",
                table: "preference");
        }
    }
}

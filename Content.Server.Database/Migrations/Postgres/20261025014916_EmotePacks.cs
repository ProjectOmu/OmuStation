using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class EmotePacks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "emote_pack",
                table: "profile",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "emote_pack_species",
                table: "profile",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "emote_pack",
                table: "profile");

            migrationBuilder.DropColumn(
                name: "emote_pack_species",
                table: "profile");
        }
    }
}

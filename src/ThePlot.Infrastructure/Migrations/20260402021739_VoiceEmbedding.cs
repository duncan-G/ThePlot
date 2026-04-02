using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace ThePlot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class VoiceEmbedding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Vector>(
                name: "embedding",
                schema: "theplot",
                table: "voices",
                type: "vector(1024)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "embedding",
                schema: "theplot",
                table: "voices");
        }
    }
}

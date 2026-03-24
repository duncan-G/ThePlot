using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace ThePlot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "theplot");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "locations",
                schema: "theplot",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_locations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "screenplay_imports",
                schema: "theplot",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    screenplay_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_blob_name = table.Column<string>(type: "text", nullable: false),
                    blob_uploaded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    validated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    total_pages = table.Column<int>(type: "integer", nullable: true),
                    import_failed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    import_error_message = table.Column<string>(type: "text", nullable: true),
                    date_created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    date_last_modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_screenplay_imports", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "screenplays",
                schema: "theplot",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    authors = table.Column<string[]>(type: "text[]", nullable: false),
                    pdf_metadata = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    date_created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    date_last_modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    date_deleted = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_screenplays", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "voices",
                schema: "theplot",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_voices", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "screenplay_import_chunks",
                schema: "theplot",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    screenplay_import_id = table.Column<Guid>(type: "uuid", nullable: false),
                    start_page = table.Column<int>(type: "integer", nullable: false),
                    end_page = table.Column<int>(type: "integer", nullable: false),
                    split_status = table.Column<string>(type: "text", nullable: false),
                    split_completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    split_error_message = table.Column<string>(type: "text", nullable: true),
                    process_status = table.Column<string>(type: "text", nullable: false),
                    process_completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    process_error_message = table.Column<string>(type: "text", nullable: true),
                    date_created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    date_last_modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_screenplay_import_chunks", x => x.id);
                    table.ForeignKey(
                        name: "fk_screenplay_import_chunks_screenplay_imports_screenplay_impo",
                        column: x => x.screenplay_import_id,
                        principalSchema: "theplot",
                        principalTable: "screenplay_imports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "scenes",
                schema: "theplot",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    screenplay_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pdf_metadata = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    heading = table.Column<string>(type: "text", nullable: false),
                    interior_exterior = table.Column<string>(type: "text", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: true),
                    time_of_day = table.Column<string>(type: "text", nullable: true),
                    embedding = table.Column<Vector>(type: "vector(1536)", nullable: true),
                    date_created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    date_last_modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_scenes", x => x.id);
                    table.ForeignKey(
                        name: "fk_scenes_locations_location_id",
                        column: x => x.location_id,
                        principalSchema: "theplot",
                        principalTable: "locations",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_scenes_screenplays_screenplay_id",
                        column: x => x.screenplay_id,
                        principalSchema: "theplot",
                        principalTable: "screenplays",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "characters",
                schema: "theplot",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    aliases = table.Column<string[]>(type: "text[]", nullable: false),
                    voice_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_characters", x => x.id);
                    table.ForeignKey(
                        name: "fk_characters_voices_voice_id",
                        column: x => x.voice_id,
                        principalSchema: "theplot",
                        principalTable: "voices",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "scene_elements",
                schema: "theplot",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    scene_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pdf_metadata = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    sequence_order = table.Column<int>(type: "integer", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    content = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    character_id = table.Column<Guid>(type: "uuid", nullable: true),
                    embedding = table.Column<Vector>(type: "vector(1536)", nullable: true),
                    date_created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    date_last_modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_scene_elements", x => x.id);
                    table.ForeignKey(
                        name: "fk_scene_elements_characters_character_id",
                        column: x => x.character_id,
                        principalSchema: "theplot",
                        principalTable: "characters",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_scene_elements_scenes_scene_id",
                        column: x => x.scene_id,
                        principalSchema: "theplot",
                        principalTable: "scenes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_characters_voice_id",
                schema: "theplot",
                table: "characters",
                column: "voice_id");

            migrationBuilder.CreateIndex(
                name: "ix_scene_elements_character_id",
                schema: "theplot",
                table: "scene_elements",
                column: "character_id");

            migrationBuilder.CreateIndex(
                name: "ix_scene_elements_date_created",
                schema: "theplot",
                table: "scene_elements",
                column: "date_created");

            migrationBuilder.CreateIndex(
                name: "ix_scene_elements_scene_id",
                schema: "theplot",
                table: "scene_elements",
                column: "scene_id");

            migrationBuilder.CreateIndex(
                name: "ix_scenes_date_created",
                schema: "theplot",
                table: "scenes",
                column: "date_created");

            migrationBuilder.CreateIndex(
                name: "ix_scenes_location_id",
                schema: "theplot",
                table: "scenes",
                column: "location_id");

            migrationBuilder.CreateIndex(
                name: "ix_scenes_screenplay_id",
                schema: "theplot",
                table: "scenes",
                column: "screenplay_id");

            migrationBuilder.CreateIndex(
                name: "ix_screenplay_import_chunks_screenplay_import_id",
                schema: "theplot",
                table: "screenplay_import_chunks",
                column: "screenplay_import_id");

            migrationBuilder.CreateIndex(
                name: "ix_screenplay_import_chunks_screenplay_import_id_start_page",
                schema: "theplot",
                table: "screenplay_import_chunks",
                columns: new[] { "screenplay_import_id", "start_page" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_screenplay_imports_date_created",
                schema: "theplot",
                table: "screenplay_imports",
                column: "date_created");

            migrationBuilder.CreateIndex(
                name: "ix_screenplay_imports_screenplay_id",
                schema: "theplot",
                table: "screenplay_imports",
                column: "screenplay_id");

            migrationBuilder.CreateIndex(
                name: "ix_screenplay_imports_source_blob_name",
                schema: "theplot",
                table: "screenplay_imports",
                column: "source_blob_name");

            migrationBuilder.CreateIndex(
                name: "ix_screenplays_date_created",
                schema: "theplot",
                table: "screenplays",
                column: "date_created");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "scene_elements",
                schema: "theplot");

            migrationBuilder.DropTable(
                name: "screenplay_import_chunks",
                schema: "theplot");

            migrationBuilder.DropTable(
                name: "characters",
                schema: "theplot");

            migrationBuilder.DropTable(
                name: "scenes",
                schema: "theplot");

            migrationBuilder.DropTable(
                name: "screenplay_imports",
                schema: "theplot");

            migrationBuilder.DropTable(
                name: "voices",
                schema: "theplot");

            migrationBuilder.DropTable(
                name: "locations",
                schema: "theplot");

            migrationBuilder.DropTable(
                name: "screenplays",
                schema: "theplot");
        }
    }
}

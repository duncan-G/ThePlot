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
                    description = table.Column<string>(type: "text", nullable: false),
                    screenplay_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    name_normalized = table.Column<string>(type: "text", nullable: false),
                    role = table.Column<string>(type: "text", nullable: false),
                    audio_blob_uri = table.Column<string>(type: "text", nullable: true),
                    audio_mime_type = table.Column<string>(type: "text", nullable: true),
                    audio_metadata = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    embedding = table.Column<Vector>(type: "vector(1024)", nullable: true),
                    date_created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    date_last_modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_voices", x => x.id);
                    table.ForeignKey(
                        name: "fk_voices_screenplays_screenplay_id",
                        column: x => x.screenplay_id,
                        principalSchema: "theplot",
                        principalTable: "screenplays",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "generation_runs",
                schema: "theplot",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    screenplay_id = table.Column<Guid>(type: "uuid", nullable: false),
                    phase = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    trace_parent = table.Column<string>(type: "text", nullable: true),
                    date_created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    date_last_modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_generation_runs", x => x.id);
                    table.ForeignKey(
                        name: "fk_generation_runs_screenplays_screenplay_id",
                        column: x => x.screenplay_id,
                        principalSchema: "theplot",
                        principalTable: "screenplays",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "generated_artifacts",
                schema: "theplot",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    generation_node_id = table.Column<Guid>(type: "uuid", nullable: false),
                    generation_attempt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_current = table.Column<bool>(type: "boolean", nullable: false),
                    storage_uri = table.Column<string>(type: "text", nullable: true),
                    mime_type = table.Column<string>(type: "text", nullable: true),
                    content_hash = table.Column<string>(type: "text", nullable: true),
                    metadata = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    date_created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    date_last_modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_generated_artifacts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "generation_attempts",
                schema: "theplot",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    generation_node_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attempt_number = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    provider_request_json = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    provider_response_json = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    input_text_tokens = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    input_audio_tokens = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    output_text_tokens = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    output_audio_tokens = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    cost = table.Column<decimal>(type: "numeric", nullable: false, defaultValue: 0m),
                    started_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    date_created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    date_last_modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_generation_attempts", x => x.id);
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
                name: "generation_nodes",
                schema: "theplot",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    generation_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    payload = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    analysis_result = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    lease_worker_id = table.Column<string>(type: "text", nullable: true),
                    lease_expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    runnable_after_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    current_attempt_id = table.Column<Guid>(type: "uuid", nullable: true),
                    retry_count = table.Column<int>(type: "integer", nullable: false),
                    last_error_message = table.Column<string>(type: "text", nullable: true),
                    date_created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    date_last_modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_generation_nodes", x => x.id);
                    table.ForeignKey(
                        name: "fk_generation_nodes_generation_attempts_current_attempt_id",
                        column: x => x.current_attempt_id,
                        principalSchema: "theplot",
                        principalTable: "generation_attempts",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_generation_nodes_generation_runs_generation_run_id",
                        column: x => x.generation_run_id,
                        principalSchema: "theplot",
                        principalTable: "generation_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "generation_edges",
                schema: "theplot",
                columns: table => new
                {
                    from_node_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_node_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_generation_edges", x => new { x.from_node_id, x.to_node_id });
                    table.ForeignKey(
                        name: "fk_generation_edges_generation_nodes_from_node_id",
                        column: x => x.from_node_id,
                        principalSchema: "theplot",
                        principalTable: "generation_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_generation_edges_generation_nodes_to_node_id",
                        column: x => x.to_node_id,
                        principalSchema: "theplot",
                        principalTable: "generation_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
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
                name: "ix_generated_artifacts_generation_attempt_id",
                schema: "theplot",
                table: "generated_artifacts",
                column: "generation_attempt_id");

            migrationBuilder.CreateIndex(
                name: "ix_generated_artifacts_generation_node_id_is_current",
                schema: "theplot",
                table: "generated_artifacts",
                columns: new[] { "generation_node_id", "is_current" });

            migrationBuilder.CreateIndex(
                name: "ix_generation_attempts_generation_node_id",
                schema: "theplot",
                table: "generation_attempts",
                column: "generation_node_id");

            migrationBuilder.CreateIndex(
                name: "ix_generation_attempts_generation_node_id_attempt_number",
                schema: "theplot",
                table: "generation_attempts",
                columns: new[] { "generation_node_id", "attempt_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_generation_edges_to_node_id",
                schema: "theplot",
                table: "generation_edges",
                column: "to_node_id");

            migrationBuilder.CreateIndex(
                name: "ix_generation_nodes_current_attempt_id",
                schema: "theplot",
                table: "generation_nodes",
                column: "current_attempt_id");

            migrationBuilder.CreateIndex(
                name: "ix_generation_nodes_generation_run_id",
                schema: "theplot",
                table: "generation_nodes",
                column: "generation_run_id");

            migrationBuilder.CreateIndex(
                name: "ix_generation_nodes_generation_run_id_status_runnable_after_utc",
                schema: "theplot",
                table: "generation_nodes",
                columns: new[] { "generation_run_id", "status", "runnable_after_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_generation_runs_screenplay_id",
                schema: "theplot",
                table: "generation_runs",
                column: "screenplay_id");

            migrationBuilder.CreateIndex(
                name: "ix_generation_runs_screenplay_id_status",
                schema: "theplot",
                table: "generation_runs",
                columns: new[] { "screenplay_id", "status" });

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

            migrationBuilder.CreateIndex(
                name: "ix_voices_screenplay_id",
                schema: "theplot",
                table: "voices",
                column: "screenplay_id");

            migrationBuilder.CreateIndex(
                name: "ix_voices_screenplay_id_name_normalized",
                schema: "theplot",
                table: "voices",
                columns: new[] { "screenplay_id", "name_normalized" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_generated_artifacts_generation_attempts_generation_attempt_",
                schema: "theplot",
                table: "generated_artifacts",
                column: "generation_attempt_id",
                principalSchema: "theplot",
                principalTable: "generation_attempts",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_generated_artifacts_generation_nodes_generation_node_id",
                schema: "theplot",
                table: "generated_artifacts",
                column: "generation_node_id",
                principalSchema: "theplot",
                principalTable: "generation_nodes",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_generation_attempts_generation_nodes_generation_node_id",
                schema: "theplot",
                table: "generation_attempts",
                column: "generation_node_id",
                principalSchema: "theplot",
                principalTable: "generation_nodes",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_generation_nodes_generation_attempts_current_attempt_id",
                schema: "theplot",
                table: "generation_nodes");

            migrationBuilder.DropTable(
                name: "generated_artifacts",
                schema: "theplot");

            migrationBuilder.DropTable(
                name: "generation_edges",
                schema: "theplot");

            migrationBuilder.DropTable(
                name: "scene_elements",
                schema: "theplot");

            migrationBuilder.DropTable(
                name: "screenplay_import_chunks",
                schema: "theplot");

            migrationBuilder.DropTable(
                name: "generation_attempts",
                schema: "theplot");

            migrationBuilder.DropTable(
                name: "generation_nodes",
                schema: "theplot");

            migrationBuilder.DropTable(
                name: "characters",
                schema: "theplot");

            migrationBuilder.DropTable(
                name: "scenes",
                schema: "theplot");

            migrationBuilder.DropTable(
                name: "generation_runs",
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

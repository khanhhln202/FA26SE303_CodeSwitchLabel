using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CodeSwitchLabel.Repositories.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ErdV2Schema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rejection_reason",
                columns: table => new
                {
                    reason_id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    reason_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    category = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rejection_reason", x => x.reason_id);
                });

            migrationBuilder.CreateTable(
                name: "role",
                columns: table => new
                {
                    role_id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    role_name = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role", x => x.role_id);
                });

            migrationBuilder.CreateTable(
                name: "script_error_reason",
                columns: table => new
                {
                    reason_id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    reason_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_script_error_reason", x => x.reason_id);
                });

            migrationBuilder.CreateTable(
                name: "app_user",
                columns: table => new
                {
                    user_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    role_id = table.Column<short>(type: "smallint", nullable: false),
                    full_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_app_user", x => x.user_id);
                    table.ForeignKey(
                        name: "fk_app_user_role_role_id",
                        column: x => x.role_id,
                        principalTable: "role",
                        principalColumn: "role_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "audit_log",
                columns: table => new
                {
                    audit_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    user_id = table.Column<long>(type: "bigint", nullable: true),
                    entity_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    entity_id = table.Column<long>(type: "bigint", nullable: false),
                    action = table.Column<int>(type: "integer", nullable: false),
                    old_value = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    new_value = table.Column<JsonDocument>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_log", x => new { x.audit_id, x.changed_at });
                    table.ForeignKey(
                        name: "fk_audit_log_app_user_user_id",
                        column: x => x.user_id,
                        principalTable: "app_user",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "dataset",
                columns: table => new
                {
                    dataset_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    dataset_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    version = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    filter_criteria = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    released_by_id = table.Column<long>(type: "bigint", nullable: true),
                    released_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    file_key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    file_format = table.Column<int>(type: "integer", nullable: false),
                    row_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dataset", x => x.dataset_id);
                    table.CheckConstraint("ck_dataset_release", "status <> 1 OR (released_by_id IS NOT NULL AND released_at IS NOT NULL AND file_key IS NOT NULL)");
                    table.CheckConstraint("ck_dataset_row_count", "row_count >= 0");
                    table.ForeignKey(
                        name: "fk_dataset_app_user_released_by_id",
                        column: x => x.released_by_id,
                        principalTable: "app_user",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "import_batch",
                columns: table => new
                {
                    batch_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    imported_by_id = table.Column<long>(type: "bigint", nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    row_count = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_import_batch", x => x.batch_id);
                    table.CheckConstraint("ck_import_batch_row_count", "row_count >= 0");
                    table.ForeignKey(
                        name: "fk_import_batch_app_user_imported_by_id",
                        column: x => x.imported_by_id,
                        principalTable: "app_user",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notification",
                columns: table => new
                {
                    notification_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    recipient_id = table.Column<long>(type: "bigint", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    payload = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification", x => x.notification_id);
                    table.ForeignKey(
                        name: "fk_notification_app_user_recipient_id",
                        column: x => x.recipient_id,
                        principalTable: "app_user",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "speaker_profile",
                columns: table => new
                {
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    birth_year = table.Column<int>(type: "integer", nullable: true),
                    region = table.Column<int>(type: "integer", nullable: true),
                    province = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    english_level = table.Column<int>(type: "integer", nullable: true),
                    occupation = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_speaker_profile", x => x.user_id);
                    table.CheckConstraint("ck_speaker_profile_birth_year", "birth_year IS NULL OR (birth_year >= 1900 AND birth_year <= EXTRACT(YEAR FROM now()))");
                    table.ForeignKey(
                        name: "fk_speaker_profile_app_user_user_id",
                        column: x => x.user_id,
                        principalTable: "app_user",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "system_config",
                columns: table => new
                {
                    config_id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    config_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    config_value = table.Column<string>(type: "text", nullable: false),
                    value_type = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    updated_by_id = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_system_config", x => x.config_id);
                    table.ForeignKey(
                        name: "fk_system_config_app_user_updated_by_id",
                        column: x => x.updated_by_id,
                        principalTable: "app_user",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "task",
                columns: table => new
                {
                    task_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    created_by_id = table.Column<long>(type: "bigint", nullable: false),
                    task_type = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    target_qty = table.Column<int>(type: "integer", nullable: false),
                    deadline = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_task", x => x.task_id);
                    table.CheckConstraint("ck_task_target_qty", "target_qty > 0");
                    table.ForeignKey(
                        name: "fk_task_app_user_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "app_user",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "script",
                columns: table => new
                {
                    script_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    content = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    word_count = table.Column<int>(type: "integer", nullable: false),
                    en_word_count = table.Column<int>(type: "integer", nullable: false),
                    domain = table.Column<int>(type: "integer", nullable: false),
                    created_by_id = table.Column<long>(type: "bigint", nullable: false),
                    import_batch_id = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_script", x => x.script_id);
                    table.CheckConstraint("ck_script_en_not_exceed_total", "en_word_count <= word_count");
                    table.CheckConstraint("ck_script_en_word_count", "en_word_count >= 0");
                    table.CheckConstraint("ck_script_word_count", "word_count >= 0");
                    table.ForeignKey(
                        name: "fk_script_app_user_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "app_user",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_script_import_batch_import_batch_id",
                        column: x => x.import_batch_id,
                        principalTable: "import_batch",
                        principalColumn: "batch_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "task_assignment",
                columns: table => new
                {
                    assignment_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    task_id = table.Column<long>(type: "bigint", nullable: false),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    assigned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    assignment_status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_task_assignment", x => x.assignment_id);
                    table.ForeignKey(
                        name: "fk_task_assignment_app_user_user_id",
                        column: x => x.user_id,
                        principalTable: "app_user",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_task_assignment_task_task_id",
                        column: x => x.task_id,
                        principalTable: "task",
                        principalColumn: "task_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recording",
                columns: table => new
                {
                    recording_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    script_id = table.Column<long>(type: "bigint", nullable: false),
                    speaker_id = table.Column<long>(type: "bigint", nullable: false),
                    task_id = table.Column<long>(type: "bigint", nullable: true),
                    s3_key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    audio_format = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "wav"),
                    status = table.Column<int>(type: "integer", nullable: false),
                    duration_sec = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recording", x => x.recording_id);
                    table.CheckConstraint("ck_recording_duration", "duration_sec > 0");
                    table.ForeignKey(
                        name: "fk_recording_app_user_speaker_id",
                        column: x => x.speaker_id,
                        principalTable: "app_user",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_recording_script_script_id",
                        column: x => x.script_id,
                        principalTable: "script",
                        principalColumn: "script_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_recording_task_task_id",
                        column: x => x.task_id,
                        principalTable: "task",
                        principalColumn: "task_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "script_review",
                columns: table => new
                {
                    script_review_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    script_id = table.Column<long>(type: "bigint", nullable: false),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    error_reason_id = table.Column<short>(type: "smallint", nullable: true),
                    action = table.Column<int>(type: "integer", nullable: false),
                    edited_content = table.Column<string>(type: "text", nullable: true),
                    comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_script_review", x => x.script_review_id);
                    table.CheckConstraint("ck_script_review_edited", "(action = 1) = (edited_content IS NOT NULL)");
                    table.CheckConstraint("ck_script_review_reason", "action <> 2 OR error_reason_id IS NOT NULL");
                    table.ForeignKey(
                        name: "fk_script_review_app_user_user_id",
                        column: x => x.user_id,
                        principalTable: "app_user",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_script_review_script_error_reason_error_reason_id",
                        column: x => x.error_reason_id,
                        principalTable: "script_error_reason",
                        principalColumn: "reason_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_script_review_script_script_id",
                        column: x => x.script_id,
                        principalTable: "script",
                        principalColumn: "script_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "script_skip",
                columns: table => new
                {
                    skip_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    script_id = table.Column<long>(type: "bigint", nullable: false),
                    speaker_id = table.Column<long>(type: "bigint", nullable: false),
                    task_id = table.Column<long>(type: "bigint", nullable: true),
                    skipped_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_script_skip", x => x.skip_id);
                    table.ForeignKey(
                        name: "fk_script_skip_app_user_speaker_id",
                        column: x => x.speaker_id,
                        principalTable: "app_user",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_script_skip_script_script_id",
                        column: x => x.script_id,
                        principalTable: "script",
                        principalColumn: "script_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_script_skip_task_task_id",
                        column: x => x.task_id,
                        principalTable: "task",
                        principalColumn: "task_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "task_script",
                columns: table => new
                {
                    task_id = table.Column<long>(type: "bigint", nullable: false),
                    script_id = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    included_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_task_script", x => new { x.task_id, x.script_id });
                    table.ForeignKey(
                        name: "fk_task_script_script_script_id",
                        column: x => x.script_id,
                        principalTable: "script",
                        principalColumn: "script_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_task_script_task_task_id",
                        column: x => x.task_id,
                        principalTable: "task",
                        principalColumn: "task_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "dataset_recording",
                columns: table => new
                {
                    dataset_id = table.Column<long>(type: "bigint", nullable: false),
                    recording_id = table.Column<long>(type: "bigint", nullable: false),
                    included_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dataset_recording", x => new { x.dataset_id, x.recording_id });
                    table.ForeignKey(
                        name: "fk_dataset_recording_dataset_dataset_id",
                        column: x => x.dataset_id,
                        principalTable: "dataset",
                        principalColumn: "dataset_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_dataset_recording_recording_recording_id",
                        column: x => x.recording_id,
                        principalTable: "recording",
                        principalColumn: "recording_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "review",
                columns: table => new
                {
                    review_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    recording_id = table.Column<long>(type: "bigint", nullable: false),
                    reviewer_id = table.Column<long>(type: "bigint", nullable: false),
                    task_id = table.Column<long>(type: "bigint", nullable: true),
                    review_round = table.Column<short>(type: "smallint", nullable: false),
                    decision = table.Column<int>(type: "integer", nullable: false),
                    is_blind = table.Column<bool>(type: "boolean", nullable: false),
                    comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_review", x => x.review_id);
                    table.CheckConstraint("ck_review_round", "review_round BETWEEN 1 AND 3");
                    table.ForeignKey(
                        name: "fk_review_app_user_reviewer_id",
                        column: x => x.reviewer_id,
                        principalTable: "app_user",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_review_recording_recording_id",
                        column: x => x.recording_id,
                        principalTable: "recording",
                        principalColumn: "recording_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_review_task_task_id",
                        column: x => x.task_id,
                        principalTable: "task",
                        principalColumn: "task_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "task_recording",
                columns: table => new
                {
                    task_id = table.Column<long>(type: "bigint", nullable: false),
                    recording_id = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    included_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_task_recording", x => new { x.task_id, x.recording_id });
                    table.ForeignKey(
                        name: "fk_task_recording_recording_recording_id",
                        column: x => x.recording_id,
                        principalTable: "recording",
                        principalColumn: "recording_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_task_recording_task_task_id",
                        column: x => x.task_id,
                        principalTable: "task",
                        principalColumn: "task_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "review_rejection_reason",
                columns: table => new
                {
                    review_id = table.Column<long>(type: "bigint", nullable: false),
                    reason_id = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_review_rejection_reason", x => new { x.review_id, x.reason_id });
                    table.ForeignKey(
                        name: "fk_review_rejection_reason_rejection_reason_reason_id",
                        column: x => x.reason_id,
                        principalTable: "rejection_reason",
                        principalColumn: "reason_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_review_rejection_reason_review_review_id",
                        column: x => x.review_id,
                        principalTable: "review",
                        principalColumn: "review_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_app_user_role",
                table: "app_user",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ux_app_user_email",
                table: "app_user",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_audit_entity",
                table: "audit_log",
                columns: new[] { "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_user",
                table: "audit_log",
                columns: new[] { "user_id", "changed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_dataset_released_by",
                table: "dataset",
                column: "released_by_id");

            migrationBuilder.CreateIndex(
                name: "ux_dataset_file_key",
                table: "dataset",
                column: "file_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_dataset_name_version",
                table: "dataset",
                columns: new[] { "dataset_name", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_dataset_recording_recording",
                table: "dataset_recording",
                column: "recording_id");

            migrationBuilder.CreateIndex(
                name: "ix_import_batch_imported_by_id",
                table: "import_batch",
                column: "imported_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_notification_unread",
                table: "notification",
                columns: new[] { "recipient_id", "created_at" },
                filter: "read_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_recording_pending",
                table: "recording",
                column: "recorded_at",
                filter: "status = 1");

            migrationBuilder.CreateIndex(
                name: "ix_recording_script",
                table: "recording",
                column: "script_id");

            migrationBuilder.CreateIndex(
                name: "ix_recording_speaker",
                table: "recording",
                column: "speaker_id");

            migrationBuilder.CreateIndex(
                name: "ix_recording_task",
                table: "recording",
                column: "task_id");

            migrationBuilder.CreateIndex(
                name: "ux_recording_s3_key",
                table: "recording",
                column: "s3_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_rejection_reason_code",
                table: "rejection_reason",
                column: "reason_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_review_rejected",
                table: "review",
                column: "recording_id",
                filter: "decision = 1");

            migrationBuilder.CreateIndex(
                name: "ix_review_reviewer",
                table: "review",
                column: "reviewer_id");

            migrationBuilder.CreateIndex(
                name: "ix_review_task",
                table: "review",
                column: "task_id");

            migrationBuilder.CreateIndex(
                name: "ux_review_recording_reviewer_round",
                table: "review",
                columns: new[] { "recording_id", "reviewer_id", "review_round" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_review_rejection_reason_reason",
                table: "review_rejection_reason",
                column: "reason_id");

            migrationBuilder.CreateIndex(
                name: "ux_role_name",
                table: "role",
                column: "role_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_script_created_by",
                table: "script",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_script_import_batch_id",
                table: "script",
                column: "import_batch_id");

            migrationBuilder.CreateIndex(
                name: "ix_script_pending",
                table: "script",
                column: "created_at",
                filter: "status = 0");

            migrationBuilder.CreateIndex(
                name: "ix_script_status_domain",
                table: "script",
                columns: new[] { "status", "domain" });

            migrationBuilder.CreateIndex(
                name: "ux_script_error_reason_code",
                table: "script_error_reason",
                column: "reason_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_script_review_error_reason_id",
                table: "script_review",
                column: "error_reason_id");

            migrationBuilder.CreateIndex(
                name: "ix_script_review_script",
                table: "script_review",
                column: "script_id");

            migrationBuilder.CreateIndex(
                name: "ix_script_review_user",
                table: "script_review",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_script_skip_speaker_id",
                table: "script_skip",
                column: "speaker_id");

            migrationBuilder.CreateIndex(
                name: "ix_script_skip_task_id",
                table: "script_skip",
                column: "task_id");

            migrationBuilder.CreateIndex(
                name: "ux_script_skip_speaker",
                table: "script_skip",
                columns: new[] { "script_id", "speaker_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_system_config_updated_by_id",
                table: "system_config",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ux_system_config_key",
                table: "system_config",
                column: "config_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_task_created_by",
                table: "task",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_task_open_deadline",
                table: "task",
                column: "deadline",
                filter: "status IN (1, 2)");

            migrationBuilder.CreateIndex(
                name: "ix_task_assignment_user",
                table: "task_assignment",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ux_task_active_assignment",
                table: "task_assignment",
                column: "task_id",
                unique: true,
                filter: "assignment_status = 0");

            migrationBuilder.CreateIndex(
                name: "ix_task_recording_recording",
                table: "task_recording",
                column: "recording_id");

            migrationBuilder.CreateIndex(
                name: "ix_task_script_script",
                table: "task_script",
                column: "script_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_log");

            migrationBuilder.DropTable(
                name: "dataset_recording");

            migrationBuilder.DropTable(
                name: "notification");

            migrationBuilder.DropTable(
                name: "review_rejection_reason");

            migrationBuilder.DropTable(
                name: "script_review");

            migrationBuilder.DropTable(
                name: "script_skip");

            migrationBuilder.DropTable(
                name: "speaker_profile");

            migrationBuilder.DropTable(
                name: "system_config");

            migrationBuilder.DropTable(
                name: "task_assignment");

            migrationBuilder.DropTable(
                name: "task_recording");

            migrationBuilder.DropTable(
                name: "task_script");

            migrationBuilder.DropTable(
                name: "dataset");

            migrationBuilder.DropTable(
                name: "rejection_reason");

            migrationBuilder.DropTable(
                name: "review");

            migrationBuilder.DropTable(
                name: "script_error_reason");

            migrationBuilder.DropTable(
                name: "recording");

            migrationBuilder.DropTable(
                name: "script");

            migrationBuilder.DropTable(
                name: "task");

            migrationBuilder.DropTable(
                name: "import_batch");

            migrationBuilder.DropTable(
                name: "app_user");

            migrationBuilder.DropTable(
                name: "role");
        }
    }
}

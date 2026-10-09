using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConntectLive.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    code = table.Column<string>(type: "TEXT", maxLength: 8, nullable: true),
                    title = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    hostuserid = table.Column<Guid>(name: "host-user-id", type: "TEXT", nullable: false),
                    status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    currentquestionindex = table.Column<int>(name: "current-question-index", type: "INTEGER", nullable: false),
                    createdat = table.Column<DateTime>(name: "created-at", type: "TEXT", nullable: false),
                    startedat = table.Column<DateTime>(name: "started-at", type: "TEXT", nullable: true),
                    endedat = table.Column<DateTime>(name: "ended-at", type: "TEXT", nullable: true),
                    version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sessions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "session_questions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    sessionid = table.Column<Guid>(name: "session-id", type: "TEXT", nullable: false),
                    questionid = table.Column<Guid>(name: "question-id", type: "TEXT", nullable: false),
                    order = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_questions", x => x.id);
                    table.ForeignKey(
                        name: "FK_session_questions_sessions_session-id",
                        column: x => x.sessionid,
                        principalTable: "sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_session_questions_session-id_order",
                table: "session_questions",
                columns: new[] { "session-id", "order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sessions_code",
                table: "sessions",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "session_questions");

            migrationBuilder.DropTable(
                name: "sessions");
        }
    }
}

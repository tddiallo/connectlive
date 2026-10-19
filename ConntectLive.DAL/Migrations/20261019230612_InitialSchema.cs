using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConntectLive.DAL.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    hostuserid = table.Column<Guid>(name: "host-user-id", type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    currentquestionindex = table.Column<int>(name: "current-question-index", type: "integer", nullable: false),
                    createdat = table.Column<DateTime>(name: "created-at", type: "timestamp with time zone", nullable: false),
                    startedat = table.Column<DateTime>(name: "started-at", type: "timestamp with time zone", nullable: true),
                    endedat = table.Column<DateTime>(name: "ended-at", type: "timestamp with time zone", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sessions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    firstname = table.Column<string>(type: "text", nullable: true),
                    lastname = table.Column<string>(type: "text", nullable: true),
                    email = table.Column<string>(type: "text", nullable: true),
                    nickname = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "session_answers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sessionid = table.Column<Guid>(name: "session-id", type: "uuid", nullable: false),
                    questionid = table.Column<Guid>(name: "question-id", type: "uuid", nullable: false),
                    userid = table.Column<Guid>(name: "user-id", type: "uuid", nullable: false),
                    answer = table.Column<string>(type: "text", nullable: true),
                    iscorrect = table.Column<bool>(name: "is-correct", type: "boolean", nullable: false),
                    points = table.Column<int>(type: "integer", nullable: false),
                    answeredat = table.Column<DateTime>(name: "answered-at", type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_answers", x => x.id);
                    table.ForeignKey(
                        name: "FK_session_answers_sessions_session-id",
                        column: x => x.sessionid,
                        principalTable: "sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "session_questions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sessionid = table.Column<Guid>(name: "session-id", type: "uuid", nullable: false),
                    questionid = table.Column<Guid>(name: "question-id", type: "uuid", nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "questions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    question = table.Column<string>(type: "text", nullable: true),
                    category = table.Column<string>(type: "text", nullable: true),
                    choices = table.Column<List<string>>(type: "text[]", nullable: true),
                    correctanswer = table.Column<string>(name: "correct-answer", type: "text", nullable: true),
                    UserEntityId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_questions", x => x.id);
                    table.ForeignKey(
                        name: "FK_questions_users_UserEntityId",
                        column: x => x.UserEntityId,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_questions_UserEntityId",
                table: "questions",
                column: "UserEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_session_answers_session-id_question-id_user-id",
                table: "session_answers",
                columns: new[] { "session-id", "question-id", "user-id" },
                unique: true);

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

            migrationBuilder.CreateIndex(
                name: "IX_users_email",
                table: "users",
                column: "email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "questions");

            migrationBuilder.DropTable(
                name: "session_answers");

            migrationBuilder.DropTable(
                name: "session_questions");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "sessions");
        }
    }
}

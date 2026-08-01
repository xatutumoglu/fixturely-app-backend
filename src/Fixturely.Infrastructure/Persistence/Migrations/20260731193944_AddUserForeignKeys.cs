using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fixturely.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_TournamentInvitations_AcceptedByUserId",
                table: "TournamentInvitations",
                column: "AcceptedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_AuditLogs_AspNetUsers_UserId",
                table: "AuditLogs",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_EmailDeliveryEvents_AspNetUsers_UserId",
                table: "EmailDeliveryEvents",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_RefreshTokens_AspNetUsers_UserId",
                table: "RefreshTokens",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TournamentInvitations_AspNetUsers_AcceptedByUserId",
                table: "TournamentInvitations",
                column: "AcceptedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Tournaments_AspNetUsers_OwnerUserId",
                table: "Tournaments",
                column: "OwnerUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserSessions_AspNetUsers_UserId",
                table: "UserSessions",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // Two references below cannot carry their own cascading EF-level FK,
            // because SQL Server rejects a second cascading path from AspNetUsers
            // into a table that is already reachable via another cascading path
            // (TournamentMembers: via Tournaments.OwnerUserId -> Tournaments ->
            // TournamentMembers; TournamentInvitations.InvitedByUserId: because
            // AcceptedByUserId already owns the one cascading path SQL Server
            // allows into that table from AspNetUsers). This trigger performs
            // the equivalent cleanup for both:
            //   - TournamentMembers rows for a deleted user who was a plain
            //     (non-owner) member of a tournament owned by someone else.
            //     Rows belonging to tournaments the deleted user owned are
            //     already gone by the time this trigger runs (removed by the
            //     Tournaments.OwnerUserId cascade), so this is a no-op for those.
            //   - TournamentInvitations rows the deleted user sent as the
            //     inviter (a pending invitation from a now-deleted account
            //     should not remain actionable).
            migrationBuilder.Sql(
                """
                CREATE TRIGGER TR_AspNetUsers_CleanupTournamentMembers
                ON AspNetUsers
                AFTER DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    DELETE tm
                    FROM TournamentMembers AS tm
                    INNER JOIN deleted AS d ON tm.UserId = d.Id;

                    DELETE ti
                    FROM TournamentInvitations AS ti
                    INNER JOIN deleted AS d ON ti.InvitedByUserId = d.Id;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER TR_AspNetUsers_CleanupTournamentMembers");

            migrationBuilder.DropForeignKey(
                name: "FK_AuditLogs_AspNetUsers_UserId",
                table: "AuditLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_EmailDeliveryEvents_AspNetUsers_UserId",
                table: "EmailDeliveryEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_RefreshTokens_AspNetUsers_UserId",
                table: "RefreshTokens");

            migrationBuilder.DropForeignKey(
                name: "FK_TournamentInvitations_AspNetUsers_AcceptedByUserId",
                table: "TournamentInvitations");

            migrationBuilder.DropForeignKey(
                name: "FK_Tournaments_AspNetUsers_OwnerUserId",
                table: "Tournaments");

            migrationBuilder.DropForeignKey(
                name: "FK_UserSessions_AspNetUsers_UserId",
                table: "UserSessions");

            migrationBuilder.DropIndex(
                name: "IX_TournamentInvitations_AcceptedByUserId",
                table: "TournamentInvitations");
        }
    }
}

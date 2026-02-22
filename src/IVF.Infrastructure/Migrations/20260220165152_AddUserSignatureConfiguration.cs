using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IVF.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserSignatureConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserSignatures_users_UserId",
                table: "UserSignatures");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserSignatures",
                table: "UserSignatures");

            migrationBuilder.RenameTable(
                name: "UserSignatures",
                newName: "user_signatures");

            migrationBuilder.RenameIndex(
                name: "IX_UserSignatures_UserId",
                table: "user_signatures",
                newName: "IX_user_signatures_UserId");

            migrationBuilder.AlterColumn<string>(
                name: "WorkerName",
                table: "user_signatures",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "KeystorePath",
                table: "user_signatures",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ImageMimeType",
                table: "user_signatures",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "image/png",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "CertificateSubject",
                table: "user_signatures",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CertificateSerialNumber",
                table: "user_signatures",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "CertStatus",
                table: "user_signatures",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddPrimaryKey(
                name: "PK_user_signatures",
                table: "user_signatures",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "menu_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Section = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SectionHeader = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Icon = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Route = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Permission = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AdminOnly = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_menu_items", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_menu_items_Route",
                table: "menu_items",
                column: "Route",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_menu_items_Section_SortOrder",
                table: "menu_items",
                columns: new[] { "Section", "SortOrder" });

            migrationBuilder.AddForeignKey(
                name: "FK_user_signatures_users_UserId",
                table: "user_signatures",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_user_signatures_users_UserId",
                table: "user_signatures");

            migrationBuilder.DropTable(
                name: "menu_items");

            migrationBuilder.DropPrimaryKey(
                name: "PK_user_signatures",
                table: "user_signatures");

            migrationBuilder.RenameTable(
                name: "user_signatures",
                newName: "UserSignatures");

            migrationBuilder.RenameIndex(
                name: "IX_user_signatures_UserId",
                table: "UserSignatures",
                newName: "IX_UserSignatures_UserId");

            migrationBuilder.AlterColumn<string>(
                name: "WorkerName",
                table: "UserSignatures",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "KeystorePath",
                table: "UserSignatures",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ImageMimeType",
                table: "UserSignatures",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldDefaultValue: "image/png");

            migrationBuilder.AlterColumn<string>(
                name: "CertificateSubject",
                table: "UserSignatures",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CertificateSerialNumber",
                table: "UserSignatures",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "CertStatus",
                table: "UserSignatures",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserSignatures",
                table: "UserSignatures",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_UserSignatures_users_UserId",
                table: "UserSignatures",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

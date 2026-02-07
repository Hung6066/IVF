using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IVF.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateSpermWashingSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SpermWashings_patients_PatientId",
                table: "SpermWashings");

            migrationBuilder.DropForeignKey(
                name: "FK_SpermWashings_treatment_cycles_CycleId",
                table: "SpermWashings");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SpermWashings",
                table: "SpermWashings");

            migrationBuilder.RenameTable(
                name: "SpermWashings",
                newName: "sperm_washings");

            migrationBuilder.RenameIndex(
                name: "IX_SpermWashings_PatientId",
                table: "sperm_washings",
                newName: "IX_sperm_washings_PatientId");

            migrationBuilder.RenameIndex(
                name: "IX_SpermWashings_CycleId",
                table: "sperm_washings",
                newName: "IX_sperm_washings_CycleId");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "sperm_washings",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<decimal>(
                name: "PreWashConcentration",
                table: "sperm_washings",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "PostWashMotility",
                table: "sperm_washings",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "PostWashConcentration",
                table: "sperm_washings",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "sperm_washings",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Method",
                table: "sperm_washings",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddPrimaryKey(
                name: "PK_sperm_washings",
                table: "sperm_washings",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_sperm_washings_WashDate",
                table: "sperm_washings",
                column: "WashDate");

            migrationBuilder.AddForeignKey(
                name: "FK_sperm_washings_patients_PatientId",
                table: "sperm_washings",
                column: "PatientId",
                principalTable: "patients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_sperm_washings_treatment_cycles_CycleId",
                table: "sperm_washings",
                column: "CycleId",
                principalTable: "treatment_cycles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_sperm_washings_patients_PatientId",
                table: "sperm_washings");

            migrationBuilder.DropForeignKey(
                name: "FK_sperm_washings_treatment_cycles_CycleId",
                table: "sperm_washings");

            migrationBuilder.DropPrimaryKey(
                name: "PK_sperm_washings",
                table: "sperm_washings");

            migrationBuilder.DropIndex(
                name: "IX_sperm_washings_WashDate",
                table: "sperm_washings");

            migrationBuilder.RenameTable(
                name: "sperm_washings",
                newName: "SpermWashings");

            migrationBuilder.RenameIndex(
                name: "IX_sperm_washings_PatientId",
                table: "SpermWashings",
                newName: "IX_SpermWashings_PatientId");

            migrationBuilder.RenameIndex(
                name: "IX_sperm_washings_CycleId",
                table: "SpermWashings",
                newName: "IX_SpermWashings_CycleId");

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "SpermWashings",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<decimal>(
                name: "PreWashConcentration",
                table: "SpermWashings",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(10,2)",
                oldPrecision: 10,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "PostWashMotility",
                table: "SpermWashings",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(5,2)",
                oldPrecision: 5,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "PostWashConcentration",
                table: "SpermWashings",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(10,2)",
                oldPrecision: 10,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "SpermWashings",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Method",
                table: "SpermWashings",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AddPrimaryKey(
                name: "PK_SpermWashings",
                table: "SpermWashings",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SpermWashings_patients_PatientId",
                table: "SpermWashings",
                column: "PatientId",
                principalTable: "patients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SpermWashings_treatment_cycles_CycleId",
                table: "SpermWashings",
                column: "CycleId",
                principalTable: "treatment_cycles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

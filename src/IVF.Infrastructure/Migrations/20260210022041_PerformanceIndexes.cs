using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IVF.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_treatment_cycles_CurrentPhase",
                table: "treatment_cycles",
                column: "CurrentPhase");

            migrationBuilder.CreateIndex(
                name: "IX_treatment_cycles_StartDate",
                table: "treatment_cycles",
                column: "StartDate");

            migrationBuilder.CreateIndex(
                name: "IX_queue_tickets_Status",
                table: "queue_tickets",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_queue_tickets_Status_DepartmentCode_IssuedAt",
                table: "queue_tickets",
                columns: new[] { "Status", "DepartmentCode", "IssuedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_patients_FullName",
                table: "patients",
                column: "FullName");

            migrationBuilder.CreateIndex(
                name: "IX_form_responses_PatientId_FormTemplateId",
                table: "form_responses",
                columns: new[] { "PatientId", "FormTemplateId" });

            migrationBuilder.CreateIndex(
                name: "IX_appointments_DoctorId_ScheduledAt",
                table: "appointments",
                columns: new[] { "DoctorId", "ScheduledAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_treatment_cycles_CurrentPhase",
                table: "treatment_cycles");

            migrationBuilder.DropIndex(
                name: "IX_treatment_cycles_StartDate",
                table: "treatment_cycles");

            migrationBuilder.DropIndex(
                name: "IX_queue_tickets_Status",
                table: "queue_tickets");

            migrationBuilder.DropIndex(
                name: "IX_queue_tickets_Status_DepartmentCode_IssuedAt",
                table: "queue_tickets");

            migrationBuilder.DropIndex(
                name: "IX_patients_FullName",
                table: "patients");

            migrationBuilder.DropIndex(
                name: "IX_form_responses_PatientId_FormTemplateId",
                table: "form_responses");

            migrationBuilder.DropIndex(
                name: "IX_appointments_DoctorId_ScheduledAt",
                table: "appointments");
        }
    }
}

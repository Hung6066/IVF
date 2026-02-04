using IVF.Domain.Entities;
using IVF.Domain.Enums;

namespace IVF.Application.Common.Interfaces;

public interface IAppointmentRepository
{
    Task<Appointment?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Appointment>> GetByPatientAsync(Guid patientId, CancellationToken ct = default);
    Task<IReadOnlyList<Appointment>> GetByDoctorAsync(Guid doctorId, DateTime date, CancellationToken ct = default);
    Task<IReadOnlyList<Appointment>> GetByDateRangeAsync(DateTime start, DateTime end, CancellationToken ct = default);
    Task<IReadOnlyList<Appointment>> GetTodayAppointmentsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Appointment>> GetUpcomingAsync(Guid? doctorId = null, int days = 7, CancellationToken ct = default);
    Task<bool> HasConflictAsync(Guid doctorId, DateTime scheduledAt, int durationMinutes, Guid? excludeAppointmentId = null, CancellationToken ct = default);
    Task<Appointment> AddAsync(Appointment appointment, CancellationToken ct = default);
    Task UpdateAsync(Appointment appointment, CancellationToken ct = default);
}

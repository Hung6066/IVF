using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Domain.Enums;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.Infrastructure.Repositories;

public class AppointmentRepository : IAppointmentRepository
{
    private readonly IvfDbContext _context;
    
    public AppointmentRepository(IvfDbContext context) => _context = context;

    public async Task<Appointment?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Appointments
            .Include(a => a.Patient)
            .Include(a => a.Doctor).ThenInclude(d => d!.User)
            .Include(a => a.Cycle)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IReadOnlyList<Appointment>> GetByPatientAsync(Guid patientId, CancellationToken ct = default)
        => await _context.Appointments
            .Include(a => a.Doctor).ThenInclude(d => d!.User)
            .Where(a => a.PatientId == patientId)
            .OrderByDescending(a => a.ScheduledAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Appointment>> GetByDoctorAsync(Guid doctorId, DateTime date, CancellationToken ct = default)
    {
        // Use range to capture full day based on client provided date (which implies start of their day)
        var nextDay = date.AddDays(1);
        return await _context.Appointments
            .Include(a => a.Patient)
            .Where(a => a.DoctorId == doctorId && a.ScheduledAt >= date && a.ScheduledAt < nextDay)
            .OrderBy(a => a.ScheduledAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Appointment>> GetByDateRangeAsync(DateTime start, DateTime end, CancellationToken ct = default)
        => await _context.Appointments
            .Include(a => a.Patient)
            .Include(a => a.Doctor).ThenInclude(d => d!.User)
            .Where(a => a.ScheduledAt >= start && a.ScheduledAt <= end)
            .OrderBy(a => a.ScheduledAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Appointment>> GetTodayAppointmentsAsync(CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;
        return await _context.Appointments
            .Include(a => a.Patient)
            .Include(a => a.Doctor).ThenInclude(d => d!.User)
            .Where(a => a.ScheduledAt.Date == today)
            .OrderBy(a => a.ScheduledAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Appointment>> GetUpcomingAsync(Guid? doctorId = null, int days = 7, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var end = now.AddDays(days);
        var query = _context.Appointments
            .Include(a => a.Patient)
            .Include(a => a.Doctor).ThenInclude(d => d!.User)
            .Where(a => a.ScheduledAt >= now && a.ScheduledAt <= end && 
                        a.Status != AppointmentStatus.Cancelled && a.Status != AppointmentStatus.Completed);
        
        if (doctorId.HasValue)
            query = query.Where(a => a.DoctorId == doctorId);
        
        return await query.OrderBy(a => a.ScheduledAt).ToListAsync(ct);
    }

    public async Task<bool> HasConflictAsync(Guid doctorId, DateTime scheduledAt, int durationMinutes, Guid? excludeAppointmentId = null, CancellationToken ct = default)
    {
        var endTime = scheduledAt.AddMinutes(durationMinutes);
        var query = _context.Appointments
            .Where(a => a.DoctorId == doctorId &&
                        a.Status != AppointmentStatus.Cancelled &&
                        ((a.ScheduledAt >= scheduledAt && a.ScheduledAt < endTime) ||
                         (a.ScheduledAt.AddMinutes(a.DurationMinutes) > scheduledAt && a.ScheduledAt < endTime)));
        
        if (excludeAppointmentId.HasValue)
            query = query.Where(a => a.Id != excludeAppointmentId);
        
        return await query.AnyAsync(ct);
    }

    public async Task<Appointment> AddAsync(Appointment appointment, CancellationToken ct = default)
    {
        await _context.Appointments.AddAsync(appointment, ct);
        return appointment;
    }

    public Task UpdateAsync(Appointment appointment, CancellationToken ct = default)
    {
        _context.Appointments.Update(appointment);
        return Task.CompletedTask;
    }
}

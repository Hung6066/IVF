using IVF.Domain.Entities;

namespace IVF.Application.Common.Interfaces;

// Repository interfaces (DDD pattern)
public interface IPatientRepository
{
    Task<Patient?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Patient?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<(IReadOnlyList<Patient> Items, int Total)> SearchAsync(string? query, int page, int pageSize, CancellationToken ct = default);
    Task<Patient> AddAsync(Patient patient, CancellationToken ct = default);
    Task UpdateAsync(Patient patient, CancellationToken ct = default);
    Task<string> GenerateCodeAsync(CancellationToken ct = default);
    // Reporting
    Task<int> GetTotalCountAsync(CancellationToken ct = default);
}

public interface ICoupleRepository
{
    Task<Couple?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Couple?> GetByWifeAndHusbandAsync(Guid wifeId, Guid husbandId, CancellationToken ct = default);
    Task<IReadOnlyList<Couple>> GetAllAsync(CancellationToken ct = default);
    Task<Couple> AddAsync(Couple couple, CancellationToken ct = default);
    Task UpdateAsync(Couple couple, CancellationToken ct = default);
}

public interface ITreatmentCycleRepository
{
    Task<TreatmentCycle?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<TreatmentCycle?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<TreatmentCycle>> GetByCoupleIdAsync(Guid coupleId, CancellationToken ct = default);
    Task<TreatmentCycle> AddAsync(TreatmentCycle cycle, CancellationToken ct = default);
    Task UpdateAsync(TreatmentCycle cycle, CancellationToken ct = default);
    Task<string> GenerateCodeAsync(CancellationToken ct = default);
    // Reporting
    Task<int> GetActiveCountAsync(CancellationToken ct = default);
    Task<Dictionary<string, int>> GetOutcomeStatsAsync(int year, CancellationToken ct = default);
    Task<Dictionary<string, int>> GetMethodDistributionAsync(int year, CancellationToken ct = default);
}

public interface IQueueTicketRepository
{
    Task<QueueTicket?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<QueueTicket>> GetByDepartmentTodayAsync(string departmentCode, CancellationToken ct = default);
    Task<QueueTicket> AddAsync(QueueTicket ticket, CancellationToken ct = default);
    Task UpdateAsync(QueueTicket ticket, CancellationToken ct = default);
    Task<string> GenerateTicketNumberAsync(string departmentCode, CancellationToken ct = default);
    // Reporting
    Task<int> GetTodayCountAsync(CancellationToken ct = default);
    Task<Dictionary<string, int>> GetDailyStatsAsync(DateTime date, CancellationToken ct = default);
}

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default);
    Task<User?> GetByRefreshTokenAsync(string refreshToken, CancellationToken ct = default);
    Task UpdateAsync(User user, CancellationToken ct = default);
}

// Unit of Work
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}


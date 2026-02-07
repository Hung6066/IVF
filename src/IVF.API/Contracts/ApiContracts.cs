using IVF.Domain.Enums;

namespace IVF.API.Contracts;

// ==================== REQUEST RECORDS ====================
public record LoginRequest(string Username, string Password);
public record RefreshTokenRequest(string RefreshToken);
public record AuthResponse(string AccessToken, string RefreshToken, int ExpiresIn, UserDto User);
public record UserDto(Guid Id, string Username, string FullName, string Role, string? Department)
{
    public static UserDto FromEntity(IVF.Domain.Entities.User u) => new(u.Id, u.Username, u.FullName, u.Role, u.Department);
}
public record UpdatePatientRequest(string FullName, string? Phone, string? Address);
public record UpdateCoupleRequest(DateTime? MarriageDate, int? InfertilityYears);
public record SetDonorRequest(Guid DonorId);
public record AdvancePhaseRequest(CyclePhase Phase);
public record CompleteRequest(CycleOutcome Outcome);
public record RecordFolliclesRequest(int? LeftOvaryCount, int? RightOvaryCount, string? LeftFollicles, string? RightFollicles, decimal? EndometriumThickness, string? Findings);
public record UpdateGradeRequest(string Grade, EmbryoDay Day);

public record FreezeRequest(Guid CryoLocationId);
public record UpdateCryoTankRequest(int Used, SpecimenType SpecimenType);

// Andrology
public record RecordMacroscopicRequest(decimal? Volume, string? Appearance, string? Liquefaction, decimal? Ph);
public record RecordMicroscopicRequest(decimal? Concentration, decimal? TotalCount, decimal? ProgressiveMotility, decimal? NonProgressiveMotility, decimal? Immotile, decimal? NormalMorphology, decimal? Vitality);

// SpermBank
public record UpdateDonorProfileRequest(string? BloodType, decimal? Height, decimal? Weight, string? EyeColor, string? HairColor, string? Ethnicity, string? Education, string? Occupation);
public record RecordQualityRequest(decimal? Volume, decimal? Concentration, decimal? Motility, int? VialCount);

// Billing
public record AddItemRequest(string ServiceCode, string Description, int Quantity, decimal UnitPrice);
public record RecordPaymentRequest(decimal Amount, PaymentMethod PaymentMethod, string? TransactionReference);

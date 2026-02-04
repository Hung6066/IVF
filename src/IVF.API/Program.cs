using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using IVF.Application;
using IVF.Application.Common.Interfaces;
using IVF.Application.Features.Andrology.Commands;
using IVF.Application.Features.Andrology.Queries;
using IVF.Application.Features.Billing.Commands;
using IVF.Application.Features.Billing.Queries;
using IVF.Application.Features.Couples.Commands;
using IVF.Application.Features.Couples.Queries;
using IVF.Application.Features.Cycles.Commands;
using IVF.Application.Features.Cycles.Queries;
using IVF.Application.Features.Embryos.Commands;
using IVF.Application.Features.Embryos.Queries;
using IVF.Application.Features.Patients.Commands;
using IVF.Application.Features.Patients.Queries;
using IVF.Application.Features.Queue.Commands;
using IVF.Application.Features.Queue.Queries;
using IVF.Application.Features.Reports.Queries;
using IVF.Application.Features.SpermBank.Commands;
using IVF.Application.Features.SpermBank.Queries;
using IVF.Application.Features.Ultrasounds.Commands;
using IVF.Application.Features.Ultrasounds.Queries;
using IVF.Domain.Entities;
using IVF.Domain.Enums;
using IVF.Infrastructure;
using IVF.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

var builder = WebApplication.CreateBuilder(args);

// Clean Architecture DI
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["Secret"] ?? throw new InvalidOperationException("JWT Secret not configured");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddSignalR();
builder.Services.AddSingleton<IVF.API.Hubs.IQueueNotifier, IVF.API.Hubs.QueueNotifier>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials());
});

var app = builder.Build();

// Validation exception handler
app.UseExceptionHandler(appBuilder =>
{
    appBuilder.Run(async context =>
    {
        var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
        if (exception is ValidationException validationException)
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(new
            {
                errors = validationException.Errors.Select(e => new { e.PropertyName, e.ErrorMessage })
            });
        }
    });
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowAngular");
app.UseAuthentication();
app.UseAuthorization();

// SignalR Hub
app.MapHub<IVF.API.Hubs.QueueHub>("/hubs/queue");

// ==================== AUTH API ====================
var authApi = app.MapGroup("/api/auth").WithTags("Auth");

authApi.MapPost("/login", async (LoginRequest request, IUserRepository userRepo, IUnitOfWork uow, IConfiguration config) =>
{
    var user = await userRepo.GetByUsernameAsync(request.Username);
    if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        return Results.Unauthorized();
    var token = GenerateJwtToken(user, config);
    var refreshToken = GenerateRefreshToken();
    user.UpdateRefreshToken(refreshToken, DateTime.UtcNow.AddDays(7));
    await userRepo.UpdateAsync(user);
    await uow.SaveChangesAsync();
    return Results.Ok(new AuthResponse(token, refreshToken, 3600, UserDto.FromEntity(user)));
});

authApi.MapPost("/refresh", async (RefreshTokenRequest request, IUserRepository userRepo, IUnitOfWork uow, IConfiguration config) =>
{
    var user = await userRepo.GetByRefreshTokenAsync(request.RefreshToken);
    if (user == null || user.RefreshTokenExpiryTime <= DateTime.UtcNow) return Results.Unauthorized();
    var token = GenerateJwtToken(user, config);
    var refreshToken = GenerateRefreshToken();
    user.UpdateRefreshToken(refreshToken, DateTime.UtcNow.AddDays(7));
    await userRepo.UpdateAsync(user);
    await uow.SaveChangesAsync();
    return Results.Ok(new AuthResponse(token, refreshToken, 3600, UserDto.FromEntity(user)));
});

authApi.MapGet("/me", async (ClaimsPrincipal principal, IUserRepository userRepo) =>
{
    var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();
    var user = await userRepo.GetByIdAsync(Guid.Parse(userId));
    return user == null ? Results.NotFound() : Results.Ok(UserDto.FromEntity(user));
}).RequireAuthorization();

// ==================== PATIENTS API ====================
var patientsApi = app.MapGroup("/api/patients").WithTags("Patients").RequireAuthorization();

patientsApi.MapGet("/", async (IMediator m, string? q, int page = 1, int pageSize = 20) =>
    Results.Ok(await m.Send(new SearchPatientsQuery(q, page, pageSize))));

patientsApi.MapGet("/{id:guid}", async (Guid id, IMediator m) =>
{
    var r = await m.Send(new GetPatientByIdQuery(id));
    return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error);
});

patientsApi.MapPost("/", async (CreatePatientCommand cmd, IMediator m) =>
{
    var r = await m.Send(cmd);
    return r.IsSuccess ? Results.Created($"/api/patients/{r.Value!.Id}", r.Value) : Results.BadRequest(r.Error);
});

patientsApi.MapPut("/{id:guid}", async (Guid id, UpdatePatientRequest req, IMediator m) =>
{
    var r = await m.Send(new UpdatePatientCommand(id, req.FullName, req.Phone, req.Address));
    return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error);
});

patientsApi.MapDelete("/{id:guid}", async (Guid id, IMediator m) =>
{
    var r = await m.Send(new DeletePatientCommand(id));
    return r.IsSuccess ? Results.NoContent() : Results.NotFound(r.Error);
});

// ==================== COUPLES API ====================
var couplesApi = app.MapGroup("/api/couples").WithTags("Couples").RequireAuthorization();

couplesApi.MapGet("/", async (IMediator m) =>
    Results.Ok(await m.Send(new GetAllCouplesQuery())));

couplesApi.MapGet("/{id:guid}", async (Guid id, IMediator m) =>
{
    var r = await m.Send(new GetCoupleByIdQuery(id));
    return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error);
});

couplesApi.MapPost("/", async (CreateCoupleCommand cmd, IMediator m) =>
{
    var r = await m.Send(cmd);
    return r.IsSuccess ? Results.Created($"/api/couples/{r.Value!.Id}", r.Value) : Results.BadRequest(r.Error);
});

couplesApi.MapPut("/{id:guid}", async (Guid id, UpdateCoupleRequest req, IMediator m) =>
{
    var r = await m.Send(new UpdateCoupleCommand(id, req.MarriageDate, req.InfertilityYears));
    return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error);
});

couplesApi.MapPost("/{id:guid}/donor", async (Guid id, SetDonorRequest req, IMediator m) =>
{
    var r = await m.Send(new SetSpermDonorCommand(id, req.DonorId));
    return r.IsSuccess ? Results.Ok() : Results.BadRequest(r.Error);
});

// ==================== CYCLES API ====================
var cyclesApi = app.MapGroup("/api/cycles").WithTags("Cycles").RequireAuthorization();

cyclesApi.MapGet("/{id:guid}", async (Guid id, IMediator m) =>
{
    var r = await m.Send(new GetCycleByIdQuery(id));
    return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error);
});

cyclesApi.MapGet("/couple/{coupleId:guid}", async (Guid coupleId, IMediator m) =>
    Results.Ok(await m.Send(new GetCyclesByCoupleQuery(coupleId))));

cyclesApi.MapPost("/", async (CreateCycleCommand cmd, IMediator m) =>
{
    var r = await m.Send(cmd);
    return r.IsSuccess ? Results.Created($"/api/cycles/{r.Value!.Id}", r.Value) : Results.BadRequest(r.Error);
});

cyclesApi.MapPut("/{id:guid}/phase", async (Guid id, AdvancePhaseRequest req, IMediator m) =>
{
    var r = await m.Send(new AdvancePhaseCommand(id, req.Phase));
    return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error);
});

cyclesApi.MapPost("/{id:guid}/complete", async (Guid id, CompleteRequest req, IMediator m) =>
{
    var r = await m.Send(new CompleteCycleCommand(id, req.Outcome));
    return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error);
});

cyclesApi.MapPost("/{id:guid}/cancel", async (Guid id, IMediator m) =>
{
    var r = await m.Send(new CancelCycleCommand(id));
    return r.IsSuccess ? Results.NoContent() : Results.NotFound(r.Error);
});

// ==================== QUEUE API ====================
var queueApi = app.MapGroup("/api/queue").WithTags("Queue").RequireAuthorization();

queueApi.MapGet("/{departmentCode}", async (string departmentCode, IMediator m) =>
    Results.Ok(await m.Send(new GetQueueByDepartmentQuery(departmentCode))));

queueApi.MapPost("/issue", async (IssueTicketCommand cmd, IMediator m) =>
{
    var r = await m.Send(cmd);
    return r.IsSuccess ? Results.Created($"/api/queue/{r.Value!.Id}", r.Value) : Results.BadRequest(r.Error);
});

queueApi.MapPost("/{id:guid}/call", async (Guid id, ClaimsPrincipal principal, IMediator m) =>
{
    var userId = Guid.Parse(principal.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    var r = await m.Send(new CallTicketCommand(id, userId));
    return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error);
});

queueApi.MapPost("/{id:guid}/complete", async (Guid id, IMediator m) =>
{
    var r = await m.Send(new CompleteTicketCommand(id));
    return r.IsSuccess ? Results.NoContent() : Results.NotFound(r.Error);
});

queueApi.MapPost("/{id:guid}/skip", async (Guid id, IMediator m) =>
{
    var r = await m.Send(new SkipTicketCommand(id));
    return r.IsSuccess ? Results.NoContent() : Results.NotFound(r.Error);
});

// ==================== ULTRASOUNDS API ====================
var ultrasoundsApi = app.MapGroup("/api/ultrasounds").WithTags("Ultrasounds").RequireAuthorization();

ultrasoundsApi.MapGet("/cycle/{cycleId:guid}", async (Guid cycleId, IMediator m) =>
    Results.Ok(await m.Send(new GetUltrasoundsByCycleQuery(cycleId))));

ultrasoundsApi.MapPost("/", async (CreateUltrasoundCommand cmd, IMediator m) =>
{
    var r = await m.Send(cmd);
    return r.IsSuccess ? Results.Created($"/api/ultrasounds/{r.Value!.Id}", r.Value) : Results.BadRequest(r.Error);
});

ultrasoundsApi.MapPut("/{id:guid}/follicles", async (Guid id, RecordFolliclesRequest req, IMediator m) =>
{
    var r = await m.Send(new RecordFolliclesCommand(id, req.LeftOvaryCount, req.RightOvaryCount, 
        req.LeftFollicles, req.RightFollicles, req.EndometriumThickness, req.Findings));
    return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error);
});

// ==================== EMBRYOS API ====================
var embryosApi = app.MapGroup("/api/embryos").WithTags("Embryos").RequireAuthorization();

embryosApi.MapGet("/cycle/{cycleId:guid}", async (Guid cycleId, IMediator m) =>
    Results.Ok(await m.Send(new GetEmbryosByCycleQuery(cycleId))));

embryosApi.MapPost("/", async (CreateEmbryoCommand cmd, IMediator m) =>
{
    var r = await m.Send(cmd);
    return r.IsSuccess ? Results.Created($"/api/embryos/{r.Value!.Id}", r.Value) : Results.BadRequest(r.Error);
});

embryosApi.MapPut("/{id:guid}/grade", async (Guid id, UpdateGradeRequest req, IMediator m) =>
{
    var r = await m.Send(new UpdateEmbryoGradeCommand(id, req.Grade, req.Day));
    return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error);
});

embryosApi.MapPost("/{id:guid}/transfer", async (Guid id, IMediator m) =>
{
    var r = await m.Send(new TransferEmbryoCommand(id));
    return r.IsSuccess ? Results.NoContent() : Results.NotFound(r.Error);
});

embryosApi.MapPost("/{id:guid}/freeze", async (Guid id, FreezeRequest req, IMediator m) =>
{
    var r = await m.Send(new FreezeEmbryoCommand(id, req.CryoLocationId));
    return r.IsSuccess ? Results.Ok(r.Value) : Results.BadRequest(r.Error);
});

embryosApi.MapPost("/{id:guid}/thaw", async (Guid id, IMediator m) =>
{
    var r = await m.Send(new ThawEmbryoCommand(id));
    return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error);
});

// ==================== ANDROLOGY API ====================
var andrologyApi = app.MapGroup("/api/andrology").WithTags("Andrology").RequireAuthorization();

andrologyApi.MapGet("/patient/{patientId:guid}", async (Guid patientId, IMediator m) =>
    Results.Ok(await m.Send(new GetAnalysesByPatientQuery(patientId))));

andrologyApi.MapGet("/cycle/{cycleId:guid}", async (Guid cycleId, IMediator m) =>
    Results.Ok(await m.Send(new GetAnalysesByCycleQuery(cycleId))));

andrologyApi.MapPost("/", async (CreateSemenAnalysisCommand cmd, IMediator m) =>
{
    var r = await m.Send(cmd);
    return r.IsSuccess ? Results.Created($"/api/andrology/{r.Value!.Id}", r.Value) : Results.BadRequest(r.Error);
});

andrologyApi.MapPut("/{id:guid}/macroscopic", async (Guid id, RecordMacroscopicRequest req, IMediator m) =>
{
    var r = await m.Send(new RecordMacroscopicCommand(id, req.Volume, req.Appearance, req.Liquefaction, req.Ph));
    return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error);
});

andrologyApi.MapPut("/{id:guid}/microscopic", async (Guid id, RecordMicroscopicRequest req, IMediator m) =>
{
    var r = await m.Send(new RecordMicroscopicCommand(id, req.Concentration, req.TotalCount, req.ProgressiveMotility,
        req.NonProgressiveMotility, req.Immotile, req.NormalMorphology, req.Vitality));
    return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error);
});

// ==================== SPERM BANK API ====================
var spermBankApi = app.MapGroup("/api/spermbank").WithTags("SpermBank").RequireAuthorization();

spermBankApi.MapGet("/donors", async (IMediator m, string? q, int page = 1, int pageSize = 20) =>
    Results.Ok(await m.Send(new SearchDonorsQuery(q, page, pageSize))));

spermBankApi.MapGet("/donors/{id:guid}", async (Guid id, IMediator m) =>
{
    var r = await m.Send(new GetDonorByIdQuery(id));
    return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error);
});

spermBankApi.MapPost("/donors", async (CreateDonorCommand cmd, IMediator m) =>
{
    var r = await m.Send(cmd);
    return r.IsSuccess ? Results.Created($"/api/spermbank/donors/{r.Value!.Id}", r.Value) : Results.BadRequest(r.Error);
});

spermBankApi.MapPut("/donors/{id:guid}/profile", async (Guid id, UpdateDonorProfileRequest req, IMediator m) =>
{
    var r = await m.Send(new UpdateDonorProfileCommand(id, req.BloodType, req.Height, req.Weight, 
        req.EyeColor, req.HairColor, req.Ethnicity, req.Education, req.Occupation));
    return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error);
});

spermBankApi.MapGet("/samples/donor/{donorId:guid}", async (Guid donorId, IMediator m) =>
    Results.Ok(await m.Send(new GetSamplesByDonorQuery(donorId))));

spermBankApi.MapGet("/samples/available", async (IMediator m) =>
    Results.Ok(await m.Send(new GetAvailableSamplesQuery())));

spermBankApi.MapPost("/samples", async (CreateSampleCommand cmd, IMediator m) =>
{
    var r = await m.Send(cmd);
    return r.IsSuccess ? Results.Created($"/api/spermbank/samples/{r.Value!.Id}", r.Value) : Results.BadRequest(r.Error);
});

spermBankApi.MapPut("/samples/{id:guid}/quality", async (Guid id, RecordQualityRequest req, IMediator m) =>
{
    var r = await m.Send(new RecordSampleQualityCommand(id, req.Volume, req.Concentration, req.Motility, req.VialCount));
    return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error);
});

// ==================== BILLING API ====================
var billingApi = app.MapGroup("/api/billing").WithTags("Billing").RequireAuthorization();

billingApi.MapGet("/invoices", async (IMediator m, string? q, int page = 1, int pageSize = 20) =>
    Results.Ok(await m.Send(new SearchInvoicesQuery(q, page, pageSize))));

billingApi.MapGet("/invoices/{id:guid}", async (Guid id, IMediator m) =>
{
    var r = await m.Send(new GetInvoiceByIdQuery(id));
    return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error);
});

billingApi.MapGet("/invoices/patient/{patientId:guid}", async (Guid patientId, IMediator m) =>
    Results.Ok(await m.Send(new GetInvoicesByPatientQuery(patientId))));

billingApi.MapPost("/invoices", async (CreateInvoiceCommand cmd, IMediator m) =>
{
    var r = await m.Send(cmd);
    return r.IsSuccess ? Results.Created($"/api/billing/invoices/{r.Value!.Id}", r.Value) : Results.BadRequest(r.Error);
});

billingApi.MapPost("/invoices/{id:guid}/items", async (Guid id, AddItemRequest req, IMediator m) =>
{
    var r = await m.Send(new AddInvoiceItemCommand(id, req.ServiceCode, req.Description, req.Quantity, req.UnitPrice));
    return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error);
});

billingApi.MapPost("/invoices/{id:guid}/issue", async (Guid id, IMediator m) =>
{
    var r = await m.Send(new IssueInvoiceCommand(id));
    return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error);
});

billingApi.MapPost("/invoices/{id:guid}/pay", async (Guid id, ClaimsPrincipal principal, RecordPaymentRequest req, IMediator m) =>
{
    var userId = Guid.Parse(principal.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    var r = await m.Send(new RecordPaymentCommand(id, req.Amount, req.PaymentMethod, req.TransactionReference, userId));
    return r.IsSuccess ? Results.Ok(r.Value) : Results.BadRequest(r.Error);
});

// ==================== REPORTS API ====================
var reportsApi = app.MapGroup("/api/reports").WithTags("Reports").RequireAuthorization();

reportsApi.MapGet("/dashboard", async (IMediator m) =>
    Results.Ok(await m.Send(new GetDashboardStatsQuery())));

reportsApi.MapGet("/cycles/success-rates", async (IMediator m, int? year) =>
    Results.Ok(await m.Send(new GetCycleSuccessRatesQuery(year))));

reportsApi.MapGet("/cycles/methods", async (IMediator m, int? year) =>
    Results.Ok(await m.Send(new GetTreatmentMethodDistributionQuery(year))));

reportsApi.MapGet("/revenue/monthly", async (IMediator m, int year) =>
    Results.Ok(await m.Send(new GetMonthlyRevenueQuery(year))));

reportsApi.MapGet("/queue/stats", async (IMediator m, DateTime? date) =>
    Results.Ok(await m.Send(new GetQueueStatisticsQuery(date ?? DateTime.UtcNow))));

// Auto-migrate and seed in dev
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();
    await db.Database.MigrateAsync();
    await DatabaseSeeder.SeedAsync(app.Services);
}

app.Run();

// ==================== HELPER FUNCTIONS ====================
static string GenerateJwtToken(User user, IConfiguration config)
{
    var jwtSettings = config.GetSection("JwtSettings");
    var key = Encoding.UTF8.GetBytes(jwtSettings["Secret"]!);
    var tokenHandler = new JwtSecurityTokenHandler();
    var tokenDescriptor = new SecurityTokenDescriptor
    {
        Subject = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.GivenName, user.FullName),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("department", user.Department ?? "")
        }),
        Expires = DateTime.UtcNow.AddMinutes(60),
        Issuer = jwtSettings["Issuer"],
        Audience = jwtSettings["Audience"],
        SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
    };
    return tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor));
}

static string GenerateRefreshToken()
{
    var bytes = new byte[64];
    using var rng = RandomNumberGenerator.Create();
    rng.GetBytes(bytes);
    return Convert.ToBase64String(bytes);
}

// ==================== REQUEST RECORDS ====================
record LoginRequest(string Username, string Password);
record RefreshTokenRequest(string RefreshToken);
record AuthResponse(string AccessToken, string RefreshToken, int ExpiresIn, UserDto User);
record UserDto(Guid Id, string Username, string FullName, string Role, string? Department)
{
    public static UserDto FromEntity(User u) => new(u.Id, u.Username, u.FullName, u.Role, u.Department);
}
record UpdatePatientRequest(string FullName, string? Phone, string? Address);
record UpdateCoupleRequest(DateTime? MarriageDate, int? InfertilityYears);
record SetDonorRequest(Guid DonorId);
record AdvancePhaseRequest(CyclePhase Phase);
record CompleteRequest(CycleOutcome Outcome);
record RecordFolliclesRequest(int? LeftOvaryCount, int? RightOvaryCount, string? LeftFollicles, string? RightFollicles, decimal? EndometriumThickness, string? Findings);
record UpdateGradeRequest(EmbryoGrade Grade, EmbryoDay Day);
record FreezeRequest(Guid CryoLocationId);

// Andrology
record RecordMacroscopicRequest(decimal? Volume, string? Appearance, string? Liquefaction, decimal? Ph);
record RecordMicroscopicRequest(decimal? Concentration, decimal? TotalCount, decimal? ProgressiveMotility, decimal? NonProgressiveMotility, decimal? Immotile, decimal? NormalMorphology, decimal? Vitality);

// SpermBank
record UpdateDonorProfileRequest(string? BloodType, decimal? Height, decimal? Weight, string? EyeColor, string? HairColor, string? Ethnicity, string? Education, string? Occupation);
record RecordQualityRequest(decimal? Volume, decimal? Concentration, decimal? Motility, int? VialCount);

// Billing
record AddItemRequest(string ServiceCode, string Description, int Quantity, decimal UnitPrice);
record RecordPaymentRequest(decimal Amount, PaymentMethod PaymentMethod, string? TransactionReference);


using System.Diagnostics;
using System.Security.Claims;
using IVF.API.Services;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IVF.API.Endpoints;

/// <summary>
/// Endpoints for managing user handwritten signatures and per-user signing certificates.
/// Users can upload/draw their signature; admins can provision signing certificates.
/// </summary>
public static class UserSignatureEndpoints
{
    public static void MapUserSignatureEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/user-signatures")
            .WithTags("User Signatures")
            .RequireAuthorization();

        // ─── Get current user's signature ───────────────────────
        group.MapGet("/me", async (
            ClaimsPrincipal principal,
            IvfDbContext db) =>
        {
            var userId = GetUserId(principal);
            if (userId == null) return Results.Unauthorized();

            var sig = await db.UserSignatures
                .Where(s => s.UserId == userId.Value && !s.IsDeleted && s.IsActive)
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync();

            if (sig == null) return Results.NotFound(new { message = "Chưa có chữ ký" });

            return Results.Ok(MapToDto(sig));
        })
        .WithName("GetMySignature");

        // ─── Upload/Update current user's signature ─────────────
        group.MapPost("/me", async (
            UserSignatureRequest request,
            ClaimsPrincipal principal,
            IvfDbContext db) =>
        {
            var userId = GetUserId(principal);
            if (userId == null) return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(request.SignatureImageBase64))
                return Results.BadRequest(new { error = "Chữ ký không được để trống" });

            // Validate base64 is a PNG/JPEG
            if (!IsValidSignatureImage(request.SignatureImageBase64))
                return Results.BadRequest(new { error = "Ảnh chữ ký không hợp lệ (yêu cầu PNG hoặc JPEG)" });

            // Deactivate any existing signatures
            var existingSignatures = await db.UserSignatures
                .Where(s => s.UserId == userId.Value && !s.IsDeleted && s.IsActive)
                .ToListAsync();

            foreach (var existing in existingSignatures)
            {
                existing.Deactivate();
            }

            // Create new signature
            var mimeType = request.SignatureImageBase64.StartsWith("/9j/") ? "image/jpeg" : "image/png";
            var sig = UserSignature.Create(userId.Value, request.SignatureImageBase64, mimeType);

            db.UserSignatures.Add(sig);
            await db.SaveChangesAsync();

            return Results.Created($"/api/user-signatures/{sig.Id}", MapToDto(sig));
        })
        .WithName("UploadMySignature");

        // ─── Delete current user's signature ────────────────────
        group.MapDelete("/me", async (
            ClaimsPrincipal principal,
            IvfDbContext db) =>
        {
            var userId = GetUserId(principal);
            if (userId == null) return Results.Unauthorized();

            var signatures = await db.UserSignatures
                .Where(s => s.UserId == userId.Value && !s.IsDeleted)
                .ToListAsync();

            foreach (var sig in signatures)
            {
                sig.MarkAsDeleted();
            }

            await db.SaveChangesAsync();
            return Results.NoContent();
        })
        .WithName("DeleteMySignature");

        // ─── Get signature by user ID (admin) ───────────────────
        group.MapGet("/users/{userId:guid}", async (
            Guid userId,
            IvfDbContext db) =>
        {
            var sig = await db.UserSignatures
                .Where(s => s.UserId == userId && !s.IsDeleted && s.IsActive)
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync();

            if (sig == null) return Results.NotFound(new { message = "Người dùng chưa có chữ ký" });

            return Results.Ok(MapToDto(sig));
        })
        .WithName("GetUserSignature")
        .RequireAuthorization("AdminOnly");

        // ─── List all users with their signature status (admin) ──
        // LEFT JOIN: returns ALL active users, with signature info if exists.
        // This ensures the admin can see and manage all users, even those without signatures.
        group.MapGet("/", async (
            IvfDbContext db,
            bool? activeOnly) =>
        {
            var items = await db.Users
                .Where(u => !u.IsDeleted && u.IsActive)
                .GroupJoin(
                    db.UserSignatures.Where(s => !s.IsDeleted && s.IsActive),
                    u => u.Id,
                    s => s.UserId,
                    (user, sigs) => new { user, sigs })
                .SelectMany(
                    x => x.sigs.DefaultIfEmpty(),
                    (x, sig) => new
                    {
                        Id = sig != null ? sig.Id : (Guid?)null,
                        UserId = x.user.Id,
                        UserFullName = x.user.FullName,
                        UserRole = x.user.Role,
                        UserDepartment = x.user.Department,
                        IsActive = sig != null && sig.IsActive,
                        CertificateSubject = sig != null ? sig.CertificateSubject : null,
                        CertificateSerialNumber = sig != null ? sig.CertificateSerialNumber : null,
                        CertificateExpiry = sig != null ? sig.CertificateExpiry : (DateTime?)null,
                        WorkerName = sig != null ? sig.WorkerName : null,
                        CertStatus = sig != null ? sig.CertStatus.ToString() : "None",
                        HasSignatureImage = sig != null && !string.IsNullOrEmpty(sig.SignatureImageBase64),
                        CreatedAt = sig != null ? sig.CreatedAt : (DateTime?)null,
                        UpdatedAt = sig != null ? sig.UpdatedAt : (DateTime?)null
                    })
                .OrderBy(x => x.UserFullName)
                .ToListAsync();

            return Results.Ok(new { items, total = items.Count });
        })
        .WithName("ListUserSignatures")
        .RequireAuthorization("AdminOnly");

        // ─── Provision signing certificate for user (admin) ─────
        group.MapPost("/users/{userId:guid}/provision-certificate", async (
            Guid userId,
            IvfDbContext db,
            IOptions<DigitalSigningOptions> options,
            ILogger<Program> logger) =>
        {
            var opts = options.Value;
            if (!opts.Enabled)
                return Results.BadRequest(new { error = "Ký số chưa được bật" });

            var user = await db.Users.FindAsync(userId);
            if (user == null)
                return Results.NotFound(new { error = "Người dùng không tồn tại" });

            var sig = await db.UserSignatures
                .Where(s => s.UserId == userId && !s.IsDeleted && s.IsActive)
                .FirstOrDefaultAsync();

            if (sig == null)
                return Results.BadRequest(new { error = "Người dùng chưa có chữ ký tay. Yêu cầu tải chữ ký trước." });

            try
            {
                sig.SetCertificateStatus(CertificateStatus.Pending);
                await db.SaveChangesAsync();

                // Generate per-user keystore and SignServer worker
                var result = await ProvisionUserCertificateAsync(user, opts, logger);

                sig.SetCertificateInfo(
                    subject: result.CertSubject,
                    serialNumber: result.SerialNumber,
                    expiry: result.Expiry,
                    workerName: result.WorkerName,
                    keystorePath: result.KeystorePath);

                await db.SaveChangesAsync();

                return Results.Ok(new
                {
                    success = true,
                    certificateSubject = result.CertSubject,
                    workerName = result.WorkerName,
                    expiry = result.Expiry,
                    message = $"Đã cấp chứng thư số cho {user.FullName}"
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to provision certificate for user {UserId}", userId);
                sig.SetCertificateStatus(CertificateStatus.Error);
                await db.SaveChangesAsync();

                return Results.Ok(new
                {
                    success = false,
                    error = ex.Message,
                    message = "Lỗi khi cấp chứng thư số"
                });
            }
        })
        .WithName("ProvisionUserCertificate")
        .RequireAuthorization("AdminOnly")
        .RequireRateLimiting("signing-provision");

        // ─── Upload signature for a specific user (admin) ───────
        group.MapPost("/users/{userId:guid}", async (
            Guid userId,
            UserSignatureRequest request,
            IvfDbContext db) =>
        {
            var user = await db.Users.FindAsync(userId);
            if (user == null)
                return Results.NotFound(new { error = "Người dùng không tồn tại" });

            if (string.IsNullOrWhiteSpace(request.SignatureImageBase64))
                return Results.BadRequest(new { error = "Chữ ký không được để trống" });

            // Deactivate existing
            var existingSignatures = await db.UserSignatures
                .Where(s => s.UserId == userId && !s.IsDeleted && s.IsActive)
                .ToListAsync();
            foreach (var existing in existingSignatures)
                existing.Deactivate();

            var mimeType = request.SignatureImageBase64.StartsWith("/9j/") ? "image/jpeg" : "image/png";
            var sig = UserSignature.Create(userId, request.SignatureImageBase64, mimeType);
            db.UserSignatures.Add(sig);
            await db.SaveChangesAsync();

            return Results.Created($"/api/user-signatures/{sig.Id}", MapToDto(sig));
        })
        .WithName("AdminUploadUserSignature")
        .RequireAuthorization("AdminOnly");

        // ─── Get signature image as file (for PDF rendering) ────
        group.MapGet("/{id:guid}/image", async (
            Guid id,
            IvfDbContext db) =>
        {
            var sig = await db.UserSignatures
                .Where(s => s.Id == id && !s.IsDeleted)
                .FirstOrDefaultAsync();

            if (sig == null || string.IsNullOrEmpty(sig.SignatureImageBase64))
                return Results.NotFound();

            var bytes = Convert.FromBase64String(sig.SignatureImageBase64);
            return Results.File(bytes, sig.ImageMimeType, $"signature_{id}.png");
        })
        .WithName("GetSignatureImage");

        // ─── Get signature image by user ID ─────────────────────
        group.MapGet("/users/{userId:guid}/image", async (
            Guid userId,
            IvfDbContext db) =>
        {
            var sig = await db.UserSignatures
                .Where(s => s.UserId == userId && !s.IsDeleted && s.IsActive)
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync();

            if (sig == null || string.IsNullOrEmpty(sig.SignatureImageBase64))
                return Results.NotFound();

            var bytes = Convert.FromBase64String(sig.SignatureImageBase64);
            return Results.File(bytes, sig.ImageMimeType, $"signature_{userId}.png");
        })
        .WithName("GetUserSignatureImage");

        // ─── Test sign with user's certificate ──────────────────
        group.MapPost("/users/{userId:guid}/test-sign", async (
            Guid userId,
            IvfDbContext db,
            IOptions<DigitalSigningOptions> options,
            ILogger<Program> logger) =>
        {
            var sig = await db.UserSignatures
                .Include(s => s.User)
                .Where(s => s.UserId == userId && !s.IsDeleted && s.IsActive)
                .FirstOrDefaultAsync();

            if (sig == null)
                return Results.NotFound(new { error = "Người dùng chưa có chữ ký" });

            if (sig.CertStatus != CertificateStatus.Active || string.IsNullOrEmpty(sig.WorkerName))
                return Results.BadRequest(new { error = "Chưa cấp chứng thư số cho người dùng này" });

            var opts = options.Value;
            try
            {
                var testPdf = SigningAdminEndpoints.GenerateMinimalTestPdf();
                var workerName = sig.WorkerName ?? opts.WorkerName;

                var sw = Stopwatch.StartNew();
                var signedPdf = await SignPdfWithWorkerAsync(testPdf, workerName, sig.User?.FullName, opts, logger);
                sw.Stop();

                return Results.Ok(new
                {
                    success = true,
                    workerName,
                    originalSize = testPdf.Length,
                    signedSize = signedPdf.Length,
                    durationMs = sw.ElapsedMilliseconds,
                    signer = sig.User?.FullName
                });
            }
            catch (Exception ex)
            {
                return Results.Ok(new { success = false, error = ex.Message });
            }
        })
        .WithName("TestUserSigning")
        .RequireAuthorization("AdminOnly")
        .RequireRateLimiting("signing");
    }

    // ─── Helper Methods ─────────────────────────────────────────

    private static Guid? GetUserId(ClaimsPrincipal principal)
    {
        var claim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    private static bool IsValidSignatureImage(string base64)
    {
        try
        {
            var bytes = Convert.FromBase64String(base64);
            // Check PNG magic bytes (‰PNG)
            if (bytes.Length > 4 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
                return true;
            // Check JPEG magic bytes (ÿØÿ)
            if (bytes.Length > 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
                return true;
            return false;
        }
        catch
        {
            return false;
        }
    }

    private static object MapToDto(UserSignature sig) => new
    {
        sig.Id,
        sig.UserId,
        sig.SignatureImageBase64,
        sig.ImageMimeType,
        sig.IsActive,
        sig.CertificateSubject,
        sig.CertificateSerialNumber,
        sig.CertificateExpiry,
        sig.WorkerName,
        CertStatus = sig.CertStatus.ToString(),
        sig.CreatedAt,
        sig.UpdatedAt
    };

    /// <summary>
    /// Provision a per-user signing certificate and SignServer worker.
    /// Supports two crypto token types (Phase 4):
    ///   - P12CryptoToken: PKCS#12 file-based keystore (default, Phase 1-3)
    ///   - PKCS11CryptoToken: SoftHSM2 / hardware HSM (Phase 4, FIPS 140-2 Level 1)
    /// 
    /// For P12: Uses Java keytool to generate PKCS#12 keystore in persistent volume.
    /// For PKCS#11: Generates key inside SoftHSM2 token via keytool -providerClass.
    /// </summary>
    private static async Task<CertProvisionResult> ProvisionUserCertificateAsync(
        User user,
        DigitalSigningOptions opts,
        ILogger logger)
    {
        var sanitizedName = SanitizeForDN(user.FullName);
        var workerName = $"PDFSigner_{SanitizeWorkerId(user.Username)}";
        var keyAlias = "signer";
        const string keyDir = "/opt/keyfactor/persistent/keys";
        var certValidity = 1095; // 3 years
        var workerId = 100 + Math.Abs(user.Id.GetHashCode() % 900);

        logger.LogInformation(
            "Provisioning certificate for user {User}, worker {Worker} (ID: {WorkerId}), CryptoToken={TokenType}",
            user.FullName, workerName, workerId, opts.CryptoTokenType);

        // Step 0: Ensure key directory exists with proper permissions
        await RunDockerExecAsRootAsync("ivf-signserver",
            $"mkdir -p {keyDir} && chown 10001:root {keyDir} && chmod 700 {keyDir}", logger);

        var certDN = $"CN={sanitizedName},O=IVF Clinic,OU={user.Role ?? "Staff"},C=VN";

        string propsContent;

        if (opts.CryptoTokenType == CryptoTokenType.PKCS11)
        {
            // ─── PKCS#11 (SoftHSM2 / HSM) provisioning ───
            var pin = opts.ResolvePkcs11Pin() ?? "changeit";
            var hsmKeyLabel = $"{workerName}_{keyAlias}";

            // Generate key pair inside PKCS#11 token using pkcs11-tool
            var genKeyCmd = $"pkcs11-tool --module /usr/lib/softhsm/libsofthsm2.so " +
                $"--login --pin {pin} " +
                $"--keypairgen --key-type rsa:2048 " +
                $"--label \"{hsmKeyLabel}\" " +
                $"--id $(printf '%02x' {workerId}) " +
                $"--usage-sign";

            await RunDockerExecAsync("ivf-signserver", genKeyCmd, logger);

            // Generate self-signed certificate for the key using keytool with PKCS#11 provider
            var keytoolP11Cmd = $"keytool -selfcert " +
                $"-alias \"{hsmKeyLabel}\" " +
                $"-dname \"{certDN}\" " +
                $"-validity {certValidity} " +
                $"-sigalg SHA256withRSA " +
                $"-storetype PKCS11 " +
                $"-providerClass sun.security.pkcs11.SunPKCS11 " +
                $"-providerArg /opt/keyfactor/persistent/softhsm/pkcs11-java.cfg " +
                $"-storepass {pin} " +
                $"-J-Djava.security.debug=none";

            // Create PKCS#11 provider config for Java
            var p11Config = $"name = SoftHSM\nlibrary = /usr/lib/softhsm/libsofthsm2.so\nslot = 0\n";
            var p11ConfigPath = "/opt/keyfactor/persistent/softhsm/pkcs11-java.cfg";
            await RunDockerExecAsync("ivf-signserver",
                $"bash -c 'echo -e \"{p11Config}\" > {p11ConfigPath}'", logger);

            await RunDockerExecAsync("ivf-signserver", keytoolP11Cmd, logger);

            // Worker properties for PKCS#11
            propsContent =
                $"GLOB.WORKER{workerId}.CLASSPATH = org.signserver.module.pdfsigner.PDFSigner\n" +
                $"GLOB.WORKER{workerId}.SIGNERTOKEN.CLASSPATH = org.signserver.server.cryptotokens.PKCS11CryptoToken\n" +
                $"WORKER{workerId}.NAME = {workerName}\n" +
                $"WORKER{workerId}.AUTHTYPE = org.signserver.server.ClientCertAuthorizer\n" +
                $"WORKER{workerId}.SHAREDLIBRARYNAME = {opts.Pkcs11SharedLibraryName}\n" +
                $"WORKER{workerId}.SLOT = {opts.Pkcs11SlotLabel}\n" +
                $"WORKER{workerId}.PIN = {pin}\n" +
                $"WORKER{workerId}.DEFAULTKEY = {hsmKeyLabel}\n" +
                $"WORKER{workerId}.ATTRIBUTE.PRIVATE.RSA.CKA_EXTRACTABLE = FALSE\n" +
                $"WORKER{workerId}.ATTRIBUTE.PRIVATE.RSA.CKA_SENSITIVE = TRUE\n";

            logger.LogInformation("Using PKCS11CryptoToken with SoftHSM2 for worker {Worker}", workerName);
        }
        else
        {
            // ─── P12 (file-based) provisioning (original Phase 1-3 path) ───
            var keystorePassword = "changeit";
            var keystorePath = $"{keyDir}/{workerName.ToLowerInvariant()}.p12";

            var keytoolCmd = $"keytool -genkeypair " +
                $"-alias {keyAlias} " +
                $"-keyalg RSA -keysize 2048 -sigalg SHA256withRSA " +
                $"-validity {certValidity} " +
                $"-dname \"{certDN}\" " +
                $"-keystore {keystorePath} " +
                $"-storetype PKCS12 " +
                $"-storepass {keystorePassword} " +
                $"-keypass {keystorePassword}";

            await RunDockerExecAsync("ivf-signserver", keytoolCmd, logger);

            // Set restrictive permissions on the new keystore (owner read-only)
            await RunDockerExecAsRootAsync("ivf-signserver",
                $"chmod 400 {keystorePath} && chown 10001:root {keystorePath}", logger);

            propsContent =
                $"GLOB.WORKER{workerId}.CLASSPATH = org.signserver.module.pdfsigner.PDFSigner\n" +
                $"GLOB.WORKER{workerId}.SIGNERTOKEN.CLASSPATH = org.signserver.server.cryptotokens.P12CryptoToken\n" +
                $"WORKER{workerId}.NAME = {workerName}\n" +
                $"WORKER{workerId}.AUTHTYPE = org.signserver.server.ClientCertAuthorizer\n" +
                $"WORKER{workerId}.DEFAULTKEY = {keyAlias}\n" +
                $"WORKER{workerId}.KEYSTOREPATH = {keystorePath}\n" +
                $"WORKER{workerId}.KEYSTOREPASSWORD = {keystorePassword}\n";
        }

        // Write properties to a temp file and copy to container
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, propsContent);

        var containerPropsPath = $"/tmp/worker_{workerId}.properties";
        await RunProcessAsync("docker", $"cp \"{tempFile}\" ivf-signserver:{containerPropsPath}", logger);
        File.Delete(tempFile);

        // Load properties
        await RunDockerExecAsync("ivf-signserver", $"bin/signserver setproperties {containerPropsPath}", logger);

        // Clean up temp properties file (contains keystore password / PIN)
        await RunDockerExecAsync("ivf-signserver", $"rm -f {containerPropsPath}", logger);

        // Step 3: Fix TYPE and set additional properties
        await RunDockerExecAsync("ivf-signserver", $"bin/signserver setproperty {workerId} TYPE PROCESSABLE", logger);
        await RunDockerExecAsync("ivf-signserver",
            $"bin/signserver setproperty {workerId} CERTIFICATION_LEVEL NOT_CERTIFIED", logger);
        await RunDockerExecAsync("ivf-signserver",
            $"bin/signserver setproperty {workerId} ADD_VISIBLE_SIGNATURE false", logger);
        await RunDockerExecAsync("ivf-signserver",
            $"bin/signserver setproperty {workerId} REASON \"Ky boi {sanitizedName}\"", logger);
        await RunDockerExecAsync("ivf-signserver",
            $"bin/signserver setproperty {workerId} LOCATION \"IVF Clinic\"", logger);

        // Step 4: Add authorized API client certificate (mTLS) if configured
        if (!string.IsNullOrEmpty(opts.ClientCertificatePath) && File.Exists(opts.ClientCertificatePath))
        {
            try
            {
                using var apiCert = System.Security.Cryptography.X509Certificates.X509CertificateLoader
                    .LoadPkcs12FromFile(opts.ClientCertificatePath, opts.ResolveClientCertificatePassword());
                var serial = apiCert.SerialNumber;
                var issuerDN = apiCert.Issuer;
                await RunDockerExecAsync("ivf-signserver",
                    $"bin/signserver addauthorizedclient {workerId} {serial} \"{issuerDN}\"", logger);
                logger.LogInformation("Added authorized client {Serial} to worker {WorkerId}", serial, workerId);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to add authorized client to worker {WorkerId}. " +
                    "Manual configuration may be needed via: signserver addauthorizedclient {EscapedWorkerId} <serial> <issuerDN>", workerId, workerId);
            }
        }

        // Step 5: Reload and activate
        await RunDockerExecAsync("ivf-signserver", $"bin/signserver reload {workerId}", logger);
        var activationPin = opts.CryptoTokenType == CryptoTokenType.PKCS11
            ? (opts.ResolvePkcs11Pin() ?? "changeit")
            : "changeit";
        await RunDockerExecAsync("ivf-signserver",
            $"bin/signserver activatecryptotoken {workerId} {activationPin}", logger);

        var expiry = DateTime.UtcNow.AddDays(certValidity);
        var keystoreInfo = opts.CryptoTokenType == CryptoTokenType.PKCS11
            ? $"PKCS11:{opts.Pkcs11SharedLibraryName}/{opts.Pkcs11SlotLabel}"
            : $"{keyDir}/{workerName.ToLowerInvariant()}.p12";

        logger.LogInformation("Certificate provisioned: {Subject}, worker {Worker}, type {TokenType}, expires {Expiry}",
            certDN, workerName, opts.CryptoTokenType, expiry);

        return new CertProvisionResult(certDN, null, expiry, workerName, keystoreInfo);
    }

    /// <summary>
    /// Sign a PDF using a specific SignServer worker (for per-user signing).
    /// Supports mTLS client certificate authentication when configured.
    /// </summary>
    internal static async Task<byte[]> SignPdfWithWorkerAsync(
        byte[] pdfBytes,
        string workerName,
        string? signerName,
        DigitalSigningOptions opts,
        ILogger logger)
    {
        using var handler = new HttpClientHandler();
        if (opts.SkipTlsValidation)
            handler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

        // mTLS: attach client certificate if configured
        if (!string.IsNullOrEmpty(opts.ClientCertificatePath) && File.Exists(opts.ClientCertificatePath))
        {
            var certPassword = opts.ResolveClientCertificatePassword();
            handler.ClientCertificates.Add(
                System.Security.Cryptography.X509Certificates.X509CertificateLoader
                    .LoadPkcs12FromFile(opts.ClientCertificatePath, certPassword));
        }

        using var client = new HttpClient(handler);
        client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);

        var processUrl = $"{opts.SignServerUrl.TrimEnd('/')}/process";

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(workerName), "workerName");

        var pdfContent = new ByteArrayContent(pdfBytes);
        pdfContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        content.Add(pdfContent, "data", "document.pdf");

        // Note: REASON and LOCATION are configured on the worker properties.
        // SignServer CE does not allow overriding metadata via request by default.

        var response = await client.PostAsync(processUrl, content);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"SignServer error ({response.StatusCode}): {errorBody}");
        }

        return await response.Content.ReadAsByteArrayAsync();
    }

    // ─── Process Helpers ────────────────────────────────────────

    private static async Task RunDockerExecAsync(string container, string command, ILogger logger)
    {
        await RunProcessAsync("docker", $"exec {container} {command}", logger);
    }

    private static async Task RunDockerExecAsRootAsync(string container, string command, ILogger logger)
    {
        await RunProcessAsync("docker", $"exec -u root {container} bash -c \"{command}\"", logger);
    }

    private static async Task<string> RunProcessAsync(string fileName, string arguments, ILogger logger)
    {
        logger.LogDebug("Running: {FileName} {Arguments}", fileName, arguments);

        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)!;
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            logger.LogWarning("Process exited with code {Code}: {Error}", process.ExitCode, error);
        }

        return output + error;
    }

    private static string SanitizeForDN(string name)
    {
        // Remove characters invalid in X.500 DNs
        return name.Replace("\"", "").Replace(",", " ").Replace("+", " ")
                    .Replace("=", " ").Replace("<", "").Replace(">", "")
                    .Replace("#", "").Replace(";", "").Trim();
    }

    private static string SanitizeWorkerId(string username)
    {
        return new string(username.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
    }

    // ─── Internal: Make GenerateMinimalTestPdf accessible ───────
    // (Referenced from SigningAdminEndpoints.GenerateMinimalTestPdf)

    private record CertProvisionResult(
        string CertSubject,
        string? SerialNumber,
        DateTime Expiry,
        string WorkerName,
        string KeystorePath);
}

/// <summary>
/// Request model for uploading a handwritten signature.
/// </summary>
public record UserSignatureRequest(
    string SignatureImageBase64
);

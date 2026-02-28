using System.Diagnostics;
using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using IVF.API.Hubs;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;

namespace IVF.API.Services;

/// <summary>
/// Manages Certificate Authorities and mTLS certificates for secure inter-service
/// communication (PostgreSQL SSL, MinIO TLS, API client certs).
/// Uses System.Security.Cryptography for cert generation — no external openssl dependency.
/// </summary>
public sealed class CertificateAuthorityService(
    IServiceScopeFactory scopeFactory,
    IHubContext<BackupHub> hubContext,
    IConfiguration configuration,
    ILogger<CertificateAuthorityService> logger,
    IDataProtectionProvider dataProtectionProvider)
{
    private const string KeyProtectionPurpose = "IVF.CertificateAuthority.PrivateKeys";

    // OID: anyPolicy (RFC 5280 §4.2.1.4)
    private const string OidAnyPolicy = "2.5.29.32.0";
    // OID: Certificate Policies
    private const string OidCertificatePolicies = "2.5.29.32";
    // OID: Name Constraints
    private const string OidNameConstraints = "2.5.29.30";

    /// <summary>Encrypt a private key PEM before storing in the database.</summary>
    private string ProtectKey(string keyPem)
    {
        var protector = dataProtectionProvider.CreateProtector(KeyProtectionPurpose);
        return protector.Protect(keyPem);
    }

    /// <summary>Decrypt a private key PEM retrieved from the database.</summary>
    private string UnprotectKey(string protectedKeyPem)
    {
        // Handle legacy unencrypted keys (before migration)
        if (protectedKeyPem.Contains("-----BEGIN", StringComparison.Ordinal))
            return protectedKeyPem;
        var protector = dataProtectionProvider.CreateProtector(KeyProtectionPurpose);
        return protector.Unprotect(protectedKeyPem);
    }

    /// <summary>Generate a cryptographically random serial number (RFC 5280 §4.1.2.2).</summary>
    private static string GenerateRandomSerialHex()
    {
        Span<byte> serialBytes = stackalloc byte[20]; // 160-bit random serial
        RandomNumberGenerator.Fill(serialBytes);
        serialBytes[0] &= 0x7F; // Ensure positive (clear high bit per X.690 DER)
        if (serialBytes[0] == 0) serialBytes[0] = 0x01; // Avoid leading zero byte
        return Convert.ToHexString(serialBytes);
    }

    /// <summary>
    /// Create an asymmetric key pair. Supports RSA and ECDSA (P-256, P-384, P-521).
    /// Google/Cloudflare prefer ECDSA P-256/P-384 for performance; RSA 4096 for compatibility.
    /// </summary>
    private static (AsymmetricAlgorithm key, string algorithm, int keySize) CreateKeyPair(
        string keyAlgorithm, int requestedKeySize)
    {
        if (keyAlgorithm.Equals("ECDSA", StringComparison.OrdinalIgnoreCase) ||
            keyAlgorithm.Equals("EC", StringComparison.OrdinalIgnoreCase))
        {
            var curve = requestedKeySize switch
            {
                <= 256 => ECCurve.NamedCurves.nistP256,
                <= 384 => ECCurve.NamedCurves.nistP384,
                _ => ECCurve.NamedCurves.nistP521
            };
            var actualSize = requestedKeySize switch
            {
                <= 256 => 256,
                <= 384 => 384,
                _ => 521
            };
            return (ECDsa.Create(curve), "ECDSA", actualSize);
        }

        // Default: RSA
        var rsaSize = requestedKeySize > 0 ? requestedKeySize : 4096;
        return (RSA.Create(rsaSize), "RSA", rsaSize);
    }

    /// <summary>Export private key PEM for RSA or ECDSA.</summary>
    private static string ExportKeyPem(AsymmetricAlgorithm key)
    {
        var keyBytes = key switch
        {
            RSA rsa => rsa.ExportPkcs8PrivateKey(),
            ECDsa ecdsa => ecdsa.ExportPkcs8PrivateKey(),
            _ => throw new InvalidOperationException($"Unsupported key type: {key.GetType().Name}")
        };
        var sb = new StringBuilder();
        sb.AppendLine("-----BEGIN PRIVATE KEY-----");
        sb.AppendLine(Convert.ToBase64String(keyBytes, Base64FormattingOptions.InsertLineBreaks));
        sb.AppendLine("-----END PRIVATE KEY-----");
        return sb.ToString();
    }

    /// <summary>Build Certificate Policies extension (RFC 5280 §4.2.1.4). Used by Google/Amazon/Microsoft.</summary>
    private static X509Extension BuildCertificatePoliciesExtension(string policyOid)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence()) // certificatePolicies SEQUENCE
        {
            using (writer.PushSequence()) // PolicyInformation SEQUENCE
            {
                writer.WriteObjectIdentifier(policyOid); // policyIdentifier
            }
        }
        return new X509Extension(OidCertificatePolicies, writer.Encode(), critical: false);
    }

    /// <summary>Build Name Constraints extension (RFC 5280 §4.2.1.10). Restricts intermediate CA to permitted domains.</summary>
    private static X509Extension BuildNameConstraintsExtension(string[] permittedDomains)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence()) // NameConstraints SEQUENCE
        {
            // permittedSubtrees [0]
            using (writer.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 0, true)))
            {
                foreach (var domain in permittedDomains)
                {
                    using (writer.PushSequence()) // GeneralSubtree SEQUENCE
                    {
                        // dNSName [2] IA5String
                        writer.WriteCharacterString(
                            UniversalTagNumber.IA5String,
                            domain,
                            new Asn1Tag(TagClass.ContextSpecific, 2));
                    }
                }
            }
        }
        return new X509Extension(OidNameConstraints, writer.Encode(), critical: true);
    }

    // ═══════════════════════════════════════════════════════
    // CA Management
    // ═══════════════════════════════════════════════════════

    public async Task<List<CaListItem>> ListCAsAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();

        return await db.CertificateAuthorities
            .AsNoTracking()
            .OrderBy(c => c.CreatedAt)
            .Select(c => new CaListItem(
                c.Id, c.Name, c.CommonName, c.Type, c.Status,
                c.KeyAlgorithm, c.KeySize, c.Fingerprint,
                c.NotBefore, c.NotAfter, c.ParentCaId,
                c.IssuedCertificates.Count(x => x.Status == ManagedCertStatus.Active)))
            .ToListAsync(ct);
    }

    public async Task<CertificateAuthority?> GetCaAsync(Guid id, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();
        return await db.CertificateAuthorities
            .Include(c => c.IssuedCertificates)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    /// <summary>
    /// Create a self-signed Root CA using .NET's X509 APIs.
    /// </summary>
    public async Task<CertificateAuthority> CreateRootCaAsync(CreateCaRequest req, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();

        // Check name uniqueness
        if (await db.CertificateAuthorities.AnyAsync(c => c.Name == req.Name, ct))
            throw new InvalidOperationException($"CA with name '{req.Name}' already exists");

        var keySize = req.KeySize > 0 ? req.KeySize : 4096;
        var keyAlgorithm = req.KeyAlgorithm ?? "RSA";
        if (keyAlgorithm.Equals("RSA", StringComparison.OrdinalIgnoreCase) && keySize < 4096)
            throw new InvalidOperationException("RSA CA key size must be at least 4096 bits");
        var validityDays = req.ValidityDays > 0 ? req.ValidityDays : 3650; // 10 years

        // Build subject DN
        var subject = BuildSubjectName(req.CommonName, req.Organization, req.OrgUnit,
            req.Country, req.State, req.Locality);

        var (key, actualAlgorithm, actualKeySize) = CreateKeyPair(keyAlgorithm, keySize);
        using var _ = key; // Ensure key is disposed

        // Use SHA-384 for Root CA (stronger hash for long-lived trust anchors per NIST SP 800-57)
        CertificateRequest certReq = key switch
        {
            RSA rsa => new CertificateRequest(subject, rsa, HashAlgorithmName.SHA384, RSASignaturePadding.Pkcs1),
            ECDsa ecdsa => new CertificateRequest(subject, ecdsa, HashAlgorithmName.SHA384),
            _ => throw new InvalidOperationException($"Unsupported key type: {key.GetType().Name}")
        };

        // CA extensions
        certReq.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        certReq.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign | X509KeyUsageFlags.DigitalSignature, true));
        certReq.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(certReq.PublicKey, false));

        // Certificate Policies (RFC 5280 §4.2.1.4) — used by Google GTS, Amazon PCA, Microsoft
        certReq.CertificateExtensions.Add(BuildCertificatePoliciesExtension(OidAnyPolicy));

        var notBefore = DateTimeOffset.UtcNow;
        var notAfter = notBefore.AddDays(validityDays);

        using var cert = certReq.CreateSelfSigned(notBefore, notAfter);
        var certPem = ExportCertPem(cert);
        var keyPem = ExportKeyPem(key);
        var fingerprint = cert.GetCertHashString(HashAlgorithmName.SHA256);

        var ca = CertificateAuthority.Create(
            name: req.Name,
            commonName: req.CommonName,
            organization: req.Organization ?? "IVF System",
            orgUnit: req.OrgUnit,
            country: req.Country ?? "VN",
            state: req.State,
            locality: req.Locality,
            type: CaType.Root,
            keyAlgorithm: actualAlgorithm,
            keySize: actualKeySize,
            certificatePem: certPem,
            privateKeyPem: ProtectKey(keyPem),
            fingerprint: fingerprint,
            notBefore: notBefore.UtcDateTime,
            notAfter: notAfter.UtcDateTime,
            chainPem: certPem // Root CA chain is just itself
        );

        db.CertificateAuthorities.Add(ca);
        await AuditAsync(db, CertAuditEventType.CaCreated,
            $"Created Root CA '{req.Name}' (CN={req.CommonName})",
            caId: ca.Id,
            metadata: JsonSerializer.Serialize(new { KeyAlgorithm = actualAlgorithm, KeySize = actualKeySize, ValidityDays = validityDays }));
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Created Root CA: {Name} (CN={CN}, expires {Expiry})",
            ca.Name, ca.CommonName, notAfter);
        return ca;
    }

    // ═══════════════════════════════════════════════════════
    // Certificate Issuance
    // ═══════════════════════════════════════════════════════

    public async Task<List<CertListItem>> ListCertificatesAsync(Guid? caId, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();

        var query = db.ManagedCertificates.AsNoTracking();
        if (caId.HasValue)
            query = query.Where(c => c.IssuingCaId == caId.Value);

        var entities = await query
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        return entities.Select(c => new CertListItem(
                c.Id, c.CommonName, c.SubjectAltNames, c.Type, c.Purpose,
                c.Status, c.Fingerprint, c.SerialNumber,
                c.NotBefore, c.NotAfter, c.IssuingCaId,
                c.DeployedTo, c.DeployedAt,
                c.AutoRenewEnabled, c.RenewBeforeDays,
                c.ReplacedCertId, c.ReplacedByCertId,
                c.LastRenewalAttempt, c.LastRenewalResult,
                c.IsExpiringSoon()))
            .ToList();
    }

    /// <summary>
    /// Issue a new server or client certificate signed by the specified CA.
    /// </summary>
    public async Task<ManagedCertificate> IssueCertificateAsync(IssueCertRequest req, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();

        var ca = await db.CertificateAuthorities.FirstOrDefaultAsync(c => c.Id == req.CaId, ct)
            ?? throw new InvalidOperationException("CA not found");

        if (ca.Status != CaStatus.Active)
            throw new InvalidOperationException("CA is not active");

        var validityDays = req.ValidityDays > 0 ? req.ValidityDays : 365;
        var keySize = req.KeySize > 0 ? req.KeySize : 4096;
        var keyAlgorithm = req.KeyAlgorithm ?? "RSA";
        if (keyAlgorithm.Equals("RSA", StringComparison.OrdinalIgnoreCase) && keySize < 2048)
            throw new InvalidOperationException("RSA certificate key size must be at least 2048 bits");

        // Use cryptographic random serial (RFC 5280 §4.1.2.2 recommends randomized serials)
        var serialHex = GenerateRandomSerialHex();

        // Generate key pair (RSA or ECDSA)
        var (key, actualAlgorithm, actualKeySize) = CreateKeyPair(keyAlgorithm, keySize);
        using var _k = key;
        var subject = new X500DistinguishedName($"CN={req.CommonName}");

        CertificateRequest certReq = key switch
        {
            RSA rsa => new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1),
            ECDsa ecdsa => new CertificateRequest(subject, ecdsa, HashAlgorithmName.SHA256),
            _ => throw new InvalidOperationException($"Unsupported key type: {key.GetType().Name}")
        };

        // Extensions based on type
        if (req.Type == CertType.Server)
        {
            var keyUsage = actualAlgorithm == "ECDSA"
                ? X509KeyUsageFlags.DigitalSignature  // ECDSA doesn't use KeyEncipherment
                : X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment;
            certReq.CertificateExtensions.Add(new X509KeyUsageExtension(keyUsage, true));
            certReq.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
                [new Oid("1.3.6.1.5.5.7.3.1")], false)); // serverAuth
        }
        else
        {
            certReq.CertificateExtensions.Add(new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature, true));
            certReq.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
                [new Oid("1.3.6.1.5.5.7.3.2")], false)); // clientAuth
        }

        certReq.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(certReq.PublicKey, false));

        // Subject Alternative Names
        if (!string.IsNullOrWhiteSpace(req.SubjectAltNames))
        {
            var sanBuilder = new SubjectAlternativeNameBuilder();
            foreach (var san in req.SubjectAltNames.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (System.Net.IPAddress.TryParse(san, out var ip))
                    sanBuilder.AddIpAddress(ip);
                else
                    sanBuilder.AddDnsName(san);
            }
            certReq.CertificateExtensions.Add(sanBuilder.Build());
        }

        // Load CA cert + key for signing (decrypt key if encrypted)
        var caCert = X509Certificate2.CreateFromPem(ca.CertificatePem, UnprotectKey(ca.PrivateKeyPem));

        // Authority Key Identifier — links issued cert to issuing CA for chain validation
        certReq.CertificateExtensions.Add(
            X509AuthorityKeyIdentifierExtension.CreateFromCertificate(
                caCert, includeKeyIdentifier: true, includeIssuerAndSerial: false));

        var notBefore = DateTimeOffset.UtcNow;
        var notAfter = notBefore.AddDays(validityDays);

        // Clamp: issued cert cannot outlive the issuing CA
        if (notAfter > caCert.NotAfter)
            notAfter = caCert.NotAfter.AddMinutes(-1);

        var serialBytes = Convert.FromHexString(serialHex);

        using var signedCert = certReq.Create(caCert, notBefore, notAfter, serialBytes);
        var certPem = ExportCertPem(signedCert);
        var keyPem = ExportKeyPem(key);
        var fingerprint = signedCert.GetCertHashString(HashAlgorithmName.SHA256);

        var managedCert = ManagedCertificate.Create(
            commonName: req.CommonName,
            subjectAltNames: req.SubjectAltNames,
            type: req.Type,
            purpose: req.Purpose,
            certificatePem: certPem,
            privateKeyPem: ProtectKey(keyPem),
            fingerprint: fingerprint,
            serialNumber: serialHex,
            notBefore: notBefore.UtcDateTime,
            notAfter: notAfter.UtcDateTime,
            keyAlgorithm: actualAlgorithm,
            keySize: actualKeySize,
            issuingCaId: ca.Id,
            renewBeforeDays: req.RenewBeforeDays > 0 ? req.RenewBeforeDays : 30
        );

        db.ManagedCertificates.Add(managedCert);
        await db.SaveChangesAsync(ct);

        await AuditAsync(db, CertAuditEventType.CertIssued,
            $"Issued {req.Type} cert CN={req.CommonName} serial={serialHex} purpose={req.Purpose}",
            certificateId: managedCert.Id, caId: ca.Id,
            metadata: JsonSerializer.Serialize(new { req.CommonName, req.Purpose, SerialNumber = serialHex, KeyAlgorithm = actualAlgorithm, KeySize = actualKeySize }));
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Issued {Type} cert: CN={CN}, purpose={Purpose}, expires={Expiry}, serial={Serial}",
            req.Type, req.CommonName, req.Purpose, notAfter, serialHex);

        return managedCert;
    }

    // ═══════════════════════════════════════════════════════
    // Certificate Rotation / Renewal
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Renew a certificate: generate new key pair + cert with same CN/SANs/purpose,
    /// mark old cert as superseded, and link the rotation chain.
    /// </summary>
    public async Task<ManagedCertificate> RenewCertificateAsync(Guid certId, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();

        var oldCert = await db.ManagedCertificates.FirstOrDefaultAsync(c => c.Id == certId, ct)
            ?? throw new InvalidOperationException("Certificate not found");

        if (oldCert.Status != ManagedCertStatus.Active)
            throw new InvalidOperationException($"Cannot renew a certificate with status {oldCert.Status}");

        // Issue a replacement with same parameters (including key algorithm)
        var newCert = await IssueCertificateInternalAsync(db, new IssueCertRequest(
            CaId: oldCert.IssuingCaId,
            CommonName: oldCert.CommonName,
            SubjectAltNames: oldCert.SubjectAltNames,
            Type: oldCert.Type,
            Purpose: oldCert.Purpose,
            ValidityDays: oldCert.ValidityDays,
            KeySize: oldCert.KeySize,
            RenewBeforeDays: oldCert.RenewBeforeDays,
            KeyAlgorithm: oldCert.KeyAlgorithm
        ), ct);

        // Link rotation chain
        newCert.SetReplacedCert(oldCert.Id);
        oldCert.MarkSuperseded(newCert.Id);
        oldCert.RecordRenewalAttempt($"Renewed → {newCert.Id}");

        await AuditAsync(db, CertAuditEventType.CertRenewed,
            $"Renewed cert CN={oldCert.CommonName} ({oldCert.Id} → {newCert.Id})",
            certificateId: newCert.Id, caId: oldCert.IssuingCaId,
            metadata: JsonSerializer.Serialize(new { OldCertId = oldCert.Id, NewCertId = newCert.Id, oldCert.CommonName }));

        await db.SaveChangesAsync(ct);

        logger.LogInformation("Renewed cert {OldId} → {NewId} (CN={CN}, purpose={Purpose})",
            oldCert.Id, newCert.Id, oldCert.CommonName, oldCert.Purpose);

        return newCert;
    }

    /// <summary>
    /// Get all certificates needing renewal (expiring within their RenewBeforeDays window).
    /// </summary>
    public async Task<List<CertListItem>> GetExpiringCertificatesAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();

        var now = DateTime.UtcNow;
        return await db.ManagedCertificates
            .AsNoTracking()
            .Where(c => c.Status == ManagedCertStatus.Active && c.ReplacedByCertId == null)
            .ToListAsync(ct)
            .ContinueWith(t => t.Result
                .Where(c => c.IsExpiringSoon())
                .Select(c => new CertListItem(
                    c.Id, c.CommonName, c.SubjectAltNames, c.Type, c.Purpose,
                    c.Status, c.Fingerprint, c.SerialNumber,
                    c.NotBefore, c.NotAfter, c.IssuingCaId,
                    c.DeployedTo, c.DeployedAt,
                    c.AutoRenewEnabled, c.RenewBeforeDays,
                    c.ReplacedCertId, c.ReplacedByCertId,
                    c.LastRenewalAttempt, c.LastRenewalResult,
                    c.IsExpiringSoon()))
                .ToList(), ct);
    }

    /// <summary>
    /// Auto-renew all certificates that have AutoRenewEnabled and are expiring soon.
    /// Called by CertAutoRenewalService on a schedule.
    /// </summary>
    public async Task<CertRenewalBatchResult> AutoRenewExpiringAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();

        var candidates = await db.ManagedCertificates
            .Where(c => c.Status == ManagedCertStatus.Active
                     && c.AutoRenewEnabled
                     && c.ReplacedByCertId == null)
            .ToListAsync(ct);

        var needsRenewal = candidates.Where(c => c.NeedsAutoRenewal()).ToList();

        if (needsRenewal.Count == 0)
            return new CertRenewalBatchResult(0, 0, []);

        var results = new List<CertRenewalResult>();
        var renewed = 0;

        foreach (var cert in needsRenewal)
        {
            try
            {
                var newCert = await IssueCertificateInternalAsync(db, new IssueCertRequest(
                    CaId: cert.IssuingCaId,
                    CommonName: cert.CommonName,
                    SubjectAltNames: cert.SubjectAltNames,
                    Type: cert.Type,
                    Purpose: cert.Purpose,
                    ValidityDays: cert.ValidityDays,
                    KeySize: cert.KeySize,
                    RenewBeforeDays: cert.RenewBeforeDays,
                    KeyAlgorithm: cert.KeyAlgorithm
                ), ct);

                newCert.SetReplacedCert(cert.Id);
                cert.MarkSuperseded(newCert.Id);
                cert.RecordRenewalAttempt($"Auto-renewed → {newCert.Id}");
                renewed++;

                results.Add(new CertRenewalResult(cert.Id, newCert.Id, cert.CommonName, cert.Purpose, true, "OK"));

                logger.LogInformation("Auto-renewed cert {Id} (CN={CN}, purpose={Purpose}) → {NewId}",
                    cert.Id, cert.CommonName, cert.Purpose, newCert.Id);
            }
            catch (Exception ex)
            {
                cert.RecordRenewalAttempt($"FAILED: {ex.Message}");
                results.Add(new CertRenewalResult(cert.Id, null, cert.CommonName, cert.Purpose, false, ex.Message));
                logger.LogError(ex, "Failed to auto-renew cert {Id} (CN={CN})", cert.Id, cert.CommonName);
            }
        }

        await db.SaveChangesAsync(ct);
        return new CertRenewalBatchResult(needsRenewal.Count, renewed, results);
    }

    // ═══════════════════════════════════════════════════════
    // Certificate Revocation (with CRL generation + audit)
    // ═══════════════════════════════════════════════════════

    public async Task RevokeCertificateAsync(Guid certId, RevocationReason reason, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();

        var cert = await db.ManagedCertificates.FirstOrDefaultAsync(c => c.Id == certId, ct)
            ?? throw new InvalidOperationException("Certificate not found");

        cert.Revoke(reason);
        await AuditAsync(db, CertAuditEventType.CertRevoked,
            $"Revoked cert CN={cert.CommonName} serial={cert.SerialNumber} reason={reason}",
            certificateId: certId, caId: cert.IssuingCaId,
            metadata: JsonSerializer.Serialize(new { cert.SerialNumber, cert.Fingerprint, Reason = reason.ToString() }));
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Revoked cert {Id} (CN={CN}, reason={Reason})", certId, cert.CommonName, reason);

        // Generate new CRL to include the revoked certificate
        try
        {
            await GenerateCrlAsync(cert.IssuingCaId, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to auto-generate CRL after revoking cert {Id}", certId);
        }
    }

    // ═══════════════════════════════════════════════════════
    // CRL (Certificate Revocation List) Generation
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Generate a new CRL for the specified CA containing all revoked certificates.
    /// Follows RFC 5280 §5 structure: signed by CA key, includes CRL Number extension.
    /// </summary>
    public async Task<CertificateRevocationList> GenerateCrlAsync(Guid caId, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();

        var ca = await db.CertificateAuthorities.FirstOrDefaultAsync(c => c.Id == caId, ct)
            ?? throw new InvalidOperationException("CA not found");

        if (ca.Status != CaStatus.Active)
            throw new InvalidOperationException("Cannot generate CRL for inactive CA");

        // Get all revoked certs for this CA
        var revokedCerts = await db.ManagedCertificates
            .Where(c => c.IssuingCaId == caId && c.Status == ManagedCertStatus.Revoked)
            .ToListAsync(ct);

        var crlNumber = ca.AllocateCrlNumber();
        var thisUpdate = DateTime.UtcNow;
        var nextUpdate = thisUpdate.AddDays(14); // CRL valid for 14 days

        using var caCert = X509Certificate2.CreateFromPem(ca.CertificatePem, UnprotectKey(ca.PrivateKeyPem));
        using var caKey = caCert.GetRSAPrivateKey()
            ?? throw new InvalidOperationException("CA key is not RSA");

        // Build CRL using .NET's CertificateRevocationListBuilder
        var crlBuilder = new CertificateRevocationListBuilder();
        foreach (var cert in revokedCerts)
        {
            // Serial number must be big-endian with no redundant leading zeros
            var hexSerial = cert.SerialNumber.TrimStart('0');
            if (hexSerial.Length == 0) hexSerial = "0";
            if (hexSerial.Length % 2 != 0) hexSerial = "0" + hexSerial;
            var serialBytes = Convert.FromHexString(hexSerial);
            crlBuilder.AddEntry(serialBytes, cert.RevokedAt ?? cert.UpdatedAt ?? DateTime.UtcNow);
        }

        var crlDer = crlBuilder.Build(
            caCert,
            crlNumber,
            nextUpdate,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1,
            thisUpdate);

        var crlPem = ExportCrlPem(crlDer);
        var fingerprint = Convert.ToHexString(SHA256.HashData(crlDer));

        var crl = CertificateRevocationList.Create(
            caId: caId,
            crlNumber: crlNumber,
            thisUpdate: thisUpdate,
            nextUpdate: nextUpdate,
            crlPem: crlPem,
            crlDer: crlDer,
            revokedCount: revokedCerts.Count,
            fingerprint: fingerprint);

        db.CertificateRevocationLists.Add(crl);
        await AuditAsync(db, CertAuditEventType.CrlGenerated,
            $"Generated CRL #{crlNumber} with {revokedCerts.Count} revoked cert(s)",
            caId: caId,
            metadata: JsonSerializer.Serialize(new { CrlNumber = crlNumber, RevokedCount = revokedCerts.Count, NextUpdate = nextUpdate }));
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Generated CRL #{Num} for CA {CaId} with {Count} revoked certs, valid until {Next}",
            crlNumber, caId, revokedCerts.Count, nextUpdate);

        return crl;
    }

    /// <summary>
    /// Get the latest CRL for a CA in PEM format.
    /// </summary>
    public async Task<CertificateRevocationList?> GetLatestCrlAsync(Guid caId, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();

        return await db.CertificateRevocationLists
            .AsNoTracking()
            .Where(c => c.CaId == caId)
            .OrderByDescending(c => c.CrlNumber)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// List all CRLs for a CA.
    /// </summary>
    public async Task<List<CrlListItem>> ListCrlsAsync(Guid caId, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();

        return await db.CertificateRevocationLists
            .AsNoTracking()
            .Where(c => c.CaId == caId)
            .OrderByDescending(c => c.CrlNumber)
            .Select(c => new CrlListItem(c.Id, c.CrlNumber, c.ThisUpdate, c.NextUpdate,
                c.RevokedCount, c.Fingerprint))
            .ToListAsync(ct);
    }

    // ═══════════════════════════════════════════════════════
    // OCSP Responder (Inline)
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Check certificate revocation status (OCSP-like query).
    /// Returns good/revoked/unknown per RFC 6960.
    /// </summary>
    public async Task<OcspResponse> CheckCertStatusAsync(string serialNumber, Guid caId, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();

        var cert = await db.ManagedCertificates
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.SerialNumber == serialNumber && c.IssuingCaId == caId, ct);

        await AuditAsync(db, CertAuditEventType.OcspQuery,
            $"OCSP query for serial={serialNumber}",
            caId: caId, certificateId: cert?.Id,
            metadata: JsonSerializer.Serialize(new { SerialNumber = serialNumber }));
        await db.SaveChangesAsync(ct);

        if (cert == null)
            return new OcspResponse(OcspCertStatus.Unknown, serialNumber, null, null, DateTime.UtcNow);

        return cert.Status switch
        {
            ManagedCertStatus.Revoked => new OcspResponse(
                OcspCertStatus.Revoked, serialNumber,
                cert.RevokedAt, cert.RevocationReason, DateTime.UtcNow),
            ManagedCertStatus.Active or ManagedCertStatus.Superseded => new OcspResponse(
                OcspCertStatus.Good, serialNumber, null, null, DateTime.UtcNow),
            _ => new OcspResponse(OcspCertStatus.Unknown, serialNumber, null, null, DateTime.UtcNow)
        };
    }

    // ═══════════════════════════════════════════════════════
    // Intermediate CA Creation
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Create an Intermediate CA signed by an existing Root CA.
    /// The chain PEM includes the intermediate cert + root CA cert.
    /// </summary>
    public async Task<CertificateAuthority> CreateIntermediateCaAsync(
        CreateIntermediateCaRequest req, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();

        var parentCa = await db.CertificateAuthorities.FirstOrDefaultAsync(c => c.Id == req.ParentCaId, ct)
            ?? throw new InvalidOperationException("Parent CA not found");

        if (parentCa.Status != CaStatus.Active)
            throw new InvalidOperationException("Parent CA is not active");

        if (await db.CertificateAuthorities.AnyAsync(c => c.Name == req.Name, ct))
            throw new InvalidOperationException($"CA with name '{req.Name}' already exists");

        var keySize = req.KeySize > 0 ? req.KeySize : 4096;
        var keyAlgorithm = req.KeyAlgorithm ?? "RSA";
        if (keyAlgorithm.Equals("RSA", StringComparison.OrdinalIgnoreCase) && keySize < 4096)
            throw new InvalidOperationException("RSA CA key size must be at least 4096 bits");
        var validityDays = req.ValidityDays > 0 ? req.ValidityDays : 1825; // 5 years

        var subject = BuildSubjectName(req.CommonName, req.Organization, req.OrgUnit,
            req.Country, req.State, req.Locality);

        var (key, actualAlgorithm, actualKeySize) = CreateKeyPair(keyAlgorithm, keySize);
        using var _k = key;

        // Use SHA-384 for Intermediate CA (long-lived issuing CAs per NIST SP 800-57)
        CertificateRequest certReq = key switch
        {
            RSA rsa => new CertificateRequest(subject, rsa, HashAlgorithmName.SHA384, RSASignaturePadding.Pkcs1),
            ECDsa ecdsa => new CertificateRequest(subject, ecdsa, HashAlgorithmName.SHA384),
            _ => throw new InvalidOperationException($"Unsupported key type: {key.GetType().Name}")
        };

        // Intermediate CA extensions: pathLenConstraint=0 (can issue end-entity certs only)
        certReq.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, true, 0, true));
        certReq.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign | X509KeyUsageFlags.DigitalSignature, true));
        certReq.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(certReq.PublicKey, false));

        // Certificate Policies (RFC 5280 §4.2.1.4)
        certReq.CertificateExtensions.Add(BuildCertificatePoliciesExtension(OidAnyPolicy));

        // Name Constraints (RFC 5280 §4.2.1.10) — restrict intermediate CA to permitted domains
        if (req.PermittedDomains is { Length: > 0 })
            certReq.CertificateExtensions.Add(BuildNameConstraintsExtension(req.PermittedDomains));

        // Load parent CA cert + key for signing (decrypt key if encrypted)
        using var parentCert = X509Certificate2.CreateFromPem(parentCa.CertificatePem, UnprotectKey(parentCa.PrivateKeyPem));

        // Authority Key Identifier — links intermediate CA to parent CA for chain validation
        certReq.CertificateExtensions.Add(
            X509AuthorityKeyIdentifierExtension.CreateFromCertificate(
                parentCert, includeKeyIdentifier: true, includeIssuerAndSerial: false));

        var serialHex = GenerateRandomSerialHex();
        var serialBytes = Convert.FromHexString(serialHex);

        var notBefore = DateTimeOffset.UtcNow;
        var notAfter = notBefore.AddDays(validityDays);

        // Clamp: intermediate cannot outlive parent
        if (notAfter > parentCert.NotAfter)
            notAfter = parentCert.NotAfter.AddMinutes(-1);

        using var signedCert = certReq.Create(parentCert, notBefore, notAfter, serialBytes);
        var certPem = ExportCertPem(signedCert);
        var keyPem = ExportKeyPem(key);
        var fingerprint = signedCert.GetCertHashString(HashAlgorithmName.SHA256);

        // Chain = this cert + parent chain
        var chainPem = certPem + (parentCa.ChainPem ?? parentCa.CertificatePem);

        var ca = CertificateAuthority.Create(
            name: req.Name,
            commonName: req.CommonName,
            organization: req.Organization ?? parentCa.Organization,
            orgUnit: req.OrgUnit ?? parentCa.OrganizationalUnit,
            country: req.Country ?? parentCa.Country,
            state: req.State,
            locality: req.Locality,
            type: CaType.Intermediate,
            keyAlgorithm: actualAlgorithm,
            keySize: actualKeySize,
            certificatePem: certPem,
            privateKeyPem: ProtectKey(keyPem),
            fingerprint: fingerprint,
            notBefore: notBefore.UtcDateTime,
            notAfter: notAfter.UtcDateTime,
            parentCaId: parentCa.Id,
            chainPem: chainPem
        );

        db.CertificateAuthorities.Add(ca);
        await AuditAsync(db, CertAuditEventType.IntermediateCaCreated,
            $"Created Intermediate CA '{req.Name}' (CN={req.CommonName}) signed by '{parentCa.Name}'",
            caId: ca.Id,
            metadata: JsonSerializer.Serialize(new { ParentCaId = parentCa.Id, ParentCaName = parentCa.Name, KeyAlgorithm = actualAlgorithm, KeySize = actualKeySize }));
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Created Intermediate CA: {Name} (CN={CN}, parent={Parent}, expires {Expiry})",
            ca.Name, ca.CommonName, parentCa.Name, notAfter);

        return ca;
    }

    // ═══════════════════════════════════════════════════════
    // Certificate Audit Trail
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// List audit events with optional filtering by cert, CA, or event type.
    /// </summary>
    public async Task<List<CertAuditItem>> ListAuditEventsAsync(
        Guid? certId, Guid? caId, CertAuditEventType? eventType, int limit, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();

        var query = db.CertificateAuditEvents.AsNoTracking().AsQueryable();
        if (certId.HasValue) query = query.Where(e => e.CertificateId == certId.Value);
        if (caId.HasValue) query = query.Where(e => e.CaId == caId.Value);
        if (eventType.HasValue) query = query.Where(e => e.EventType == eventType.Value);

        return await query
            .OrderByDescending(e => e.CreatedAt)
            .Take(limit)
            .Select(e => new CertAuditItem(
                e.Id, e.CertificateId, e.CaId, e.EventType,
                e.Description, e.Actor, e.SourceIp,
                e.Metadata, e.Success, e.ErrorMessage, e.CreatedAt))
            .ToListAsync(ct);
    }

    /// <summary>Helper to record an audit event in the current DbContext.</summary>
    private static async Task AuditAsync(IvfDbContext db, CertAuditEventType eventType, string description,
        string actor = "system", Guid? certificateId = null, Guid? caId = null,
        string? sourceIp = null, string? metadata = null, bool success = true, string? errorMessage = null)
    {
        db.CertificateAuditEvents.Add(CertificateAuditEvent.Create(
            eventType, description, actor, certificateId, caId,
            sourceIp, metadata, success, errorMessage));
    }

    // ═══════════════════════════════════════════════════════
    // Certificate Auto-Renew Configuration
    // ═══════════════════════════════════════════════════════

    public async Task SetAutoRenewAsync(Guid certId, bool enabled, int? renewBeforeDays, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();

        var cert = await db.ManagedCertificates.FirstOrDefaultAsync(c => c.Id == certId, ct)
            ?? throw new InvalidOperationException("Certificate not found");

        cert.SetAutoRenew(enabled, renewBeforeDays);
        await db.SaveChangesAsync(ct);
    }

    // ═══════════════════════════════════════════════════════
    // Certificate Deployment (write to Docker volume / file)
    // ═══════════════════════════════════════════════════════

    /// <summary>Emit log line to SignalR group + persist to entity.</summary>
    private async Task EmitLog(string operationId, CertDeploymentLog log, string level, string message)
    {
        log.AddLine(level, message);
        try
        {
            await hubContext.Clients.Group(operationId).SendAsync("DeployLog", new
            {
                operationId,
                timestamp = DateTime.UtcNow,
                level,
                message
            });
        }
        catch { /* SignalR send failure — non-critical */ }
    }

    private async Task EmitStatus(string operationId, string status, string? error = null)
    {
        try
        {
            await hubContext.Clients.Group(operationId).SendAsync("DeployStatus", new
            {
                operationId,
                status,
                completedAt = status != "Running" ? DateTime.UtcNow : (DateTime?)null,
                error
            });
        }
        catch { /* SignalR send failure — non-critical */ }
    }

    /// <summary>
    /// Deploy cert + key + CA chain to a Docker container volume path.
    /// Used for PostgreSQL SSL, MinIO TLS, etc.
    /// </summary>
    public async Task<CertDeployResult> DeployCertificateAsync(
        Guid certId, string container, string certPath, string keyPath, string? caPath,
        CancellationToken ct, string? operationId = null, CertDeploymentLog? existingLog = null)
    {
        // Use a long-lived CTS independent of HTTP request so deploys aren't canceled mid-operation
        using var deployCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        var deployToken = deployCts.Token;

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();

        var cert = await db.ManagedCertificates
            .Include(c => c.IssuingCa)
            .FirstOrDefaultAsync(c => c.Id == certId, deployToken)
            ?? throw new InvalidOperationException("Certificate not found");

        var opId = operationId ?? Guid.NewGuid().ToString("N")[..12];
        var log = existingLog ?? new CertDeploymentLog
        {
            CertificateId = certId,
            OperationId = opId,
            Target = "custom",
            Container = container
        };
        if (existingLog == null)
            db.CertDeploymentLogs.Add(log);

        var steps = new List<string>();
        await EmitStatus(opId, "Running");
        await EmitLog(opId, log, "info", $"Starting deploy to {container}");

        try
        {
            // Write cert PEM
            await WriteToContainerAsync(container, certPath, cert.CertificatePem, deployToken);
            var step = $"✓ Certificate → {container}:{certPath}";
            steps.Add(step);
            await EmitLog(opId, log, "success", step);

            // Write key PEM (decrypt before writing, set restrictive permissions)
            await WriteToContainerAsync(container, keyPath, UnprotectKey(cert.PrivateKeyPem), deployToken);
            await RunCommandAsync($"docker exec {container} chmod 600 {keyPath}", deployToken);
            await RunCommandAsync($"docker exec {container} chown 999:999 {keyPath}", deployToken);
            step = $"✓ Private key → {container}:{keyPath} (chmod 600)";
            steps.Add(step);
            await EmitLog(opId, log, "success", step);

            // Write CA chain if requested
            if (!string.IsNullOrWhiteSpace(caPath) && cert.IssuingCa?.ChainPem != null)
            {
                await WriteToContainerAsync(container, caPath, cert.IssuingCa.ChainPem, deployToken);
                step = $"✓ CA chain → {container}:{caPath}";
                steps.Add(step);
                await EmitLog(opId, log, "success", step);
            }

            // Also set cert file permissions
            await RunCommandAsync($"docker exec {container} chmod 644 {certPath}", deployToken);
            await RunCommandAsync($"docker exec {container} chown 999:999 {certPath}", deployToken);

            cert.MarkDeployed($"{container}:{certPath}");
            log.Complete(true);
            await db.SaveChangesAsync(CancellationToken.None);

            await EmitLog(opId, log, "success", "Deploy completed successfully");
            await EmitStatus(opId, "Completed");

            return new CertDeployResult(true, steps, opId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to deploy cert {Id} to {Container}", certId, container);
            var errorStep = $"✗ Error: {ex.Message}";
            steps.Add(errorStep);
            await EmitLog(opId, log, "error", errorStep);
            log.Complete(false, ex.Message);
            try { await db.SaveChangesAsync(CancellationToken.None); } catch { /* best-effort save */ }
            await EmitStatus(opId, "Failed", ex.Message);
            return new CertDeployResult(false, steps, opId);
        }
    }

    /// <summary>
    /// Deploy PostgreSQL SSL certificates to the primary DB container and reload config.
    /// </summary>
    public async Task<CertDeployResult> DeployPgSslAsync(Guid certId, CancellationToken ct)
    {
        const string container = "ivf-db";
        const string pgData = "/var/lib/postgresql/data";
        var opId = Guid.NewGuid().ToString("N")[..12];

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();
        var log = new CertDeploymentLog
        {
            CertificateId = certId,
            OperationId = opId,
            Target = "pg-primary",
            Container = container
        };
        db.CertDeploymentLogs.Add(log);
        await db.SaveChangesAsync(CancellationToken.None);

        var result = await DeployCertificateAsync(certId,
            container,
            $"{pgData}/server.crt",
            $"{pgData}/server.key",
            $"{pgData}/root.crt",
            ct, opId, log);

        if (result.Success)
        {
            // Fix ownership — detect the actual postgres UID inside the container
            await RunCommandAsync(
                $"docker exec {container} chown postgres:postgres {pgData}/server.crt {pgData}/server.key {pgData}/root.crt", CancellationToken.None);
            await RunCommandAsync(
                $"docker exec {container} chmod 600 {pgData}/server.key", CancellationToken.None);
            await RunCommandAsync(
                $"docker exec {container} chmod 644 {pgData}/server.crt {pgData}/root.crt", CancellationToken.None);

            await EmitLog(opId, log, "info", "Enabling SSL and reloading PostgreSQL config...");
            await RunCommandAsync(
                $"docker exec {container} psql -U postgres -d postgres -c \"ALTER SYSTEM SET ssl = on; ALTER SYSTEM SET ssl_cert_file = 'server.crt'; ALTER SYSTEM SET ssl_key_file = 'server.key'; ALTER SYSTEM SET ssl_ca_file = 'root.crt';\"", CancellationToken.None);
            await RunCommandAsync(
                $"docker exec {container} psql -U postgres -d postgres -c \"SELECT pg_reload_conf();\"", CancellationToken.None);
            var step = "✓ SSL enabled + config reloaded on primary PostgreSQL";
            await EmitLog(opId, log, "success", step);
            result = result with { Steps = [.. result.Steps, step] };
        }
        await SaveLogAsync(log);
        return result;
    }

    /// <summary>
    /// Deploy MinIO TLS certificates to the primary MinIO container and restart.
    /// </summary>
    public async Task<CertDeployResult> DeployMinioSslAsync(Guid certId, CancellationToken ct)
    {
        const string container = "ivf-minio";
        const string certsDir = "/root/.minio/certs";
        var opId = Guid.NewGuid().ToString("N")[..12];

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();
        var log = new CertDeploymentLog
        {
            CertificateId = certId,
            OperationId = opId,
            Target = "minio-primary",
            Container = container
        };
        db.CertDeploymentLogs.Add(log);
        await db.SaveChangesAsync(CancellationToken.None);

        var result = await DeployCertificateAsync(certId,
            container,
            $"{certsDir}/public.crt",
            $"{certsDir}/private.key",
            $"{certsDir}/CAs/ca.crt",
            ct, opId, log);

        if (result.Success)
        {
            await EmitLog(opId, log, "info", "Restarting MinIO container...");
            await RunCommandAsync($"docker restart {container}", CancellationToken.None, timeoutSeconds: 60);
            var step = "✓ MinIO container restarted to load new TLS certificates";
            await EmitLog(opId, log, "success", step);
            result = result with { Steps = [.. result.Steps, step] };
        }
        await SaveLogAsync(log);
        return result;
    }

    /// <summary>
    /// Deploy cert + key + CA chain to a remote server via SSH → docker exec.
    /// Reads replica host from CloudReplicationConfig.
    /// </summary>
    public async Task<CertDeployResult> DeployToRemoteContainerAsync(
        Guid certId, string container, string certPath, string keyPath, string? caPath,
        CancellationToken ct, string? operationId = null, CertDeploymentLog? existingLog = null)
    {
        // Use a long-lived CTS independent of HTTP request so remote deploys aren't canceled mid-operation
        using var deployCts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        var deployToken = deployCts.Token;

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();

        var config = await db.Set<CloudReplicationConfig>().FirstOrDefaultAsync(deployToken)
            ?? throw new InvalidOperationException("Cloud replication config not found");
        var remoteHost = config.RemoteDbHost
            ?? throw new InvalidOperationException("Remote host not configured in replication settings");

        var cert = await db.ManagedCertificates
            .Include(c => c.IssuingCa)
            .FirstOrDefaultAsync(c => c.Id == certId, deployToken)
            ?? throw new InvalidOperationException("Certificate not found");

        var opId = operationId ?? Guid.NewGuid().ToString("N")[..12];
        var log = existingLog ?? new CertDeploymentLog
        {
            CertificateId = certId,
            OperationId = opId,
            Target = "remote",
            Container = container,
            RemoteHost = remoteHost
        };
        if (existingLog == null)
            db.CertDeploymentLogs.Add(log);

        var steps = new List<string>();
        var sshOpts = "-o StrictHostKeyChecking=accept-new -o ConnectTimeout=30 -o ServerAliveInterval=10 -o ServerAliveCountMax=3";
        var sshPrefix = $"ssh {sshOpts} root@{remoteHost}";

        await EmitStatus(opId, "Running");
        await EmitLog(opId, log, "info", $"Connecting to {remoteHost} via SSH...");

        try
        {
            // Write cert PEM to remote container
            await WriteToRemoteContainerAsync(remoteHost, container, certPath, cert.CertificatePem, deployToken);
            var step = $"✓ Certificate → {remoteHost}:{container}:{certPath}";
            steps.Add(step);
            await EmitLog(opId, log, "success", step);

            // Write key PEM (decrypt before writing)
            await WriteToRemoteContainerAsync(remoteHost, container, keyPath, UnprotectKey(cert.PrivateKeyPem), deployToken);
            await RunCommandAsync($"{sshPrefix} docker exec {container} chmod 600 {keyPath}", deployToken);
            await RunCommandAsync($"{sshPrefix} docker exec {container} chown 999:999 {keyPath}", deployToken);
            step = $"✓ Private key → {remoteHost}:{container}:{keyPath} (chmod 600)";
            steps.Add(step);
            await EmitLog(opId, log, "success", step);

            // Write CA chain
            if (!string.IsNullOrWhiteSpace(caPath) && cert.IssuingCa?.ChainPem != null)
            {
                await WriteToRemoteContainerAsync(remoteHost, container, caPath, cert.IssuingCa.ChainPem, deployToken);
                step = $"✓ CA chain → {remoteHost}:{container}:{caPath}";
                steps.Add(step);
                await EmitLog(opId, log, "success", step);
            }

            // Set cert file permissions
            await RunCommandAsync($"{sshPrefix} docker exec {container} chmod 644 {certPath}", deployToken);
            await RunCommandAsync($"{sshPrefix} docker exec {container} chown 999:999 {certPath}", deployToken);

            cert.MarkDeployed($"{remoteHost}:{container}:{certPath}");
            log.Complete(true);
            await db.SaveChangesAsync(CancellationToken.None);

            await EmitLog(opId, log, "success", "Remote deploy completed successfully");
            await EmitStatus(opId, "Completed");

            return new CertDeployResult(true, steps, opId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to deploy cert {Id} to remote {Host}:{Container}", certId, remoteHost, container);
            var errorStep = $"✗ Error: {ex.Message}";
            steps.Add(errorStep);
            await EmitLog(opId, log, "error", errorStep);
            log.Complete(false, ex.Message);
            try { await db.SaveChangesAsync(CancellationToken.None); } catch { /* best-effort save */ }
            await EmitStatus(opId, "Failed", ex.Message);
            return new CertDeployResult(false, steps, opId);
        }
    }

    /// <summary>
    /// Deploy SSL certs to the replica PostgreSQL container via SSH and reload.
    /// </summary>
    public async Task<CertDeployResult> DeployReplicaPgSslAsync(Guid certId, CancellationToken ct)
    {
        const string container = "ivf-db-replica";
        const string pgData = "/var/lib/postgresql/data";
        var opId = Guid.NewGuid().ToString("N")[..12];

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();
        var log = new CertDeploymentLog
        {
            CertificateId = certId,
            OperationId = opId,
            Target = "pg-replica",
            Container = container
        };
        db.CertDeploymentLogs.Add(log);
        await db.SaveChangesAsync(CancellationToken.None);

        var result = await DeployToRemoteContainerAsync(certId,
            container,
            $"{pgData}/server.crt",
            $"{pgData}/server.key",
            $"{pgData}/root.crt",
            ct, opId, log);

        if (result.Success)
        {
            var config = await db.Set<CloudReplicationConfig>().FirstOrDefaultAsync(CancellationToken.None);
            var remoteHost = config?.RemoteDbHost ?? "unknown";
            var sshPrefix = $"ssh -o StrictHostKeyChecking=accept-new root@{remoteHost}";

            // Fix ownership to postgres user inside the container
            await RunCommandAsync(
                $"{sshPrefix} docker exec {container} chown postgres:postgres {pgData}/server.crt {pgData}/server.key {pgData}/root.crt", CancellationToken.None);
            await RunCommandAsync(
                $"{sshPrefix} docker exec {container} chmod 600 {pgData}/server.key", CancellationToken.None);
            await RunCommandAsync(
                $"{sshPrefix} docker exec {container} chmod 644 {pgData}/server.crt {pgData}/root.crt", CancellationToken.None);

            await EmitLog(opId, log, "info", "Enabling SSL and reloading replica PostgreSQL config...");
            await RunCommandAsync(
                $"{sshPrefix} docker exec {container} psql -U postgres -d postgres -c \"ALTER SYSTEM SET ssl = on; ALTER SYSTEM SET ssl_cert_file = 'server.crt'; ALTER SYSTEM SET ssl_key_file = 'server.key'; ALTER SYSTEM SET ssl_ca_file = 'root.crt';\"", CancellationToken.None);
            await RunCommandAsync(
                $"{sshPrefix} docker exec {container} psql -U postgres -d postgres -c \"SELECT pg_reload_conf();\"", CancellationToken.None);
            var step = $"✓ SSL enabled + config reloaded on replica PostgreSQL ({remoteHost})";
            await EmitLog(opId, log, "success", step);
            result = result with { Steps = [.. result.Steps, step] };
        }
        await SaveLogAsync(log);
        return result;
    }

    /// <summary>
    /// Deploy TLS certs to the replica MinIO container via SSH and restart.
    /// </summary>
    public async Task<CertDeployResult> DeployReplicaMinioSslAsync(Guid certId, CancellationToken ct)
    {
        const string container = "ivf-minio-replica";
        const string certsDir = "/root/.minio/certs";
        var opId = Guid.NewGuid().ToString("N")[..12];

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();
        var log = new CertDeploymentLog
        {
            CertificateId = certId,
            OperationId = opId,
            Target = "minio-replica",
            Container = container
        };
        db.CertDeploymentLogs.Add(log);
        await db.SaveChangesAsync(CancellationToken.None);

        var result = await DeployToRemoteContainerAsync(certId,
            container,
            $"{certsDir}/public.crt",
            $"{certsDir}/private.key",
            $"{certsDir}/CAs/ca.crt",
            ct, opId, log);

        if (result.Success)
        {
            var config = await db.Set<CloudReplicationConfig>().FirstOrDefaultAsync(CancellationToken.None);
            var remoteHost = config?.RemoteDbHost ?? "unknown";

            await EmitLog(opId, log, "info", $"Restarting MinIO replica on {remoteHost}...");
            await RunCommandAsync(
                $"ssh -o StrictHostKeyChecking=accept-new root@{remoteHost} docker restart {container}", CancellationToken.None, timeoutSeconds: 60);
            var step = $"✓ MinIO replica restarted on {remoteHost} to load new TLS certificates";
            await EmitLog(opId, log, "success", step);
            result = result with { Steps = [.. result.Steps, step] };
        }
        await SaveLogAsync(log);
        return result;
    }

    private async Task SaveLogAsync(CertDeploymentLog log)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();

            // Attach the entity properly — it was tracked by a different scope
            var existing = await db.CertDeploymentLogs
                .FirstOrDefaultAsync(l => l.OperationId == log.OperationId);
            if (existing != null)
            {
                existing.Status = log.Status;
                existing.CompletedAt = log.CompletedAt;
                existing.ErrorMessage = log.ErrorMessage;
                existing.LogLines = log.LogLines;
            }
            else
            {
                db.CertDeploymentLogs.Add(log);
            }
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to save deployment log {OpId}", log.OperationId);
        }
    }

    /// <summary>
    /// Download a certificate bundle (cert + key + CA chain) as a structured result.
    /// </summary>
    public async Task<CertBundle?> GetCertBundleAsync(Guid certId, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();

        var cert = await db.ManagedCertificates
            .Include(c => c.IssuingCa)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == certId, ct);

        if (cert == null) return null;

        return new CertBundle(
            cert.CertificatePem,
            UnprotectKey(cert.PrivateKeyPem),
            cert.IssuingCa?.ChainPem ?? cert.IssuingCa?.CertificatePem ?? "",
            cert.CommonName,
            cert.Purpose
        );
    }

    /// <summary>
    /// Get a CA dashboard summary: total CAs, active certs, expiring soon, recently renewed.
    /// </summary>
    public async Task<CaDashboard> GetDashboardAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();

        var cas = await db.CertificateAuthorities.AsNoTracking().ToListAsync(ct);
        var certs = await db.ManagedCertificates.AsNoTracking().ToListAsync(ct);

        var now = DateTime.UtcNow;
        var activeCerts = certs.Where(c => c.Status == ManagedCertStatus.Active).ToList();
        var expiringSoon = activeCerts.Where(c => c.IsExpiringSoon()).ToList();
        var recentlyRenewed = certs
            .Where(c => c.LastRenewalAttempt != null && c.LastRenewalAttempt > now.AddDays(-7))
            .OrderByDescending(c => c.LastRenewalAttempt)
            .Take(10)
            .ToList();

        return new CaDashboard(
            TotalCAs: cas.Count,
            ActiveCAs: cas.Count(c => c.Status == CaStatus.Active),
            TotalCerts: certs.Count,
            ActiveCerts: activeCerts.Count,
            ExpiringSoon: expiringSoon.Count,
            RevokedCerts: certs.Count(c => c.Status == ManagedCertStatus.Revoked),
            ExpiringSoonList: expiringSoon.Select(c => new CertListItem(
                c.Id, c.CommonName, c.SubjectAltNames, c.Type, c.Purpose,
                c.Status, c.Fingerprint, c.SerialNumber,
                c.NotBefore, c.NotAfter, c.IssuingCaId,
                c.DeployedTo, c.DeployedAt,
                c.AutoRenewEnabled, c.RenewBeforeDays,
                c.ReplacedCertId, c.ReplacedByCertId,
                c.LastRenewalAttempt, c.LastRenewalResult,
                true)).ToList(),
            RecentRenewals: recentlyRenewed.Select(c => new CertListItem(
                c.Id, c.CommonName, c.SubjectAltNames, c.Type, c.Purpose,
                c.Status, c.Fingerprint, c.SerialNumber,
                c.NotBefore, c.NotAfter, c.IssuingCaId,
                c.DeployedTo, c.DeployedAt,
                c.AutoRenewEnabled, c.RenewBeforeDays,
                c.ReplacedCertId, c.ReplacedByCertId,
                c.LastRenewalAttempt, c.LastRenewalResult,
                c.IsExpiringSoon())).ToList()
        );
    }

    // ═══════════════════════════════════════════════════════
    // Internal / Helpers
    // ═══════════════════════════════════════════════════════

    private async Task<ManagedCertificate> IssueCertificateInternalAsync(
        IvfDbContext db, IssueCertRequest req, CancellationToken ct)
    {
        var ca = await db.CertificateAuthorities.FirstOrDefaultAsync(c => c.Id == req.CaId, ct)
            ?? throw new InvalidOperationException("CA not found");

        if (ca.Status != CaStatus.Active)
            throw new InvalidOperationException("CA is not active");

        var validityDays = req.ValidityDays > 0 ? req.ValidityDays : 365;
        var keySize = req.KeySize > 0 ? req.KeySize : 4096;
        var keyAlgorithm = req.KeyAlgorithm ?? "RSA";
        if (keyAlgorithm.Equals("RSA", StringComparison.OrdinalIgnoreCase) && keySize < 2048)
            throw new InvalidOperationException("RSA certificate key size must be at least 2048 bits");

        // Use cryptographic random serial (RFC 5280 §4.1.2.2)
        var serialHex = GenerateRandomSerialHex();

        var (keyInternal, actualAlgorithmInternal, actualKeySizeInternal) = CreateKeyPair(keyAlgorithm, keySize);
        using var _ki = keyInternal;
        var subject = new X500DistinguishedName($"CN={req.CommonName}");

        CertificateRequest certReq = keyInternal switch
        {
            RSA rsa => new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1),
            ECDsa ecdsa => new CertificateRequest(subject, ecdsa, HashAlgorithmName.SHA256),
            _ => throw new InvalidOperationException($"Unsupported key type: {keyInternal.GetType().Name}")
        };

        if (req.Type == CertType.Server)
        {
            var keyUsage = actualAlgorithmInternal == "ECDSA"
                ? X509KeyUsageFlags.DigitalSignature
                : X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment;
            certReq.CertificateExtensions.Add(new X509KeyUsageExtension(keyUsage, true));
            certReq.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
                [new Oid("1.3.6.1.5.5.7.3.1")], false));
        }
        else
        {
            certReq.CertificateExtensions.Add(new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature, true));
            certReq.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
                [new Oid("1.3.6.1.5.5.7.3.2")], false));
        }

        certReq.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(certReq.PublicKey, false));

        if (!string.IsNullOrWhiteSpace(req.SubjectAltNames))
        {
            var sanBuilder = new SubjectAlternativeNameBuilder();
            foreach (var san in req.SubjectAltNames.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (System.Net.IPAddress.TryParse(san, out var ip))
                    sanBuilder.AddIpAddress(ip);
                else
                    sanBuilder.AddDnsName(san);
            }
            certReq.CertificateExtensions.Add(sanBuilder.Build());
        }

        // Add CRL Distribution Point extension if configured
        var baseUrl = configuration.GetValue<string>("CertificateAuthority:BaseUrl");
        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            var crlUrl = $"{baseUrl.TrimEnd('/')}/api/pki/crl/{ca.Id}";
            certReq.CertificateExtensions.Add(BuildCrlDistributionPointExtension(crlUrl));
        }

        // Load CA cert + key for signing (decrypt key if encrypted)
        var caCert = X509Certificate2.CreateFromPem(ca.CertificatePem, UnprotectKey(ca.PrivateKeyPem));

        // Authority Key Identifier — links issued cert to issuing CA for chain validation
        certReq.CertificateExtensions.Add(
            X509AuthorityKeyIdentifierExtension.CreateFromCertificate(
                caCert, includeKeyIdentifier: true, includeIssuerAndSerial: false));

        var notBefore = DateTimeOffset.UtcNow;
        var notAfter = notBefore.AddDays(validityDays);

        // Clamp: issued cert cannot outlive the issuing CA
        if (notAfter > caCert.NotAfter)
            notAfter = caCert.NotAfter.AddMinutes(-1);

        var serialBytes = Convert.FromHexString(serialHex);

        using var signedCert = certReq.Create(caCert, notBefore, notAfter, serialBytes);
        var certPem = ExportCertPem(signedCert);
        var keyPem = ExportKeyPem(keyInternal);
        var fingerprint = signedCert.GetCertHashString(HashAlgorithmName.SHA256);

        var managedCert = ManagedCertificate.Create(
            commonName: req.CommonName,
            subjectAltNames: req.SubjectAltNames,
            type: req.Type,
            purpose: req.Purpose,
            certificatePem: certPem,
            privateKeyPem: ProtectKey(keyPem),
            fingerprint: fingerprint,
            serialNumber: serialHex,
            notBefore: notBefore.UtcDateTime,
            notAfter: notAfter.UtcDateTime,
            keyAlgorithm: actualAlgorithmInternal,
            keySize: actualKeySizeInternal,
            issuingCaId: ca.Id,
            renewBeforeDays: req.RenewBeforeDays > 0 ? req.RenewBeforeDays : 30
        );

        db.ManagedCertificates.Add(managedCert);
        return managedCert;
    }

    private static X500DistinguishedName BuildSubjectName(
        string cn, string? org, string? orgUnit, string? country, string? state, string? locality)
    {
        var parts = new List<string> { $"CN={cn}" };
        if (!string.IsNullOrWhiteSpace(org)) parts.Add($"O={org}");
        if (!string.IsNullOrWhiteSpace(orgUnit)) parts.Add($"OU={orgUnit}");
        if (!string.IsNullOrWhiteSpace(locality)) parts.Add($"L={locality}");
        if (!string.IsNullOrWhiteSpace(state)) parts.Add($"ST={state}");
        if (!string.IsNullOrWhiteSpace(country)) parts.Add($"C={country}");
        return new X500DistinguishedName(string.Join(", ", parts));
    }

    // ═══════════════════════════════════════════════════════
    // Deploy Log Queries
    // ═══════════════════════════════════════════════════════

    public async Task<List<DeployLogItem>> ListDeployLogsAsync(Guid? certId, int limit, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();

        var query = db.CertDeploymentLogs.AsNoTracking().AsQueryable();
        if (certId.HasValue)
            query = query.Where(l => l.CertificateId == certId.Value);

        var entities = await query
            .OrderByDescending(l => l.StartedAt)
            .Take(limit)
            .ToListAsync(ct);

        return entities.Select(l => new DeployLogItem(
            l.Id, l.OperationId, l.CertificateId,
            l.Target, l.Container, l.RemoteHost,
            l.Status, l.StartedAt, l.CompletedAt,
            l.ErrorMessage, l.LogLines
        )).ToList();
    }

    public async Task<DeployLogItem?> GetDeployLogAsync(string operationId, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();

        var l = await db.CertDeploymentLogs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.OperationId == operationId, ct);
        if (l == null) return null;

        return new DeployLogItem(
            l.Id, l.OperationId, l.CertificateId,
            l.Target, l.Container, l.RemoteHost,
            l.Status, l.StartedAt, l.CompletedAt,
            l.ErrorMessage, l.LogLines
        );
    }

    private static string ExportCertPem(X509Certificate2 cert)
    {
        var sb = new StringBuilder();
        sb.AppendLine("-----BEGIN CERTIFICATE-----");
        sb.AppendLine(Convert.ToBase64String(cert.RawData, Base64FormattingOptions.InsertLineBreaks));
        sb.AppendLine("-----END CERTIFICATE-----");
        return sb.ToString();
    }

    private static string ExportCrlPem(byte[] crlDer)
    {
        var sb = new StringBuilder();
        sb.AppendLine("-----BEGIN X509 CRL-----");
        sb.AppendLine(Convert.ToBase64String(crlDer, Base64FormattingOptions.InsertLineBreaks));
        sb.AppendLine("-----END X509 CRL-----");
        return sb.ToString();
    }

    /// <summary>
    /// Build a CRL Distribution Points extension (RFC 5280 §4.2.1.13).
    /// Encodes a single HTTP URI as a DistributionPoint.
    /// </summary>
    private static X509Extension BuildCrlDistributionPointExtension(string crlUrl)
    {
        // ASN.1: SEQUENCE { SEQUENCE { [0] { [0] { [6] uri } } } }
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence()) // CRLDistributionPoints SEQUENCE
        {
            using (writer.PushSequence()) // DistributionPoint SEQUENCE
            {
                using (writer.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 0, true))) // distributionPoint [0]
                {
                    using (writer.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 0, true))) // fullName [0]
                    {
                        // uniformResourceIdentifier [6] IA5String
                        writer.WriteCharacterString(UniversalTagNumber.IA5String,
                            crlUrl, new Asn1Tag(TagClass.ContextSpecific, 6));
                    }
                }
            }
        }

        return new X509Extension("2.5.29.31", writer.Encode(), false);
    }

    private static async Task WriteToContainerAsync(string container, string path, string content, CancellationToken ct)
    {
        // Write to temp file then docker cp into container
        var tempFile = Path.Combine(Path.GetTempPath(), $"cert_{Guid.NewGuid():N}.pem");
        try
        {
            await File.WriteAllTextAsync(tempFile, content, ct);

            // docker cp creates parent directories automatically
            var (exit, output) = await RunCommandAsync($"docker cp \"{tempFile}\" {container}:{path}", ct, timeoutSeconds: 15);
            if (exit != 0)
                throw new InvalidOperationException($"Failed to write to {container}:{path}: {output}");
        }
        finally
        {
            try { File.Delete(tempFile); } catch { }
        }
    }

    private static async Task WriteToRemoteContainerAsync(string remoteHost, string container, string path, string content, CancellationToken ct)
    {
        // Write to temp file, SCP to remote host, then docker cp into container
        var tempName = $"cert_{Guid.NewGuid():N}.pem";
        var tempFile = Path.Combine(Path.GetTempPath(), tempName);
        var remoteTmp = $"/tmp/{tempName}";
        var sshOpts = "-o StrictHostKeyChecking=accept-new -o ConnectTimeout=30 -o BatchMode=yes -o ServerAliveInterval=10 -o ServerAliveCountMax=3";
        const int maxRetries = 3;

        try
        {
            await File.WriteAllTextAsync(tempFile, content, ct);

            // SCP temp file to remote host (with retry)
            await RetryRemoteCommandAsync(async () =>
            {
                var (scpExit, scpOut) = await RunCommandAsync(
                    $"scp {sshOpts} \"{tempFile}\" root@{remoteHost}:{remoteTmp}", ct, timeoutSeconds: 90);
                if (scpExit != 0)
                    throw new InvalidOperationException($"SCP to {remoteHost} failed: {scpOut}");
            }, maxRetries, ct);

            // docker cp from remote host into container (with retry)
            await RetryRemoteCommandAsync(async () =>
            {
                var (cpExit, cpOut) = await RunCommandAsync(
                    $"ssh {sshOpts} root@{remoteHost} docker cp {remoteTmp} {container}:{path}", ct, timeoutSeconds: 90);
                if (cpExit != 0)
                    throw new InvalidOperationException($"Docker cp on {remoteHost} failed: {cpOut}");
            }, maxRetries, ct);

            // Cleanup remote temp file (best-effort, no retry)
            try
            {
                await RunCommandAsync(
                    $"ssh {sshOpts} root@{remoteHost} rm -f {remoteTmp}", ct, timeoutSeconds: 30);
            }
            catch { /* cleanup is best-effort */ }
        }
        finally
        {
            try { File.Delete(tempFile); } catch { }
        }
    }

    private static async Task RetryRemoteCommandAsync(Func<Task> action, int maxRetries, CancellationToken ct)
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                await action();
                return;
            }
            catch (Exception) when (attempt < maxRetries && !ct.IsCancellationRequested)
            {
                // Exponential backoff: 2s, 4s before retries
                await Task.Delay(TimeSpan.FromSeconds(attempt * 2), ct);
            }
        }
    }

    private static async Task<(int ExitCode, string Output)> RunCommandAsync(
        string command, CancellationToken ct, int timeoutSeconds = 30)
    {
        ProcessStartInfo psi;
        if (OperatingSystem.IsWindows())
        {
            // Run directly without cmd.exe to avoid pipe-handling issues with SSH/SCP
            var (exe, args) = ParseCommand(command);
            psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
        }
        else
        {
            psi = new ProcessStartInfo
            {
                FileName = "/bin/sh",
                Arguments = $"-c \"{command.Replace("\"", "\\\"")}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
        }

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start process");

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        var linked = timeoutCts.Token;

        try
        {
            // Use cancellation token on reads so we can abort if timeout fires
            var stdoutTask = process.StandardOutput.ReadToEndAsync(linked);
            var stderrTask = process.StandardError.ReadToEndAsync(linked);

            await process.WaitForExitAsync(linked);

            // Process exited — give a brief window for pipes to flush
            var stdout = await WaitWithTimeout(stdoutTask, 3000);
            var stderr = await WaitWithTimeout(stderrTask, 3000);

            return (process.ExitCode, string.IsNullOrWhiteSpace(stdout) ? stderr : stdout);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            throw new TimeoutException($"Command timed out after {timeoutSeconds}s: {psi.FileName} {psi.Arguments}");
        }
    }

    private static async Task<string> WaitWithTimeout(Task<string> task, int milliseconds)
    {
        if (await Task.WhenAny(task, Task.Delay(milliseconds)) == task)
            return task.Result;
        return string.Empty;
    }

    private static (string Exe, string Args) ParseCommand(string command)
    {
        // Handle quoted executable paths
        if (command.StartsWith('"'))
        {
            var endQuote = command.IndexOf('"', 1);
            if (endQuote > 0)
                return (command[1..endQuote], command[(endQuote + 1)..].TrimStart());
        }

        // Split on first space
        var spaceIdx = command.IndexOf(' ');
        return spaceIdx > 0
            ? (command[..spaceIdx], command[(spaceIdx + 1)..])
            : (command, "");
    }
}

// ═══════════════════════════════════════════════════════
// DTOs
// ═══════════════════════════════════════════════════════

public record CreateCaRequest(
    string Name,
    string CommonName,
    string? Organization,
    string? OrgUnit,
    string? Country,
    string? State,
    string? Locality,
    string? KeyAlgorithm = "RSA",
    int KeySize = 4096,
    int ValidityDays = 3650
);

public record IssueCertRequest(
    Guid CaId,
    string CommonName,
    string? SubjectAltNames,
    CertType Type,
    string Purpose,
    int ValidityDays = 365,
    int KeySize = 2048,
    int RenewBeforeDays = 30,
    string? KeyAlgorithm = "RSA"
);

public record CaListItem(
    Guid Id, string Name, string CommonName, CaType Type, CaStatus Status,
    string KeyAlgorithm, int KeySize, string Fingerprint,
    DateTime NotBefore, DateTime NotAfter, Guid? ParentCaId,
    int ActiveCertCount);

public record CertListItem(
    Guid Id, string CommonName, string? SubjectAltNames, CertType Type, string Purpose,
    ManagedCertStatus Status, string Fingerprint, string SerialNumber,
    DateTime NotBefore, DateTime NotAfter, Guid IssuingCaId,
    string? DeployedTo, DateTime? DeployedAt,
    bool AutoRenewEnabled, int RenewBeforeDays,
    Guid? ReplacedCertId, Guid? ReplacedByCertId,
    DateTime? LastRenewalAttempt, string? LastRenewalResult,
    bool IsExpiringSoon);

public record CertBundle(
    string CertificatePem, string PrivateKeyPem, string CaChainPem,
    string CommonName, string Purpose);

public record CertDeployResult(bool Success, List<string> Steps, string? OperationId = null);

public record DeployLogItem(
    Guid Id, string OperationId, Guid CertificateId,
    string Target, string Container, string? RemoteHost,
    DeployStatus Status, DateTime StartedAt, DateTime? CompletedAt,
    string? ErrorMessage, List<DeployLogLine> LogLines);

public record CertRenewalResult(
    Guid OldCertId, Guid? NewCertId, string CommonName, string Purpose,
    bool Success, string Message);

public record CertRenewalBatchResult(
    int TotalCandidates, int RenewedCount, List<CertRenewalResult> Results);

public record CaDashboard(
    int TotalCAs, int ActiveCAs, int TotalCerts, int ActiveCerts,
    int ExpiringSoon, int RevokedCerts,
    List<CertListItem> ExpiringSoonList,
    List<CertListItem> RecentRenewals);

// ═══ Intermediate CA ═════════════════════════════════════

public record CreateIntermediateCaRequest(
    Guid ParentCaId,
    string Name,
    string CommonName,
    string? Organization,
    string? OrgUnit,
    string? Country,
    string? State,
    string? Locality,
    string? KeyAlgorithm = "RSA",
    int KeySize = 4096,
    int ValidityDays = 1825,
    string[]? PermittedDomains = null
);

// ═══ CRL DTOs ════════════════════════════════════════════

public record CrlListItem(
    Guid Id, long CrlNumber, DateTime ThisUpdate, DateTime NextUpdate,
    int RevokedCount, string Fingerprint);

// ═══ OCSP DTOs ═══════════════════════════════════════════

public record OcspResponse(
    OcspCertStatus Status,
    string SerialNumber,
    DateTime? RevokedAt,
    RevocationReason? RevocationReason,
    DateTime ProducedAt);

public enum OcspCertStatus
{
    Good = 0,
    Revoked = 1,
    Unknown = 2
}

// ═══ Audit DTOs ══════════════════════════════════════════

public record CertAuditItem(
    Guid Id, Guid? CertificateId, Guid? CaId, CertAuditEventType EventType,
    string Description, string Actor, string? SourceIp,
    string? Metadata, bool Success, string? ErrorMessage, DateTime CreatedAt);

// ═══ Revocation Request ═════════════════════════════════

public record RevokeCertRequest(RevocationReason Reason = RevocationReason.Unspecified);

namespace IVF.Domain.Enums;

public enum KeyPurpose
{
    Data,       // Encrypt PHI/patient data at rest
    Session,    // Encrypt session tokens
    Api,        // Encrypt API keys for desktop clients
    Backup,     // Encrypt database backups
    MasterSalt  // Salt for password hashing
}

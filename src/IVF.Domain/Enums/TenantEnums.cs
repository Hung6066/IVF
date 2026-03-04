namespace IVF.Domain.Enums;

public enum TenantStatus
{
    PendingSetup,
    Active,
    Suspended,
    Cancelled,
    Trial
}

public enum SubscriptionPlan
{
    Trial,
    Starter,
    Professional,
    Enterprise,
    Custom
}

public enum SubscriptionStatus
{
    Active,
    PastDue,
    Cancelled,
    Expired,
    Suspended
}

public enum BillingCycle
{
    Monthly,
    Quarterly,
    Annually
}

/// <summary>
/// Strategy for data isolation between tenants.
/// SharedDatabase: Row-level isolation via global query filters (default)
/// SeparateSchema: Each tenant has its own PostgreSQL schema within the shared database
/// SeparateDatabase: Each tenant has a completely separate database
/// </summary>
public enum DataIsolationStrategy
{
    SharedDatabase,
    SeparateSchema,
    SeparateDatabase
}

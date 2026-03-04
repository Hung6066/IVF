using IVF.Domain.Common;
using IVF.Domain.Enums;

namespace IVF.Domain.Entities;

public class TenantSubscription : BaseEntity
{
    public Guid TenantId { get; private set; }
    public SubscriptionPlan Plan { get; private set; } = SubscriptionPlan.Trial;
    public SubscriptionStatus Status { get; private set; } = SubscriptionStatus.Active;
    public BillingCycle BillingCycle { get; private set; } = BillingCycle.Monthly;

    public decimal MonthlyPrice { get; private set; }
    public decimal? DiscountPercent { get; private set; }
    public string Currency { get; private set; } = "VND";

    public DateTime StartDate { get; private set; }
    public DateTime? EndDate { get; private set; }
    public DateTime? TrialEndDate { get; private set; }
    public DateTime? NextBillingDate { get; private set; }

    public bool AutoRenew { get; private set; } = true;

    // Navigation
    public Tenant Tenant { get; private set; } = null!;

    private TenantSubscription() { }

    public static TenantSubscription Create(
        Guid tenantId,
        SubscriptionPlan plan,
        BillingCycle billingCycle,
        decimal monthlyPrice,
        string currency = "VND")
    {
        var sub = new TenantSubscription
        {
            TenantId = tenantId,
            Plan = plan,
            BillingCycle = billingCycle,
            MonthlyPrice = monthlyPrice,
            Currency = currency,
            Status = SubscriptionStatus.Active,
            StartDate = DateTime.UtcNow,
            NextBillingDate = billingCycle switch
            {
                BillingCycle.Monthly => DateTime.UtcNow.AddMonths(1),
                BillingCycle.Quarterly => DateTime.UtcNow.AddMonths(3),
                BillingCycle.Annually => DateTime.UtcNow.AddYears(1),
                _ => DateTime.UtcNow.AddMonths(1)
            }
        };

        if (plan == SubscriptionPlan.Trial)
        {
            sub.TrialEndDate = DateTime.UtcNow.AddDays(30);
            sub.MonthlyPrice = 0;
        }

        return sub;
    }

    public void Renew()
    {
        NextBillingDate = BillingCycle switch
        {
            BillingCycle.Monthly => DateTime.UtcNow.AddMonths(1),
            BillingCycle.Quarterly => DateTime.UtcNow.AddMonths(3),
            BillingCycle.Annually => DateTime.UtcNow.AddYears(1),
            _ => DateTime.UtcNow.AddMonths(1)
        };
        Status = SubscriptionStatus.Active;
        SetUpdated();
    }

    public void Cancel()
    {
        Status = SubscriptionStatus.Cancelled;
        EndDate = DateTime.UtcNow;
        AutoRenew = false;
        SetUpdated();
    }

    public void Suspend()
    {
        Status = SubscriptionStatus.Suspended;
        SetUpdated();
    }

    public void UpgradePlan(SubscriptionPlan newPlan, decimal newPrice)
    {
        Plan = newPlan;
        MonthlyPrice = newPrice;
        SetUpdated();
    }

    public void SetDiscount(decimal? discountPercent)
    {
        DiscountPercent = discountPercent;
        SetUpdated();
    }

    public decimal GetEffectivePrice()
    {
        var price = MonthlyPrice;
        if (DiscountPercent.HasValue)
            price *= (1 - DiscountPercent.Value / 100m);

        return BillingCycle switch
        {
            BillingCycle.Quarterly => price * 3,
            BillingCycle.Annually => price * 12,
            _ => price
        };
    }
}

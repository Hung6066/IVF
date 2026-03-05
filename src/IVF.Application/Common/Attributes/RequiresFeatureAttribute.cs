namespace IVF.Application.Common.Attributes;

/// <summary>
/// Marks a MediatR request as requiring a specific tenant feature to be enabled.
/// The FeatureGateBehavior will check this attribute before executing the handler.
/// Can be applied multiple times for commands requiring multiple features.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class RequiresFeatureAttribute : Attribute
{
    public string FeatureCode { get; }

    public RequiresFeatureAttribute(string featureCode)
    {
        FeatureCode = featureCode;
    }
}

namespace IVF.Application.Common.Exceptions;

public class FeatureNotEnabledException : Exception
{
    public string FeatureCode { get; }

    public FeatureNotEnabledException(string featureCode)
        : base($"Tính năng '{featureCode}' chưa được kích hoạt cho tổ chức của bạn")
    {
        FeatureCode = featureCode;
    }
}

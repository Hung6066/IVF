namespace IVF.Domain.Enums;

/// <summary>
/// Types of form fields available in the form builder
/// </summary>
public enum FieldType
{
    Text = 1,
    TextArea = 2,
    Number = 3,
    Decimal = 4,
    Date = 5,
    DateTime = 6,
    Time = 7,
    Dropdown = 8,
    MultiSelect = 9,
    Radio = 10,
    Checkbox = 11,
    FileUpload = 12,
    Rating = 13,
    Section = 14,
    Label = 15,
    Tags = 16
}

/// <summary>
/// Status of a form response submission
/// </summary>
public enum ResponseStatus
{
    Draft = 1,
    Submitted = 2,
    Reviewed = 3,
    Approved = 4,
    Rejected = 5
}

/// <summary>
/// Types of reports that can be generated
/// </summary>
public enum ReportType
{
    Table = 1,
    BarChart = 2,
    LineChart = 3,
    PieChart = 4,
    Summary = 5
}

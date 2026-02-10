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
    Tags = 16,
    PageBreak = 17,
    Address = 18,
    Hidden = 19,
    Slider = 20,
    Calculated = 21,
    RichText = 22,
    Signature = 23,
    Lookup = 24,
    Repeater = 25
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

/// <summary>
/// How linked data should be presented to the user in the target form
/// </summary>
public enum DataFlowType
{
    /// <summary>Auto-populate field with latest value (editable)</summary>
    AutoFill = 1,
    /// <summary>Show as suggestion, user must confirm</summary>
    Suggest = 2,
    /// <summary>Read-only reference display, not editable</summary>
    Reference = 3,
    /// <summary>Copy value into new response on submit</summary>
    Copy = 4
}

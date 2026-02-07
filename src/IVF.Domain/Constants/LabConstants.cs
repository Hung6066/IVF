namespace IVF.Domain.Constants;

/// <summary>
/// Constants for schedule types used in Lab operations
/// </summary>
public static class ScheduleTypes
{
    public const string Retrieval = "retrieval";
    public const string Transfer = "transfer";
    public const string Report = "report";
}

/// <summary>
/// Constants for schedule statuses
/// </summary>
public static class ScheduleStatuses
{
    public const string Pending = "pending";
    public const string Done = "done";
}

/// <summary>
/// Constants for appointment notes
/// </summary>
public static class AppointmentNotes
{
    public const string EmbryoReport = "Báo phôi";
}

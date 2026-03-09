namespace IVF.Domain.Enums;

/// <summary>
/// Status of a signed document amendment request
/// </summary>
public enum AmendmentStatus
{
    /// <summary>Yêu cầu chỉnh sửa đang chờ duyệt</summary>
    Pending = 1,

    /// <summary>Đã phê duyệt, dữ liệu đã được cập nhật</summary>
    Approved = 2,

    /// <summary>Yêu cầu bị từ chối</summary>
    Rejected = 3
}

/// <summary>
/// Type of field change in an amendment
/// </summary>
public enum FieldChangeType
{
    /// <summary>Giá trị được sửa đổi</summary>
    Modified = 1,

    /// <summary>Giá trị mới được thêm</summary>
    Added = 2,

    /// <summary>Giá trị bị xóa</summary>
    Removed = 3
}

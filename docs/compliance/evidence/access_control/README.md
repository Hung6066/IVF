# Access Control Evidence

Bằng chứng kiểm soát truy cập — SOC 2 CC6.1, ISO 27001 A.9, HIPAA §164.312(a).

## Evidence cần thu thập

### Hàng quý

- [ ] **User Access Review** — Danh sách tất cả user đang active + role
- [ ] **Privileged Access Review** — Danh sách Admin/elevated users
- [ ] **Terminated Users** — Xác nhận revoke access cho nhân viên nghỉ việc
- [ ] **MFA Enrollment Report** — Tỷ lệ đăng ký MFA

### Hàng năm

- [ ] **Role Matrix** — Ma trận phân quyền RBAC theo vai trò
- [ ] **Access Policy Review** — Xác nhận review + phê duyệt chính sách

## Nguồn dữ liệu (API)

```
GET /api/users?isActive=true&pageSize=1000         → Danh sách user active
GET /api/users/roles                                 → Danh sách vai trò
GET /api/enterprise/sessions/active                  → Phiên đang hoạt động
GET /api/enterprise/groups                           → Nhóm người dùng + permissions
GET /api/advanced-security/mfa/enrollment-stats      → Thống kê MFA
GET /api/audit-logs?action=Login&period=quarter      → Lịch sử đăng nhập
```

## Mẫu file

| File                                | Mô tả                                                           |
| ----------------------------------- | --------------------------------------------------------------- |
| `YYYY-MM-DD_user-list.csv`          | Export danh sách user (Id, Username, Role, IsActive, LastLogin) |
| `YYYY-MM-DD_mfa-report.csv`         | Trạng thái MFA từng user                                        |
| `YYYY-MM-DD_privileged-users.csv`   | Danh sách Admin users                                           |
| `YYYY-MM-DD_terminated-review.pdf`  | Biên bản review user đã nghỉ                                    |
| `YYYY-QN_access-review-signoff.pdf` | Biên bản phê duyệt quarterly review                             |

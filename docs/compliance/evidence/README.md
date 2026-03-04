# Audit Evidence Repository

Thư mục lưu trữ bằng chứng kiểm toán cho các framework: SOC 2, ISO 27001, HIPAA, GDPR, HITRUST.

## Cấu trúc

| Thư mục              | Mô tả                                     | Tần suất cập nhật |
| -------------------- | ----------------------------------------- | ----------------- |
| `access_control/`    | Danh sách user, cấu hình role, MFA logs   | Hàng quý          |
| `incident_response/` | Ticket sự cố, thời gian phản hồi          | Mỗi sự cố         |
| `training/`          | Bản ghi hoàn thành đào tạo, điểm kiểm tra | Hàng quý          |
| `change_management/` | Git history, deployment records           | Liên tục          |
| `encryption/`        | TLS configs, cipher suites                | Hàng năm          |
| `backup/`            | Backup logs, restore test results         | Hàng tháng        |
| `vendor/`            | BAA, đánh giá rủi ro nhà cung cấp         | Hàng năm          |
| `policy_versions/`   | Lịch sử phiên bản chính sách (via Git)    | Mỗi thay đổi      |

## Quy ước đặt tên file

```
YYYY-MM-DD_<loại-evidence>_<mô-tả-ngắn>.<ext>
```

Ví dụ:

- `2026-03-04_user-list_all-active-users.csv`
- `2026-03-04_mfa-report_enrollment-status.pdf`
- `2026-Q1_training-completion_hipaa.xlsx`

## Lưu ý

- **KHÔNG** lưu dữ liệu bệnh nhân thật (PHI/PII) vào thư mục này
- Export dữ liệu phải được **anonymize/pseudonymize** trước khi lưu
- Sử dụng `.gitignore` cho file nhạy cảm, chỉ lưu SHA256 hash
- Mỗi đợt audit tạo subfolder với format: `YYYY-MM_<tên-audit>/`

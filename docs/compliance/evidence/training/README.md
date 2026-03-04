# Training Evidence

Bằng chứng đào tạo tuân thủ — SOC 2 CC1.4, ISO 27001 A.7.2.2, HIPAA §164.308(a)(5).

## Evidence cần thu thập

### Hàng quý

- [ ] **Completion Records** — Danh sách nhân viên đã hoàn thành đào tạo
- [ ] **Test Scores** — Điểm kiểm tra từng nhân viên
- [ ] **Overdue Report** — Danh sách nhân viên chưa hoàn thành đúng hạn

### Hàng năm

- [ ] **Training Plan** — Kế hoạch đào tạo năm
- [ ] **Training Content Review** — Xác nhận nội dung đào tạo được cập nhật
- [ ] **Coverage Report** — Tỷ lệ hoàn thành theo loại đào tạo

## Nguồn dữ liệu (API)

```
GET /api/compliance/trainings?completed=true          → Đào tạo đã hoàn thành
GET /api/compliance/trainings?overdue=true             → Đào tạo quá hạn
GET /api/compliance/trainings?type=HIPAA               → Đào tạo HIPAA
GET /api/compliance/trainings?type=GDPR                → Đào tạo GDPR
GET /api/compliance/trainings?type=Security+Awareness  → Đào tạo Security
```

## Loại đào tạo bắt buộc

| Loại                     | Framework              | Tần suất |
| ------------------------ | ---------------------- | -------- |
| HIPAA Privacy & Security | HIPAA                  | Hàng năm |
| GDPR Data Protection     | GDPR                   | Hàng năm |
| Security Awareness       | SOC 2, ISO 27001       | Hàng năm |
| Incident Response        | SOC 2, ISO 27001       | Hàng năm |
| Data Handling            | HIPAA, GDPR            | Hàng năm |
| AI Ethics                | NIST AI RMF, ISO 42001 | Hàng năm |

## Mẫu file

| File                              | Mô tả                                                |
| --------------------------------- | ---------------------------------------------------- |
| `YYYY-QN_training-completion.csv` | Export hoàn thành (UserId, Type, Score, CompletedAt) |
| `YYYY-QN_overdue-report.csv`      | Danh sách quá hạn                                    |
| `YYYY_training-plan.pdf`          | Kế hoạch đào tạo năm                                 |
| `YYYY-QN_training-signoff.pdf`    | Biên bản phê duyệt kết quả đào tạo                   |

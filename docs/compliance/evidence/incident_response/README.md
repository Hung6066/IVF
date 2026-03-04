# Incident Response Evidence

Bằng chứng phản hồi sự cố — SOC 2 CC7.3-CC7.5, ISO 27001 A.16, HIPAA §164.308(a)(6).

## Evidence cần thu thập

### Mỗi sự cố

- [ ] **Incident Ticket** — Ticket đầy đủ: detection, classification, response, resolution
- [ ] **Timeline** — Dòng thời gian chi tiết từ phát hiện đến khắc phục
- [ ] **Root Cause Analysis** — Phân tích nguyên nhân gốc
- [ ] **Notification Records** — Bằng chứng thông báo (nếu có breach)

### Hàng quý

- [ ] **Incident Summary Report** — Tổng hợp sự cố trong quý
- [ ] **Response Time Metrics** — Thống kê MTTD, MTTR
- [ ] **Tabletop Exercise** — Kết quả diễn tập phản hồi sự cố

### Hàng năm

- [ ] **IR Plan Review** — Xác nhận review + cập nhật IR plan
- [ ] **Lessons Learned** — Tổng hợp bài học kinh nghiệm

## Nguồn dữ liệu (API)

```
GET /api/enterprise/security-incidents                → Danh sách sự cố
GET /api/enterprise/security-incidents/{id}           → Chi tiết sự cố
GET /api/enterprise/security-incidents/{id}/timeline  → Timeline sự cố
GET /api/enterprise/incident-response-rules           → Quy tắc phản hồi tự động
GET /api/audit-logs?action=SecurityIncident           → Audit logs sự cố
```

## Mẫu file

| File                                 | Mô tả                             |
| ------------------------------------ | --------------------------------- |
| `YYYY-MM-DD_incident-{id}.pdf`       | Báo cáo chi tiết sự cố            |
| `YYYY-QN_incident-summary.pdf`       | Tổng hợp sự cố theo quý           |
| `YYYY-QN_response-metrics.csv`       | Metrics MTTD/MTTR                 |
| `YYYY-MM-DD_tabletop-exercise.pdf`   | Kết quả diễn tập                  |
| `YYYY-MM-DD_breach-notification.pdf` | Bản sao thông báo breach (nếu có) |

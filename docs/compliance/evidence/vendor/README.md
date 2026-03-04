# Vendor Evidence

Bằng chứng quản lý nhà cung cấp — SOC 2 CC9.2, ISO 27001 A.15, HIPAA §164.308(b), GDPR Art. 28.

## Evidence cần thu thập

### Mỗi nhà cung cấp (onboarding + hàng năm)

- [ ] **Business Associate Agreement (BAA)** — Hợp đồng BAA ký kết
- [ ] **Risk Assessment** — Đánh giá rủi ro nhà cung cấp
- [ ] **Due Diligence** — Kiểm tra SOC 2/ISO 27001 của vendor
- [ ] **Data Processing Agreement (DPA)** — Hợp đồng xử lý dữ liệu (GDPR)
- [ ] **SCC** — Standard Contractual Clauses (nếu chuyển dữ liệu ngoài EU)

### Hàng năm

- [ ] **Vendor Inventory** — Cập nhật danh sách nhà cung cấp
- [ ] **Risk Re-assessment** — Đánh giá lại rủi ro
- [ ] **Compliance Certificate Review** — Xác nhận vendor vẫn tuân thủ

## Danh sách nhà cung cấp hiện tại

| Vendor                   | Service            | Data Access      | Risk Level | BAA               |
| ------------------------ | ------------------ | ---------------- | ---------- | ----------------- |
| PostgreSQL (self-hosted) | Database           | PHI/PII          | High       | N/A (self-hosted) |
| MinIO (self-hosted)      | Object Storage     | PHI documents    | High       | N/A (self-hosted) |
| Redis (self-hosted)      | Cache              | Session data     | Medium     | N/A (self-hosted) |
| EJBCA (self-hosted)      | PKI/Certificates   | None             | Low        | N/A (self-hosted) |
| SignServer (self-hosted) | Digital Signing    | PDF documents    | Medium     | N/A (self-hosted) |
| DigitalPersona           | Biometrics SDK     | Fingerprint data | High       | Required          |
| Docker Hub               | Container Registry | None             | Low        | ToS               |

## Nguồn dữ liệu (API)

```
GET /api/compliance/assets?type=SaaS          → Asset inventory (SaaS/vendors)
GET /api/compliance/assets?type=ThirdParty     → Third-party integrations
```

## Mẫu file

| File                                   | Mô tả                                                               |
| -------------------------------------- | ------------------------------------------------------------------- |
| `YYYY_vendor-inventory.csv`            | Danh sách vendor (Name, Service, DataAccess, RiskLevel, BAA_Status) |
| `vendor-name_baa_YYYY.pdf`             | Bản scan BAA đã ký                                                  |
| `vendor-name_dpa_YYYY.pdf`             | Bản scan DPA đã ký                                                  |
| `vendor-name_risk-assessment_YYYY.pdf` | Đánh giá rủi ro vendor                                              |
| `vendor-name_soc2-report_YYYY.pdf`     | SOC 2 report của vendor                                             |

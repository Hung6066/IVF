# Policy Versions Evidence

Lịch sử phiên bản chính sách — SOC 2 CC5.3, ISO 27001 A.5.1.2, HIPAA §164.316(b)(2)(iii).

## Evidence cần thu thập

### Mỗi thay đổi chính sách

- [ ] **Policy Diff** — Thay đổi so với phiên bản trước (Git diff)
- [ ] **Approval Record** — Bằng chứng phê duyệt bởi management
- [ ] **Communication Record** — Bằng chứng thông báo thay đổi cho nhân viên
- [ ] **Acknowledgment** — Xác nhận nhân viên đã đọc + hiểu

### Hàng năm

- [ ] **Policy Review** — Xác nhận tất cả chính sách đã được review
- [ ] **Policy Inventory** — Danh sách chính sách + version + last review date

## Chính sách hiện có

| Chính sách                  | File                             | Framework              |
| --------------------------- | -------------------------------- | ---------------------- |
| Information Security Policy | `information_security_policy.md` | SOC 2, ISO 27001       |
| Privacy Notice              | `privacy_notice.md`              | GDPR Art. 13-14        |
| Breach Notification SOP     | `breach_notification_sop.md`     | GDPR Art. 33-34, HIPAA |
| DPO Charter                 | `dpo_charter.md`                 | GDPR Art. 37-39        |
| AI Governance Charter       | `ai_governance_charter.md`       | NIST AI RMF, ISO 42001 |
| BCP/DRP                     | `bcp_drp.md`                     | SOC 2, ISO 27001       |
| Pseudonymization Procedures | `pseudonymization_procedures.md` | GDPR Art. 25, 32       |
| Vendor Risk Assessment      | `vendor_risk_assessment.md`      | SOC 2 CC9.2, HIPAA     |

## Thu thập tự động (Git)

```bash
# Lịch sử thay đổi tất cả policy files
git log --oneline --follow docs/compliance/*.md > YYYY_policy-history.txt

# Diff giữa 2 phiên bản
git diff v1.0..v2.0 -- docs/compliance/information_security_policy.md > policy_diff.txt

# Danh sách policy + last modified
git log -1 --format="%ai %s" -- docs/compliance/*.md
```

## Mẫu file

| File                                  | Mô tả                                                         |
| ------------------------------------- | ------------------------------------------------------------- |
| `YYYY_policy-inventory.csv`           | Danh sách (Policy, Version, LastReview, NextReview, Approver) |
| `YYYY_policy-review-signoff.pdf`      | Biên bản phê duyệt annual review                              |
| `YYYY-MM-DD_policy-change_{name}.pdf` | Approval record cho thay đổi cụ thể                           |
| `YYYY_policy-acknowledgments.csv`     | Danh sách nhân viên đã acknowledge                            |

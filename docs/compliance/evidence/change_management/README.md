# Change Management Evidence

Bằng chứng quản lý thay đổi — SOC 2 CC8.1, ISO 27001 A.12.1.2, HIPAA §164.312(e).

## Evidence cần thu thập

### Liên tục (mỗi release)

- [ ] **Git Commit History** — Log commits cho mỗi release
- [ ] **Pull Request Records** — PRs với code review approvals
- [ ] **Deployment Records** — CI/CD pipeline logs, deployment timestamps
- [ ] **Rollback Plan** — Kế hoạch rollback cho mỗi deployment

### Hàng quý

- [ ] **Change Summary** — Tổng hợp thay đổi trong quý
- [ ] **Emergency Changes** — Danh sách thay đổi khẩn cấp + justification

### Hàng năm

- [ ] **Change Management Policy Review** — Xác nhận review chính sách
- [ ] **SDLC Documentation** — Tài liệu quy trình phát triển

## Thu thập tự động

```bash
# Git commit log cho quý
git log --since="2026-01-01" --until="2026-03-31" --oneline > YYYY-QN_git-log.txt

# Thống kê contributors
git shortlog -sn --since="2026-01-01" --until="2026-03-31" > YYYY-QN_contributors.txt

# Tags/releases
git tag -l "v*" --sort=-creatordate > YYYY_releases.txt
```

## Nguồn dữ liệu

```
GitHub PR History     → https://github.com/Hung6066/IVF/pulls?q=is:merged
GitHub Actions Logs   → https://github.com/Hung6066/IVF/actions
Docker Image History  → docker history ivf-api:latest
EF Core Migrations    → src/IVF.Infrastructure/Migrations/
```

## Mẫu file

| File                         | Mô tả                                                |
| ---------------------------- | ---------------------------------------------------- |
| `YYYY-QN_git-log.txt`        | Git commit history                                   |
| `YYYY-QN_deployment-log.csv` | Deployment records (date, version, deployer, status) |
| `YYYY-QN_pr-summary.csv`     | PR list (number, title, reviewer, merged_at)         |
| `YYYY-QN_change-summary.pdf` | Tổng hợp thay đổi quý                                |

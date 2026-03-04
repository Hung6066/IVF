# Backup Evidence

Bằng chứng sao lưu & khôi phục — SOC 2 A1.2, ISO 27001 A.12.3, HIPAA §164.308(a)(7)(ii)(A).

## Evidence cần thu thập

### Hàng tháng

- [ ] **Backup Logs** — Logs xác nhận backup chạy thành công
- [ ] **Backup Integrity** — SHA256 hash verification cho backup files
- [ ] **Backup Size Trend** — Thống kê dung lượng backup theo thời gian

### Hàng quý

- [ ] **Restore Test** — Kết quả test khôi phục từ backup
- [ ] **Recovery Time** — Đo thời gian khôi phục thực tế (RTO)
- [ ] **Data Integrity Check** — Verify dữ liệu sau restore khớp production

### Hàng năm

- [ ] **BCP/DRP Review** — Xác nhận review Business Continuity Plan
- [ ] **Full DR Drill** — Kết quả diễn tập khôi phục thảm họa toàn diện

## Nguồn dữ liệu

```bash
# Backup files hiện có
ls -la backups/*.sha256

# Kiểm tra backup mới nhất
cat backups/ivf_db_$(date +%Y%m%d)*.sha256

# Test restore
pg_restore -d ivf_db_test backups/ivf_db_latest.sql.gz
```

## Backup Schedule hiện tại

| Loại          | Tần suất        | Retention | Vị trí               |
| ------------- | --------------- | --------- | -------------------- |
| SQL Dump      | Hàng ngày 02:00 | 30 ngày   | `backups/` + offsite |
| Base Backup   | Hàng tuần       | 90 ngày   | `backups/` + offsite |
| MinIO Objects | Hàng ngày       | 90 ngày   | S3 cross-region      |
| WAL Archives  | Liên tục        | 7 ngày    | Local + offsite      |

## Mẫu file

| File                          | Mô tả                                                 |
| ----------------------------- | ----------------------------------------------------- |
| `YYYY-MM_backup-log.csv`      | Backup records (date, type, size, sha256, status)     |
| `YYYY-QN_restore-test.pdf`    | Kết quả restore test (steps, time, data verification) |
| `YYYY-QN_rto-measurement.pdf` | Đo lường RTO/RPO thực tế                              |
| `YYYY_dr-drill-report.pdf`    | Báo cáo diễn tập DR                                   |

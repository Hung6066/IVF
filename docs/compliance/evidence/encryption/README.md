# Encryption Evidence

Bằng chứng mã hóa — SOC 2 CC6.1/CC6.7, ISO 27001 A.10, HIPAA §164.312(a)(2)(iv)/§164.312(e)(2)(ii).

## Evidence cần thu thập

### Hàng năm

- [ ] **TLS Configuration** — Certificate details, cipher suites, protocol versions
- [ ] **Encryption at Rest** — PostgreSQL encryption, MinIO encryption settings
- [ ] **Key Management** — Key rotation records, key access logs
- [ ] **Certificate Inventory** — Danh sách tất cả certificates + expiry dates

### Khi thay đổi

- [ ] **Certificate Renewal** — Bằng chứng renew certificate
- [ ] **Cipher Suite Update** — Justification cho thay đổi cipher

## Kiểm tra tự động

```bash
# Kiểm tra TLS configuration
openssl s_client -connect localhost:443 -tls1_2 < /dev/null 2>&1 | openssl x509 -noout -text

# Kiểm tra cipher suites
nmap --script ssl-enum-ciphers -p 443 localhost

# Kiểm tra certificate expiry
openssl s_client -connect localhost:443 < /dev/null 2>&1 | openssl x509 -noout -dates

# PostgreSQL encryption
psql -c "SHOW ssl;" -c "SHOW ssl_cipher;"
```

## Cấu hình hiện tại

| Component   | Encryption    | Chi tiết                            |
| ----------- | ------------- | ----------------------------------- |
| API (HTTPS) | TLS 1.2+      | Kestrel / reverse proxy             |
| PostgreSQL  | SSL + AES-256 | Connection string `SslMode=Require` |
| MinIO       | TLS + SSE-S3  | Server-side encryption              |
| Redis       | TLS optional  | StackExchange.Redis                 |
| SignServer  | mTLS          | Client certificate authentication   |
| JWT Tokens  | HMAC-SHA256   | 256-bit key                         |
| Passwords   | BCrypt        | Cost factor 12                      |

## Mẫu file

| File                             | Mô tả                                          |
| -------------------------------- | ---------------------------------------------- |
| `YYYY_tls-config.txt`            | Output kiểm tra TLS                            |
| `YYYY_cipher-suites.txt`         | Danh sách cipher suites                        |
| `YYYY_certificate-inventory.csv` | Certificates (CN, Issuer, NotBefore, NotAfter) |
| `YYYY_encryption-at-rest.pdf`    | Báo cáo encryption at rest                     |

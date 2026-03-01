# 🔐 Hướng dẫn sử dụng Vault Manager

> Tài liệu hướng dẫn sử dụng giao diện quản lý Vault với 15 tab chức năng — bao gồm quản lý secret, mã hóa dữ liệu, API keys, kiểm soát truy cập, Zero Trust, và nhiều tính năng bảo mật khác.

**Yêu cầu**: Tài khoản **Admin** để truy cập Vault Manager.

**Truy cập**: Menu **Admin → Vault Manager** hoặc trực tiếp tại `http://localhost:4200/admin/vault-manager`

---

## Mục lục

1. [Tổng quan giao diện](#1-tổng-quan-giao-diện)
2. [🔑 Secrets — Quản lý bí mật](#2--secrets--quản-lý-bí-mật)
3. [🗄️ Database — Quản lý kết nối CSDL](#3-️-database--quản-lý-kết-nối-csdl)
4. [🗝️ API Keys — Quản lý khóa API](#4-️-api-keys--quản-lý-khóa-api)
5. [📋 Policies — Chính sách truy cập](#5--policies--chính-sách-truy-cập)
6. [👤 User Policies — Gán quyền người dùng](#6--user-policies--gán-quyền-người-dùng)
7. [⏱️ Leases — Quản lý thời hạn](#7-️-leases--quản-lý-thời-hạn)
8. [🔄 Rotation — Xoay khóa](#8--rotation--xoay-khóa)
9. [⚡ Dynamic — Credential tạm thời](#9--dynamic--credential-tạm-thời)
10. [🎟️ Tokens — Token xác thực](#10-️-tokens--token-xác-thực)
11. [⚙️ Settings — Cấu hình Azure Key Vault](#11-️-settings--cấu-hình-azure-key-vault)
12. [📥 Import — Nhập hàng loạt](#12--import--nhập-hàng-loạt)
13. [📜 History — Lịch sử thao tác](#13--history--lịch-sử-thao-tác)
14. [🔒 Encryption — Mã hóa dữ liệu](#14--encryption--mã-hóa-dữ-liệu)
15. [🔐 Phân quyền — Kiểm soát truy cập theo trường](#15--phân-quyền--kiểm-soát-truy-cập-theo-trường)
16. [🛡️ Zero Trust — Chính sách Zero Trust](#16-️-zero-trust--chính-sách-zero-trust)

---

## 1. Tổng quan giao diện

Vault Manager gồm **15 tab** hiển thị trên thanh tab sticky ở đầu trang, tự động xuống dòng khi màn hình nhỏ.

### Khởi tạo Vault

Khi lần đầu truy cập, nếu Vault chưa được khởi tạo, hệ thống sẽ hiển thị form:

| Trường              | Mô tả                                                   |
| ------------------- | ------------------------------------------------------- |
| **Master Password** | Mật khẩu master dùng để mã hóa KEK (tối thiểu 12 ký tự) |
| **User ID**         | ID người dùng khởi tạo                                  |

Nhấn **Khởi tạo Vault** để tạo hệ thống mã hóa. Sau khi khởi tạo, các tab sẽ xuất hiện.

### Thanh trạng thái

- **🟢 Healthy** — Vault đang hoạt động bình thường
- **🔴 Unhealthy** — Kết nối Azure KV mất hoặc chưa cấu hình
- **Active Keys: N** — Số lượng API key đang hoạt động

---

## 2. 🔑 Secrets — Quản lý bí mật

Tab quản lý secret với cấu trúc **thư mục phân cấp** (giống filesystem).

### Điều hướng

- **Breadcrumb** phía trên bảng: `Home > folder1 > folder2` — click để chuyển đến bất kỳ cấp nào
- Click vào **thư mục** trong bảng để vào bên trong
- Click **Home** để về gốc

### Bảng dữ liệu

| Cột      | Mô tả                            |
| -------- | -------------------------------- |
| Icon     | 📂 thư mục hoặc 🔑 secret        |
| Tên      | Tên secret/thư mục (click để mở) |
| Loại     | Folder hoặc Secret               |
| Thao tác | 👁 Xem, 🗑 Xóa                   |

### Tạo Secret mới

Nhấn **➕ New Secret**, dialog hiện ra:

| Trường          | Mô tả                                                           |
| --------------- | --------------------------------------------------------------- |
| **Path**        | Đường dẫn secret (ví dụ: `smtp/password`, `database/prod/conn`) |
| **Data (JSON)** | Dữ liệu dạng JSON `{"key": "value"}`                            |
| **TTL**         | Thời gian sống (giây), tùy chọn                                 |

### 9 Template có sẵn

Chọn template để tự động điền form với cấu trúc phù hợp:

| Template           | Mô tả                  | Ví dụ path          |
| ------------------ | ---------------------- | ------------------- |
| 🔑 **Credentials** | Username/password      | `services/my-app`   |
| ⚙️ **Config**      | Cấu hình ứng dụng      | `config/my-app`     |
| 🎟️ **Token**       | Access/refresh token   | `tokens/my-service` |
| 🔐 **SSH Key**     | SSH private/public key | `ssh/server-name`   |
| 📜 **Certificate** | Chứng thư số PEM       | `certs/domain`      |
| 📦 **Env Vars**    | Biến môi trường        | `envs/staging`      |
| 🗄️ **Database**    | Connection string      | `database/prod`     |
| 📧 **SMTP**        | Cấu hình email         | `smtp/main`         |
| ☁️ **MinIO/S3**    | Object storage         | `minio/main`        |

### Xem chi tiết Secret

Click **👁 Xem** → dialog hiển thị từng trường key-value:

- Mỗi trường có nút **👁** để ẩn/hiện giá trị
- Nút **📋** để copy giá trị vào clipboard
- Hiển thị: Path, Version, Created At, Updated At

---

## 3. 🗄️ Database — Quản lý kết nối CSDL

Lưu trữ thông tin kết nối database một cách an toàn (mã hóa AES-256-GCM).

### Bảng dữ liệu

| Cột      | Mô tả                                          |
| -------- | ---------------------------------------------- |
| Tên      | Tên định danh (ví dụ: `production`, `staging`) |
| Host     | Hostname/IP                                    |
| Database | Tên database                                   |
| Port     | Cổng kết nối                                   |
| Thao tác | 👁 Xem, 🗑 Xóa                                 |

### Thêm Database

Nhấn **➕ Thêm Database**, dialog hiện ra:

| Trường   | Mặc định | Mô tả                                         |
| -------- | -------- | --------------------------------------------- |
| Tên      | —        | ID định danh duy nhất                         |
| Host     | —        | Hostname (ví dụ: `localhost`, `db.clinic.vn`) |
| Port     | 5432     | Cổng kết nối                                  |
| Database | —        | Tên database                                  |
| Username | —        | Tài khoản DB                                  |
| Password | —        | Mật khẩu DB (mã hóa khi lưu)                  |

> **Lưu ý**: Toàn bộ credential được mã hóa AES-256-GCM trước khi lưu vào PostgreSQL. Path lưu trữ: `database/{tên}`.

---

## 4. 🗝️ API Keys — Quản lý khóa API

Tạo, theo dõi và xoay (rotate) API key cho các dịch vụ tích hợp.

### Cảnh báo hết hạn

Phía trên bảng hiển thị ⚠️ danh sách **key sắp hết hạn** trong 30 ngày tới kèm ngày hết hạn.

### Bảng dữ liệu

| Cột           | Mô tả                   |
| ------------- | ----------------------- |
| Service       | Tên dịch vụ sử dụng key |
| Key Name      | Tên key                 |
| Trạng thái    | Active / Inactive       |
| Version       | Phiên bản hiện tại      |
| Hết hạn       | Ngày hết hạn            |
| Xoay lần cuối | Lần rotate gần nhất     |
| Thao tác      | 🔄 Xoay                 |

### Tạo API Key

Nhấn **➕ Tạo Key mới**:

| Trường       | Mô tả                                     |
| ------------ | ----------------------------------------- |
| Key Name     | Tên key (ví dụ: `lab-integration-key`)    |
| Service Name | Dịch vụ sử dụng (ví dụ: `Lab System`)     |
| Key Prefix   | Tiền tố key (ví dụ: `ivf_`)               |
| Key Hash     | Hash của key                              |
| Environment  | Development / Staging / Production        |
| Chu kỳ xoay  | Số ngày giữa các lần rotate (mặc định 90) |

### Xoay Key (Rotate)

Nhấn **🔄 Xoay** trên bảng → dialog hiện ra:

- **New Key Hash** — hash mới cho key
- **Rotated By** — người thực hiện

Hệ thống tự tăng version và cập nhật ngày xoay.

---

## 5. 📋 Policies — Chính sách truy cập

Định nghĩa chính sách truy cập vault dựa trên **path pattern** và **capabilities**.

### Bảng dữ liệu

| Cột          | Mô tả                                                    |
| ------------ | -------------------------------------------------------- |
| Tên          | Tên policy                                               |
| Path Pattern | Pattern đường dẫn (ví dụ: `secret/data/*`, `database/*`) |
| Capabilities | Danh sách quyền (badge)                                  |
| Mô tả        | Ghi chú                                                  |
| Thao tác     | 🗑 Xóa                                                   |

### Tạo Policy

Nhấn **➕ Tạo Policy**:

| Trường       | Mô tả                                                                           |
| ------------ | ------------------------------------------------------------------------------- |
| Tên Policy   | Tên chính sách                                                                  |
| Mô tả        | Ghi chú mô tả                                                                   |
| Path Pattern | Pattern áp dụng (ví dụ: `secret/*`)                                             |
| Capabilities | Chọn checkbox: **read**, **list**, **create**, **update**, **delete**, **sudo** |

### Giải thích Capabilities

| Capability | Mô tả                                    |
| ---------- | ---------------------------------------- |
| `read`     | Đọc giá trị secret                       |
| `list`     | Liệt kê danh sách secret                 |
| `create`   | Tạo secret mới                           |
| `update`   | Cập nhật secret                          |
| `delete`   | Xóa secret                               |
| `sudo`     | Quyền quản trị cao nhất (bao gồm tất cả) |

---

## 6. 👤 User Policies — Gán quyền người dùng

Gán policy cho người dùng cụ thể.

### Bảng dữ liệu

| Cột      | Mô tả                     |
| -------- | ------------------------- |
| User     | ID hoặc tên người dùng    |
| Policy   | Tên policy đã gán (badge) |
| Gán lúc  | Thời điểm gán             |
| Thao tác | 🗑 Gỡ                     |

### Gán Policy

Nhấn **➕ Gán Policy**:

| Trường  | Mô tả                                             |
| ------- | ------------------------------------------------- |
| User ID | ID người dùng                                     |
| Policy  | Chọn từ dropdown các policy đã tạo (tab Policies) |

### Gỡ Policy

Click **🗑 Gỡ** để hủy gán policy khỏi người dùng.

---

## 7. ⏱️ Leases — Quản lý thời hạn

Quản lý **thời hạn (lease)** cho secret — tự động hết hạn sau TTL.

### Bảng dữ liệu

| Cột        | Mô tả                       |
| ---------- | --------------------------- |
| Secret     | Đường dẫn secret            |
| TTL        | Thời gian sống (giây)       |
| Renewable  | ✅ có thể gia hạn / — không |
| Hết hạn    | Thời điểm hết hạn           |
| Trạng thái | Active / Revoked            |
| Thao tác   | 🔄 Gia hạn, 🚫 Thu hồi      |

### Tạo Lease

Nhấn **➕ Tạo Lease**:

| Trường           | Mô tả                                         |
| ---------------- | --------------------------------------------- |
| Secret Path      | Đường dẫn secret (ví dụ: `database/postgres`) |
| TTL (giây)       | Thời gian sống (tối thiểu 60 giây)            |
| Cho phép gia hạn | Checkbox — có được renew không                |

### Thao tác

- **🔄 Gia hạn**: Gia hạn thời gian cho lease (chỉ khi `renewable = true`)
- **🚫 Thu hồi**: Lập tức hết hạn lease — secret không còn truy cập được

---

## 8. 🔄 Rotation — Xoay khóa

Xem lịch xoay key và thực hiện xoay ngay lập tức.

### Bảng dữ liệu

| Cột           | Mô tả                |
| ------------- | -------------------- |
| Service       | Dịch vụ              |
| Key           | Tên key              |
| Trạng thái    | Active / Inactive    |
| Xoay lần cuối | Ngày rotate lần cuối |
| Hết hạn       | Ngày hết hạn         |
| Thao tác      | 🔄 Xoay ngay         |

Nhấn **🔄 Xoay ngay** để mở dialog rotate (giống tab API Keys).

---

## 9. ⚡ Dynamic — Credential tạm thời

Tạo **credential tạm thời** cho database — tự động hết hạn sau TTL. Phù hợp cho CI/CD, testing, hoặc cấp quyền ngắn hạn.

### Bảng dữ liệu

| Cột      | Mô tả                                     |
| -------- | ----------------------------------------- |
| Lease ID | Mã lease                                  |
| Backend  | Loại DB: postgres / mysql / mssql / redis |
| Username | Tên user được tạo                         |
| Host     | Host:Port                                 |
| Database | Tên database                              |
| Hết hạn  | Thời điểm hết hạn                         |
| Thao tác | 🚫 Revoke                                 |

### Tạo Dynamic Credential

Nhấn **➕ Thêm Config**:

| Trường         | Mặc định  | Mô tả                            |
| -------------- | --------- | -------------------------------- |
| Backend        | postgres  | Loại database                    |
| DB Host        | localhost | Hostname                         |
| DB Port        | 5432      | Cổng                             |
| Database Name  | —         | Tên database                     |
| Username       | —         | Username mới sẽ tạo              |
| Admin Username | —         | Tài khoản admin DB (để tạo user) |
| Admin Password | —         | Mật khẩu admin DB                |
| TTL (giây)     | —         | Thời gian sống                   |

### Cách hoạt động

1. Hệ thống dùng admin credential để kết nối DB
2. Tạo user mới với quyền hạn giới hạn
3. Tạo mật khẩu ngẫu nhiên, mã hóa và lưu vào vault
4. Khi hết TTL → tự động revoke và xóa user

---

## 10. 🎟️ Tokens — Token xác thực

Tạo và quản lý **token xác thực** cho vault — dùng cho service-to-service hoặc automation.

### Bảng dữ liệu

| Cột          | Mô tả                                                      |
| ------------ | ---------------------------------------------------------- |
| Display Name | Tên hiển thị                                               |
| Accessor     | Mã accessor (dùng để tham chiếu token mà không lộ giá trị) |
| Type         | service (dài hạn) / batch (ngắn hạn)                       |
| Policies     | Danh sách policy                                           |
| Uses         | Số lần đã dùng / tối đa (ví dụ: 5/10)                      |
| Hết hạn      | Thời điểm hết hạn                                          |
| Trạng thái   | Valid / Revoked / Expired                                  |
| Thao tác     | 🚫 Thu hồi                                                 |

### Tạo Token

Nhấn **➕ Tạo Token**:

| Trường       | Mô tả                                                                |
| ------------ | -------------------------------------------------------------------- |
| Display Name | Tên hiển thị                                                         |
| Policies     | Danh sách policy, phân cách dấu phẩy (ví dụ: `read-only, db-access`) |
| Token Type   | `service` (dài hạn) hoặc `batch` (ngắn hạn, xóa khi hết)             |
| TTL (giây)   | Thời gian sống                                                       |
| Max Uses     | Số lần sử dụng tối đa (0 = không giới hạn)                           |

### ⚠️ Lưu ý quan trọng

Sau khi tạo, token sẽ **hiển thị MỘT LẦN DUY NHẤT**:

- **Token** (Base64) — sao chép ngay!
- **Accessor** — dùng để tham chiếu

> Sau khi đóng dialog, KHÔNG thể xem lại giá trị token.

---

## 11. ⚙️ Settings — Cấu hình Azure Key Vault

Cấu hình kết nối Azure Key Vault để bọc (wrap) KEK bằng RSA key cloud.

### Cấu hình kết nối

| Trường        | Mô tả                                                    |
| ------------- | -------------------------------------------------------- |
| Vault URL     | URL Azure KV (ví dụ: `https://myvault.vault.azure.net/`) |
| Key Name      | Tên RSA key trong Azure KV                               |
| Tenant ID     | Azure AD Tenant ID                                       |
| Client ID     | App Registration Client ID                               |
| Client Secret | App Registration Secret                                  |

### Các bước thực hiện

1. **Điền thông tin** Azure Key Vault
2. Nhấn **🔌 Test Connection** — kiểm tra kết nối
   - ✅ Xanh = kết nối thành công
   - ❌ Đỏ = kết nối thất bại, kiểm tra lại thông tin
3. Nhấn **💾 Lưu cấu hình** để lưu

### Kích hoạt Auto-Unseal

Sau khi lưu cấu hình, bật auto-unseal:

| Trường          | Mô tả                                             |
| --------------- | ------------------------------------------------- |
| Master Password | Mật khẩu master vault (tối thiểu 12 ký tự)        |
| Azure Key Name  | Tên key dùng để wrap (mặc định: `ivf-master-key`) |

Nhấn **🔑 Kích Hoạt Auto-Unseal** → hệ thống wrap master password bằng Azure RSA key → vault tự động mở khi khởi động lại.

### Hướng dẫn tạo Azure KV

Hệ thống hiển thị hướng dẫn 5 bước:

1. Tạo Azure Key Vault tại Azure Portal
2. Tạo RSA key (2048-bit hoặc 4096-bit)
3. Tạo App Registration trong Azure AD
4. Cấp quyền: Key Vault → Access policies → **Wrap Key**, **Unwrap Key**
5. Nhập thông tin vào form và lưu

---

## 12. 📥 Import — Nhập hàng loạt

Nhập nhiều secret cùng lúc từ file JSON hoặc `.env`.

### Tùy chọn

| Trường | Mô tả                                                                |
| ------ | -------------------------------------------------------------------- |
| Format | `JSON` hoặc `.env`                                                   |
| Prefix | Tiền tố đường dẫn (ví dụ: `staging/` → secret lưu tại `staging/key`) |

### Cách nhập

**Cách 1: Upload file**

- Kéo thả hoặc chọn file `.json`, `.env`, `.txt`

**Cách 2: Paste nội dung**

- Dán trực tiếp vào textarea

### Định dạng JSON

```json
{
  "database-password": "super-secret",
  "api-key": "sk-12345",
  "smtp-host": "smtp.gmail.com"
}
```

### Định dạng .env

```env
# Database
DATABASE_PASSWORD=super-secret
API_KEY=sk-12345
SMTP_HOST=smtp.gmail.com
```

### Kết quả

Sau khi import, hệ thống hiển thị:

- ✅ **X thành công** — số secret đã import
- ❌ **Y lỗi** — số secret bị lỗi (nếu có)

---

## 13. 📜 History — Lịch sử thao tác

Xem **audit log** toàn bộ thao tác trên vault — ai làm gì, lúc nào, từ IP nào.

### Bảng dữ liệu

| Cột       | Mô tả                      |
| --------- | -------------------------- |
| Thời gian | Ngày giờ thao tác          |
| Action    | Loại hành động (badge màu) |
| Resource  | Tài nguyên bị tác động     |
| User      | ID người thực hiện         |
| IP        | Địa chỉ IP                 |
| Chi tiết  | Dữ liệu JSON chi tiết      |

### Phân trang

- **20 bản ghi** mỗi trang
- Điều hướng: ◀ Trước | Trang X/Y | Sau ▶
- Nhấn **🔄 Làm mới** để tải lại

---

## 14. 🔒 Encryption — Mã hóa dữ liệu

Tab phức tạp nhất với **5 section** quản lý toàn bộ hệ thống mã hóa.

### Section 1: Cấu hình mã hóa theo bảng

Danh sách các bảng DB đã cấu hình auto-encryption.

| Cột           | Mô tả                                                |
| ------------- | ---------------------------------------------------- |
| Bảng          | Tên bảng + badge "Mặc định" (nếu là config mặc định) |
| Trường mã hóa | Danh sách trường (badge) + `+N` nếu nhiều            |
| DEK Purpose   | data / session / api / backup (badge màu)            |
| Trạng thái    | Toggle switch bật/tắt                                |
| Thao tác      | ✏️ Sửa, 🗑️ Xóa                                       |

#### Thêm bảng mã hóa

Nhấn **➕ Thêm bảng**:

| Trường           | Mô tả                                         |
| ---------------- | --------------------------------------------- |
| Table name       | Dropdown chọn bảng từ DB schema thực tế       |
| Encrypted fields | Checkbox grid — tick các trường cần mã hóa    |
| DEK Purpose      | `data` (mặc định), `session`, `api`, `backup` |
| Description      | Mô tả                                         |

> Table name và danh sách trường được **load trực tiếp từ DB** (PostgreSQL `information_schema`), không cần nhập tay.

#### Bật/tắt mã hóa

Click **toggle switch** trên bảng → bật/tắt mã hóa cho bảng đó mà không xóa config.

### Section 2: Auto-Unseal Status

Hiển thị trạng thái auto-unseal:

- ✅ **Đã cấu hình** — kèm Key Vault URL, Key Name, Algorithm
- ⚠️ **Chưa cấu hình** — cần vào Settings để cấu hình

Các nút:

- **💾 Cấu hình Auto-Unseal**: Nhập Master Password + Azure Key Name
- **🔓 Auto-Unseal Now**: Thực hiện unseal ngay (nếu vault đang locked)

### Section 3: DEK Keys (5 loại)

Hiển thị 5 card mô tả các Data Encryption Key:

| Key            | Mô tả                          |
| -------------- | ------------------------------ |
| 🔑 Data DEK    | Mã hóa dữ liệu bệnh nhân       |
| 🔐 Session DEK | Mã hóa phiên đăng nhập         |
| 🗝️ API DEK     | Mã hóa API keys                |
| 💾 Backup DEK  | Mã hóa bản sao lưu             |
| 🧂 Master Salt | Salt cho PBKDF2 key derivation |

### Section 4: Key Wrap / Unwrap (Envelope Encryption)

Hai panel song song để **wrap** và **unwrap** key:

**Panel trái — Wrap Key:**

| Trường    | Mô tả                  |
| --------- | ---------------------- |
| Key Name  | Tên key trong Azure KV |
| Plaintext | Dữ liệu cần wrap       |

Kết quả: Algorithm, Wrapped Key (Base64), IV (Base64) — kèm nút Copy.

Nhấn **➡️ Dùng cho Unwrap** để tự động điền kết quả vào panel Unwrap.

**Panel phải — Unwrap Key:**

| Trường               | Mô tả          |
| -------------------- | -------------- |
| Key Name             | Tên key        |
| Wrapped Key (Base64) | Key đã wrap    |
| IV (Base64)          | IV từ khi wrap |

Kết quả: Plaintext gốc — kèm nút Copy.

### Section 5: Encrypt / Decrypt Data (AES-256-GCM)

Hai panel song song để **mã hóa** và **giải mã** dữ liệu:

**Panel trái — Encrypt:**

| Trường      | Mô tả                                      |
| ----------- | ------------------------------------------ |
| Key Purpose | Data / Session / Api / Backup / MasterSalt |
| Plaintext   | Dữ liệu cần mã hóa                         |

Kết quả: Algorithm, Purpose, Ciphertext (Base64), IV (Base64) — kèm nút Copy.

Nhấn **➡️ Dùng cho Decrypt** để tự động điền.

**Panel phải — Decrypt:**

| Trường              | Mô tả                     |
| ------------------- | ------------------------- |
| Key Purpose         | Phải khớp với lúc encrypt |
| Ciphertext (Base64) | Dữ liệu đã mã hóa         |
| IV (Base64)         | IV từ khi encrypt         |

Kết quả: Plaintext gốc — kèm nút Copy.

### Sơ đồ phân cấp khóa (Key Hierarchy)

```
☁️ Azure RSA Key (RSA-OAEP-256)
  └── 🔑 KEK (Key Encryption Key)
        ├── 🔐 DEK Data     (AES-256-GCM)
        ├── 🔐 DEK Session  (AES-256-GCM)
        ├── 🔐 DEK API      (AES-256-GCM)
        └── 🔐 DEK Backup   (AES-256-GCM)
              └── 📄 Encrypted Data
```

---

## 15. 🔐 Phân quyền — Kiểm soát truy cập theo trường

Kiểm soát **từng trường dữ liệu** hiển thị cho từng **vai trò** — hỗ trợ mask, partial, hoặc ẩn hoàn toàn.

### Sub-tabs

- **📋 Policies** — Quản lý chính sách truy cập theo trường
- **📜 Audit Log** — Lịch sử thay đổi phân quyền

### Bảng Policies (gom nhóm theo bảng)

Policies được **nhóm theo tên bảng** với header có thể mở/đóng (▼/▶):

| Cột          | Mô tả                                |
| ------------ | ------------------------------------ |
| Trường       | Tên trường DB                        |
| Vai trò      | Tên role (Doctor, Nurse, LabTech...) |
| Mức truy cập | Badge màu theo mức                   |
| Mask Pattern | Pattern mask (nếu masked)            |
| Thao tác     | ✏️ Sửa, 🗑️ Xóa                       |

### 4 mức truy cập

| Mức       | Badge             | Mô tả                   | Ví dụ (gốc: "Nguyễn Văn An") |
| --------- | ----------------- | ----------------------- | ---------------------------- |
| `full`    | 🟢 Toàn quyền     | Xem đầy đủ              | "Nguyễn Văn An"              |
| `partial` | 🟡 Một phần       | Hiện N ký tự đầu + mask | "Nguyễ**\*\*\*\***"          |
| `masked`  | 🟠 Che dấu        | Mask toàn bộ            | "**\*\*\*\***"               |
| `none`    | 🔴 Không truy cập | Ẩn hoàn toàn            | _(không hiển thị)_           |

### Tạo Policy phân quyền

Nhấn **➕ Thêm policy**:

| Trường         | Mô tả                                                                                    |
| -------------- | ---------------------------------------------------------------------------------------- |
| Table name     | Dropdown chọn bảng (load từ DB schema)                                                   |
| Fields         | **Multi-select checkbox** — chọn nhiều trường cùng lúc                                   |
| Role           | Dropdown: Admin, Doctor, Nurse, LabTech, Embryologist, Receptionist, Cashier, Pharmacist |
| Access Level   | full / partial / masked / none                                                           |
| Mask Pattern   | _(hiện khi chọn masked)_ Pattern che (ví dụ: `********`, `***HIDDEN***`)                 |
| Partial Length | _(hiện khi chọn partial)_ Số ký tự đầu hiển thị (1–50)                                   |
| Description    | Mô tả                                                                                    |

> Khi chọn nhiều trường, hệ thống tạo **batch** — mỗi trường 1 policy riêng.

### Lưu ý quan trọng

- ⚠️ Role **Admin** luôn có quyền truy cập đầy đủ vào tất cả dữ liệu
- Policies áp dụng nguyên tắc **Least Privilege** (ít quyền nhất)
- Mọi thay đổi được ghi nhận trong **Audit Log**

---

## 16. 🛡️ Zero Trust — Chính sách Zero Trust

Quản lý và kiểm tra chính sách bảo mật Zero Trust — đánh giá 6 điểm trước khi cho phép truy cập.

### Section 1: Security Dashboard

5 card trạng thái tổng quan:

| Card             | Mô tả                 |
| ---------------- | --------------------- |
| Security Score   | Điểm bảo mật tổng thể |
| Vault Status     | Trạng thái vault      |
| Trusted Devices  | Số thiết bị tin cậy   |
| Recent Alerts    | Số cảnh báo gần đây   |
| Blocked Attempts | Số lần bị chặn        |

Bên dưới: bảng **Recent Security Events** (5 sự kiện mới nhất).

### Section 2: Thống kê ZT

3 card thống kê:

- **Active Policies** — Số policy đang hoạt động
- **VPN/Tor Blocked** — Số policy chặn VPN/Tor
- **Trusted Device Required** — Số policy yêu cầu thiết bị tin cậy

### Section 3: Kiểm tra truy cập (Access Check)

Test nhanh một action có được phép hay không:

| Trường | Mô tả                                                        |
| ------ | ------------------------------------------------------------ |
| Action | Tên action cần kiểm tra (ví dụ: `ViewPatient`, `ExportData`) |

Nhấn **🧪 Kiểm tra** → kết quả:

- ✅ **GRANTED** — cho phép truy cập
- ❌ **DENIED** — từ chối, kèm:
  - Auth Level yêu cầu
  - Device Risk Level
  - Danh sách **Failed Checks** (badge đỏ)

### Section 4: Bảng Zero Trust Policies

| Cột            | Mô tả                                                                               |
| -------------- | ----------------------------------------------------------------------------------- |
| Action         | Tên action                                                                          |
| Auth Level     | Mức xác thực tối thiểu (None / Session / Password / MFA / FreshSession / Biometric) |
| Max Risk       | Mức rủi ro tối đa (Low / Medium / High / Critical)                                  |
| Trusted Device | ✅ yêu cầu / — không                                                                |
| Fresh Session  | ✅ yêu cầu / — không                                                                |
| Block VPN/Tor  | ✅ chặn / — không                                                                   |
| Block Anomaly  | ✅ chặn / — không                                                                   |
| Geo Fence      | ✅ bật / — tắt                                                                      |
| Active         | Active / Off                                                                        |
| Thao tác       | ✏️ Sửa                                                                              |

### Sửa Policy ZT

Nhấn **✏️ Sửa**:

| Trường                     | Mô tả                                                  |
| -------------------------- | ------------------------------------------------------ |
| Required Auth Level        | Mức xác thực tối thiểu                                 |
| Max Allowed Risk           | Mức rủi ro tối đa cho phép                             |
| Require Trusted Device     | Checkbox — bắt buộc thiết bị tin cậy                   |
| Require Fresh Session      | Checkbox — phiên phải mới                              |
| Block VPN/Tor              | Checkbox — chặn truy cập qua VPN/Tor                   |
| Block Anomaly              | Checkbox — chặn khi phát hiện bất thường               |
| Require Geo Fence          | Checkbox — giới hạn địa lý                             |
| Allowed Countries          | Danh sách quốc gia cho phép                            |
| Allow Break Glass Override | Checkbox — cho phép vượt qua trong trường hợp khẩn cấp |
| Updated By                 | Người thực hiện cập nhật                               |

### 6 điểm kiểm tra Zero Trust

| #   | Kiểm tra           | Mô tả                                           |
| --- | ------------------ | ----------------------------------------------- |
| 1   | **Auth Level**     | Mức xác thực đủ cao (Session / MFA / Biometric) |
| 2   | **Device Risk**    | Rủi ro thiết bị dưới ngưỡng cho phép            |
| 3   | **Trusted Device** | Thiết bị đã đăng ký và được tin cậy             |
| 4   | **Fresh Session**  | Phiên chưa quá hạn                              |
| 5   | **Geo-fence**      | Vị trí địa lý trong phạm vi cho phép            |
| 6   | **VPN/Tor**        | Không truy cập qua VPN/Tor ẩn danh              |

---

## Phụ lục: Bảng cấu hình mã hóa mặc định

Hệ thống tự tạo 5 config mặc định khi khởi tạo:

| Bảng              | Trường mã hóa                                                      | DEK Purpose |
| ----------------- | ------------------------------------------------------------------ | ----------- |
| `medical_records` | diagnosis, symptoms, treatment_plan, notes, medications, allergies | data        |
| `patients`        | medical_history, allergies, emergency_contact, insurance_info      | data        |
| `prescriptions`   | medications, dosage_instructions, notes                            | data        |
| `lab_results`     | results, notes, interpretation                                     | data        |
| `user_sessions`   | session_token                                                      | session     |

---

## Phụ lục: Phím tắt & Mẹo sử dụng

- **Copy nhanh**: Mọi giá trị nhạy cảm đều có nút 📋 copy-to-clipboard
- **Chuyển tab**: Click tab trên thanh sticky — hỗ trợ dùng Tab/Enter từ bàn phím
- **Tải lại dữ liệu**: Mỗi tab tự load khi chuyển đến, hoặc nhấn 🔄 Làm mới
- **Template SECRET**: Khi tạo secret, chọn template phù hợp để tiết kiệm thời gian
- **Import hàng loạt**: Dùng tab Import thay vì tạo từng secret một
- **Kiểm tra trước khi triển khai**: Dùng Encrypt/Decrypt panels để test trước khi tích hợp vào code

---

_Tài liệu cập nhật: Tháng 2/2026_
_Áp dụng cho: IVF Information System — Angular 21 + .NET 10_
_Xem thêm: [vault_integration_guide.md](vault_integration_guide.md) — hướng dẫn tích hợp cho developer_

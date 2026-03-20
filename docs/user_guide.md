# Hướng Dẫn Sử Dụng — Hệ Thống Quản Lý IVF (IVFMD)

> **Phiên bản:** 1.0 — Ngày cập nhật: 20/03/2026
> **Đối tượng:** Bác sĩ, Điều dưỡng, Kỹ thuật viên Lab, Tiếp đón, Thu ngân, Dược sĩ, Phôi học, Quản trị viên

---

## Mục Lục

1. [Đăng nhập & Giao diện chung](#1-đăng-nhập--giao-diện-chung)
2. [Dashboard — Tổng quan](#2-dashboard--tổng-quan)
3. [17 Luồng Quy Trình Chính](#3-17-luồng-quy-trình-chính)
   - [Luồng 1: Khám lần đầu (First Visit)](#luồng-1-khám-lần-đầu)
   - [Luồng 2: Tư vấn sau xét nghiệm](#luồng-2-tư-vấn-sau-xét-nghiệm)
   - [Luồng 3: Kích thích buồng trứng (KTBT)](#luồng-3-kích-thích-buồng-trứng)
   - [Luồng 4: Tiêm rụng trứng (Trigger)](#luồng-4-tiêm-rụng-trứng)
   - [Luồng 5: Chọc hút trứng (OPU)](#luồng-5-chọc-hút-trứng)
   - [Luồng 6: Bơm tinh trùng (IUI)](#luồng-6-bơm-tinh-trùng-iui)
   - [Luồng 7: Báo phôi — Chuyển phôi — Trữ phôi](#luồng-7-báo-phôi--chuyển-phôi--trữ-phôi)
   - [Luồng 8: Chuyển phôi đông lạnh (FET)](#luồng-8-chuyển-phôi-đông-lạnh-fet)
   - [Luồng 9: Thử thai (Beta HCG)](#luồng-9-thử-thai-beta-hcg)
   - [Luồng 10: Thai 7 tuần (Prenatal)](#luồng-10-thai-7-tuần)
   - [Luồng 11: Cho trứng (Egg Donor)](#luồng-11-cho-trứng)
   - [Luồng 12: Ngân hàng tinh trùng (NHTT)](#luồng-12-ngân-hàng-tinh-trùng)
   - [Luồng 13: Nam khoa (Andrology)](#luồng-13-nam-khoa)
   - [Luồng 14: Kho & Vật tư (Inventory)](#luồng-14-kho--vật-tư)
   - [Luồng 15: Tài chính (Billing)](#luồng-15-tài-chính)
   - [Luồng 16: Phòng Lab (Laboratory)](#luồng-16-phòng-lab)
   - [Luồng 17: Nhà thuốc (Pharmacy)](#luồng-17-nhà-thuốc)
4. [Thanh điều hướng (Sidebar)](#4-thanh-điều-hướng)
5. [Quản trị hệ thống](#5-quản-trị-hệ-thống)

---

## 1. Đăng nhập & Giao diện chung

### Đăng nhập

1. Truy cập hệ thống tại địa chỉ được cấp (ví dụ: `https://ivf.tên-trung-tâm.vn`)
2. Nhập **Tên đăng nhập** và **Mật khẩu**
3. Nhấn **Đăng nhập**

> **Lưu ý:** Phiên đăng nhập tự động hết hạn sau 60 phút không hoạt động. Hệ thống sẽ tự động refresh token trong nền nếu bạn vẫn đang sử dụng.

### Giao diện chính

Sau khi đăng nhập, giao diện gồm 3 vùng:

| Vùng                | Mô tả                                                     |
| ------------------- | --------------------------------------------------------- |
| **Sidebar (trái)**  | Menu điều hướng chính — hiển thị theo vai trò người dùng  |
| **Header (trên)**   | Tiêu đề trang hiện tại + 🔔 Thông báo + Avatar người dùng |
| **Nội dung (giữa)** | Trang làm việc — thay đổi theo menu được chọn             |

**Đăng xuất:** Nhấn nút 🚪 **Đăng xuất** ở dưới cùng sidebar.

---

## 2. Dashboard — Tổng quan

**Đường dẫn:** Sidebar → 📊 **Dashboard**

Dashboard hiển thị tổng quan hoạt động:

- Số bệnh nhân hiện có
- Số chu kỳ đang điều trị
- Lịch hẹn trong ngày
- Hàng đợi hiện tại theo phòng ban
- Thống kê nhanh theo phương pháp (ICSI, IUI, FET, IVM)

---

## 3. 17 Luồng Quy Trình Chính

Phần này mô tả chi tiết 17 luồng công việc chính mà hệ thống hỗ trợ, theo đúng quy trình lâm sàng tại trung tâm IVF.

---

### Luồng 1: Khám lần đầu

> **Mã:** IVFMD.01 — **Vai trò:** Tiếp đón → Thu ngân → Bác sĩ

**Mục đích:** Tiếp nhận bệnh nhân mới lần đầu đến trung tâm.

#### Các bước thực hiện trên hệ thống:

**Bước 1 — Tiếp đón tạo bệnh nhân**

1. Sidebar → 🏥 **Tiếp đón** (`/reception`)
2. Nhấn **Thêm bệnh nhân mới** → chuyển đến `/patients/new`
3. Điền thông tin:
   - Mã bệnh nhân (tự sinh hoặc nhập)
   - Họ tên, Ngày sinh, Giới tính
   - CMND/CCCD, Số điện thoại, Địa chỉ
   - Loại BN: **Hiếm muộn** (mặc định)
4. Nhấn **Lưu** → Hệ thống tạo hồ sơ bệnh nhân

**Bước 2 — Tạo cặp đôi**

1. Sidebar → 💑 **Cặp đôi** (`/couples`)
2. Nhấn **Tạo cặp đôi mới** → `/couples/new`
3. Chọn BN vợ và BN chồng từ danh sách
4. Nhập ngày kết hôn, số năm hiếm muộn
5. Nhấn **Lưu**

**Bước 3 — Cấp số thứ tự (STT)**

1. Sidebar → 🎫 **Hàng đợi** (`/queue/all`)
2. Chọn phòng ban **Tiếp đón (RECEPTION)**
3. Nhấn **Cấp số** → chọn bệnh nhân → hệ thống tạo phiếu hàng đợi

**Bước 4 — Tạo phiếu tư vấn**

1. Sidebar → 🗣️ **Tư vấn** (`/consultation`)
2. Nhấn **Tạo tư vấn mới**
3. Chọn bệnh nhân, Bác sĩ, Loại tư vấn: **Khám lần đầu (FirstVisit)**
4. Nhập lý do khám, ghi chú
5. Nhấn **Lưu**

**Bước 5 — Ký đồng thuận**

1. Sidebar → 📋 **Đồng thuận** (`/consent`)
2. Nhấn **Tạo mới** → `/consent/create`
3. Chọn bệnh nhân, Loại: **IVF_GENERAL**
4. Tiêu đề: "Đồng ý điều trị IVF"
5. BN ký → **Lưu**

**Bước 6 — Đặt lịch tái khám**

1. Sidebar → 📅 **Lịch hẹn** (`/appointments`)
2. Nhấn **Tạo lịch hẹn**
3. Chọn BN, Loại: **Tư vấn (Consultation)**, Ngày hẹn (sau 2-7 ngày)
4. Ghi chú: "Tái khám sau xét nghiệm"
5. Nhấn **Lưu**

---

### Luồng 2: Tư vấn sau xét nghiệm

> **Mã:** IVFMD.02 — **Vai trò:** Bác sĩ

**Mục đích:** Xem kết quả xét nghiệm, quyết định phương pháp điều trị, tạo chu kỳ.

#### Các bước:

**Bước 1 — Kiểm tra kết quả xét nghiệm**

1. Sidebar → 🧫 **Phòng Lab** (`/lab`)
2. Tìm BN → xem kết quả XN Hormone (FSH, AMH, E2) và Tinh dịch đồ

**Bước 2 — Tư vấn kết quả**

1. Sidebar → 🗣️ **Tư vấn** → Tạo phiếu tư vấn
2. Loại tư vấn: **FollowUp**
3. Nhập kết luận: VD "FSH=6.5, AMH=3.2, AFC=12 → Chỉ định ICSI"

**Bước 3 — Tạo chu kỳ điều trị**

1. Sidebar → 💑 **Cặp đôi** → Chọn cặp đôi → Nhấn **Tạo chu kỳ mới**
2. Chọn phương pháp: **ICSI**, **IUI**, **IVM**, hoặc **QHTN**
3. Nhấn **Lưu** → Hệ thống tạo chu kỳ ở pha **Tư vấn (Consultation)**

**Bước 4 — Chỉ định xét nghiệm bổ sung (nếu cần)**

1. Sidebar → 🧫 **Phòng Lab** (`/lab`)
2. Nhấn **Tạo phiếu XN** → chọn BN, chọn loại XN
3. Nhấn **Lưu**

---

### Luồng 3: Kích thích buồng trứng

> **Mã:** IVFMD.10/20 — **Vai trò:** Bác sĩ + Điều dưỡng

**Mục đích:** Theo dõi quá trình KTBT (Kích thích buồng trứng), siêu âm nang trứng, kê đơn thuốc KTBT.

#### Các bước:

**Bước 1 — Mở theo dõi KTBT**

1. Mở chi tiết chu kỳ (từ hồ sơ BN hoặc danh sách cặp đôi)
2. Trong trang chi tiết chu kỳ (`/cycles/:id`), nhấn **KTBT** → chuyển đến `/stimulation/:cycleId`
3. Nhập thông tin:
   - Ngày kinh cuối, Ngày bắt đầu KTBT
   - Ngày thứ (Day), Số nang ≥12mm, Số nang ≥14mm
   - Độ dày NMTC (mm)

**Bước 2 — Kê thuốc KTBT**

1. Trong trang KTBT, nhập danh sách thuốc:
   - Tên thuốc (VD: Gonal F), Số ngày dùng, Liều lượng (VD: 150IU)
2. Nhấn **Lưu**

**Bước 3 — Siêu âm nang trứng**

1. Sidebar → 🔬 **Siêu âm** (`/ultrasound`)
2. Nhấn **Tạo mới** → `/cycles/:cycleId/ultrasound/new`
3. Chọn loại: **Theo dõi nang trứng (FollicleMonitoring)**
4. Nhập kết quả đo → Nhấn **Lưu**

**Bước 4 — Kê đơn thuốc KTBT**

1. Sidebar → 💊 **Nhà thuốc** (`/pharmacy`)
2. Nhấn **Tạo đơn thuốc** → chọn BN + Chu kỳ
3. Nhập danh sách thuốc KTBT
4. Nhấn **Lưu**

> **Thao tác lặp lại:** Siêu âm + Kê thuốc được thực hiện nhiều lần trong 8-12 ngày KTBT. Mỗi lần thêm bản ghi mới.

---

### Luồng 4: Tiêm rụng trứng

> **Mã:** IVFMD.11/21 — **Vai trò:** Bác sĩ + Điều dưỡng tiêm

**Mục đích:** Khi nang trứng đạt kích thước, bác sĩ chỉ định tiêm rụng trứng (trigger shot).

#### Các bước:

**Bước 1 — Cập nhật Trigger trong KTBT**

1. Mở `/stimulation/:cycleId`
2. Cập nhật:
   - Loại thuốc trigger (VD: Ovitrelle)
   - Ngày tiêm HCG, Giờ tiêm
3. Nhấn **Lưu** → Chu kỳ chuyển sang pha **Trigger Shot**

**Bước 2 — Ghi nhận tiêm trigger**

1. Sidebar → 💉 **Tiêm** (`/injection`)
2. Nhấn **Ghi nhận trigger** → `/injection/trigger-shot`
3. Chọn BN, nhập thuốc: **Ovitrelle 250mcg**, liều: **250mcg**, đường dùng: **SC**
4. Đánh dấu: **Là mũi trigger ✓**
5. Nhấn **Lưu**

**Bước 3 — Đặt lịch chọc hút**

1. Sidebar → 📅 **Lịch hẹn**
2. Tạo lịch hẹn loại **Chọc hút trứng (EggRetrieval)**
3. Ngày hẹn: 36 giờ sau trigger
4. Nhấn **Lưu**

---

### Luồng 5: Chọc hút trứng

> **Mã:** IVFMD.30 — **Vai trò:** Bác sĩ thủ thuật + Điều dưỡng + KTV Lab

**Mục đích:** Thực hiện thủ thuật chọc hút trứng (OPU - Oocyte Pickup).

#### Các bước:

**Bước 1 — Ký đồng thuận OPU**

1. Sidebar → 📋 **Đồng thuận** → **Tạo mới**
2. Loại: **OPU_CONSENT**, Tiêu đề: "Đồng ý thủ thuật chọc hút trứng"
3. BN ký → **Lưu**

**Bước 2 — Tạo thủ thuật OPU**

1. Sidebar → 🔧 **Thủ thuật** (`/procedure`)
2. Nhấn **Tạo mới** → `/procedure/create`
   Hoặc từ chi tiết chu kỳ → Nhấn **OPU** → `/procedure/opu/:cycleId`
3. Nhập:
   - Loại: **OPU**
   - Tên: "Chọc hút trứng"
   - Gây mê: **Gây mê tĩnh mạch** (nếu có)
4. Nhấn **Lưu** → Chu kỳ chuyển sang pha **Egg Retrieval**

**Bước 3 — Giao mẫu cho Lab**

1. Sidebar → 🧫 **Phòng Lab** → **Bàn giao mẫu** (`/lab/sample-handover`)
2. Chọn chu kỳ, ghi nhận số trứng chọc hút được
3. Nhấn **Lưu**

---

### Luồng 6: Bơm tinh trùng (IUI)

> **Mã:** IVFMD.40 — **Vai trò:** Bác sĩ + KTV Nam khoa

**Mục đích:** Thực hiện bơm tinh trùng vào buồng tử cung (IUI - Intrauterine Insemination).

#### Các bước:

**Bước 1 — Xét nghiệm tinh dịch đồ**

1. Sidebar → 🔬 **Nam khoa** (`/andrology`)
2. Nhấn **Tạo XN mới** → `/andrology/analysis/new`
3. Chọn BN (chồng), Loại: **Trước rửa (PreWash)**
4. Nhập kết quả → Nhấn **Lưu**

**Bước 2 — Rửa tinh trùng**

1. Sidebar → 🔬 **Nam khoa** → **Rửa tinh trùng** → `/andrology/wash/new`
2. Chọn BN, Phương pháp: **Swim-up**
3. Nhấn **Lưu**

**Bước 3 — Thực hiện thủ thuật IUI**

1. Sidebar → 🔧 **Thủ thuật** → `/procedure/iui/:cycleId`
2. Loại: **IUI**, Tên: "Bơm tinh trùng vào buồng tử cung"
3. Nhấn **Lưu**

---

### Luồng 7: Báo phôi — Chuyển phôi — Trữ phôi

> **Mã:** IVFMD.80 — **Vai trò:** KTV Phôi học (Embryologist)

**Mục đích:** Nuôi cấy phôi sau OPU, theo dõi phát triển phôi, chuyển phôi tươi hoặc trữ phôi.

#### 7a. Báo phôi (Embryo Culture)

**Bước 1 — Cập nhật phôi hàng ngày**

1. Mở chi tiết chu kỳ (`/cycles/:id`)
2. Mục **Phôi** → Nhấn **Thêm phôi** hoặc cập nhật phôi sẵn có
3. Nhập:
   - Số phôi, Ngày thụ tinh
   - Đánh giá (Grade): VD "4C-G1"
   - Ngày phôi: D1, D2, D3, D4, D5, D6
   - Trạng thái: Đang phát triển / Ngừng phát triển
4. Nhấn **Lưu**

> **Lặp lại mỗi ngày** từ D1 đến D5/D6: cập nhật grade và trạng thái từng phôi.

#### 7b. Chuyển phôi tươi (Fresh ET)

**Bước 1 — Tạo thủ thuật chuyển phôi**

1. Sidebar → 🔧 **Thủ thuật** → **Tạo mới**
2. Loại: **ET**, Tên: "Chuyển phôi ngày 5"
3. Chọn phôi để chuyển → Cập nhật trạng thái phôi: **Transferred**
4. Nhấn **Lưu** → Chu kỳ chuyển sang pha **Embryo Transfer**

#### 7c. Trữ phôi (Embryo Freezing)

**Bước 1 — Đánh dấu phôi đông lạnh**

1. Trong chi tiết chu kỳ, cập nhật trạng thái phôi dư: **Frozen**

**Bước 2 — Tạo hợp đồng trữ phôi**

1. Trong chi tiết chu kỳ → Mục **Hợp đồng trữ phôi**
2. Nhập:
   - Số hợp đồng (VD: HD-FREEZE-001)
   - Ngày hợp đồng, Ngày bắt đầu lưu trữ
   - Thời hạn (tháng): 12
   - Phí hàng năm: 2,000,000 VNĐ
3. Nhấn **Lưu**

---

### Luồng 8: Chuyển phôi đông lạnh (FET)

> **Mã:** IVFMD.50/51 — **Vai trò:** Bác sĩ + Điều dưỡng

**Mục đích:** Chuẩn bị nội mạc tử cung (NMTC) và chuyển phôi đông lạnh.

#### Các bước:

**Bước 1 — Tạo chu kỳ FET**

1. Sidebar → ❄️ **FET** (`/fet`)
2. Nhấn **Tạo FET mới** → `/fet/create`
3. Chọn cặp đôi, Phương pháp: **FET**
4. Nhấn **Lưu**

**Bước 2 — Thiết lập phác đồ**

1. Mở chi tiết FET (`/fet/:id`)
2. Nhấn **Liệu pháp Hormone** → `/fet/:id/hormone-therapy`
3. Chọn loại phác đồ: **HRT** (Hormone Replacement Therapy)
4. Nhập ngày bắt đầu, Ngày chu kỳ, Ghi chú
5. Nhấn **Lưu**

**Bước 3 — Theo dõi NMTC (Endometrium Scan)**

1. Trong trang FET, thêm các lần đo NMTC:
   - Ngày đo, Ngày CK, Độ dày (mm), Hình thái (VD: Trilaminar)
2. Lặp lại 2-3 lần (VD: CK ngày 5 → ngày 12 → ngày 17)

**Bước 4 — Kê đơn chuẩn bị NMTC**

1. Sidebar → 💊 **Nhà thuốc** → Tạo đơn thuốc
2. Thuốc: Estradiol Valerate 2mg, Progesterone 200mg...
3. Nhấn **Lưu**

**Bước 5 — Chuyển phôi**

1. Nhấn **Chuyển phôi** → `/fet/:id/transfer`
2. Chọn phôi đông lạnh → Rã đông → Trạng thái: **Thawed**
3. Thực hiện chuyển phôi → Nhấn **Lưu**

---

### Luồng 9: Thử thai (Beta HCG)

> **Mã:** IVFMD.90 — **Vai trò:** Bác sĩ + Phòng Lab

**Mục đích:** Xét nghiệm Beta HCG 14 ngày sau chuyển phôi để xác nhận có thai.

#### Các bước:

**Bước 1 — Hỗ trợ hoàng thể (Luteal Support)**

1. Sidebar → 💉 **Tiêm** → **Ghi nhận tiêm** → `/injection/log/new`
2. Thuốc: **Progesterone 200mg**, Đường dùng: **PV** (đặt âm đạo)
3. Nhấn **Lưu**

**Bước 2 — Kê đơn hỗ trợ hoàng thể**

1. Sidebar → 💊 **Nhà thuốc** → Tạo đơn
2. Ghi chú: "Hỗ trợ hoàng thể sau chuyển phôi"
3. Nhấn **Lưu**

**Bước 3 — Chỉ định XN Beta HCG**

1. Sidebar → 🧫 **Phòng Lab** → Tạo phiếu XN
2. Loại: **BETA_HCG**
3. Ghi chú: "Xét nghiệm Beta HCG ngày 14 sau chuyển phôi"
4. Nhấn **Lưu**

**Bước 4 — Cập nhật kết quả thai**

1. Mở chi tiết chu kỳ → Mục **Thử thai** → `/pregnancy/:cycleId/beta-hcg`
2. Nhập kết quả Beta HCG
3. Nhấn **Lưu** → Nếu dương tính, chuyển sang Luồng 10

---

### Luồng 10: Thai 7 tuần

> **Mã:** IVFMD.91 — **Vai trò:** Bác sĩ

**Mục đích:** Xác nhận thai trong tử cung bằng siêu âm lúc 7 tuần, theo dõi đến 11 tuần rồi chuyển tuyến sản.

#### Các bước:

**Bước 1 — Siêu âm thai 7 tuần**

1. Mở chi tiết chu kỳ → **Prenatal** → `/pregnancy/:cycleId/prenatal`
2. Hoặc: Sidebar → 🔬 **Siêu âm** → Tạo siêu âm loại **Prenatal7W**
3. Nhập kết quả: số thai, tim thai, kích thước CRL
4. Nhấn **Lưu**

**Bước 2 — Kết quả điều trị**

1. `/pregnancy/:cycleId/result` → Ghi nhận kết quả cuối cùng
2. Nếu thành công → Đặt lịch tái khám thai 12 tuần
3. Sidebar → 📅 **Lịch hẹn** → Loại: **Tái khám (FollowUp)**

**Bước 3 — Xuất viện**

1. `/pregnancy/:cycleId/discharge` → Ghi nhận xuất viện chuyển tuyến sản

---

### Luồng 11: Cho trứng

> **Vai trò:** Bác sĩ + Điều phối + KTV Lab

**Mục đích:** Đăng ký người cho trứng, quản lý mẫu trứng, ghép đôi với cặp nhận.

#### Các bước:

**Bước 1 — Tạo hồ sơ người cho trứng**

1. Sidebar → 🥚 **Người cho trứng** (`/egg-donor`)
2. Nhấn **Đăng ký mới** → `/egg-donor/register`
3. Tạo BN mới với loại: **Cho trứng (EggDonor)**
4. Nhập mã donor, thông tin y khoa
5. Nhấn **Lưu**

**Bước 2 — Xét nghiệm sàng lọc**

1. Sidebar → 🧫 **Phòng Lab** → Tạo phiếu XN
2. Loại: **DONOR_SCREENING**
3. XN: Truyền nhiễm, Hormone, Karyotype...
4. Nhấn **Lưu**

**Bước 3 — Ký đồng thuận hiến trứng**

1. Sidebar → 📋 **Đồng thuận** → Tạo mới
2. Loại: **EGG_DONOR_CONSENT**
3. BN ký → **Lưu**

**Bước 4 — Quản lý mẫu trứng**

1. Chi tiết người cho trứng → **Mẫu** → `/egg-donor/:id/samples`
2. Thêm mẫu trứng: mã mẫu, ngày thu, số lượng

**Bước 5 — Ghép đôi**

1. `/egg-donor/matching` → Chọn người cho + Cặp nhận
2. Nhấn **Ghép đôi** → Hệ thống tạo liên kết

---

### Luồng 12: Ngân hàng tinh trùng

> **Vai trò:** Bác sĩ Nam khoa + KTV Lab

**Mục đích:** Quản lý người hiến tinh trùng, lưu trữ và sử dụng mẫu tinh trùng.

#### Các bước:

**Bước 1 — Đăng ký người hiến**

1. Sidebar → 🏦 **NHTT** (`/sperm-bank`)
2. Nhấn **Đăng ký donor** → Tạo BN loại **Cho tinh trùng (SpermDonor)**
3. Nhập mã donor, thông tin

**Bước 2 — Sàng lọc**

1. `/sperm-bank/screening/:id` → Thực hiện XN sàng lọc
2. Xét duyệt: `/sperm-bank/approve/:id`

**Bước 3 — Thu mẫu**

1. `/sperm-bank/sample/:donorId/collect`
2. Nhập mã mẫu, ngày thu, loại mẫu
3. Nhấn **Lưu**

**Bước 4 — Ký đồng thuận**

1. Sidebar → 📋 **Đồng thuận** → Loại: **SPERM_DONOR_CONSENT**

**Bước 5 — Sử dụng mẫu**

1. `/sperm-bank/sample-usage/:cycleId` → Chọn mẫu, số lọ sử dụng
2. Nhấn **Lưu**

**Bước 6 — XN HIV lại**

1. `/sperm-bank/hiv-retest/:donorId` → XN HIV sau 6 tháng giải phóng mẫu

**Bước 7 — Kho mẫu**

1. `/sperm-bank/samples` → Xem toàn bộ mẫu đang lưu trữ

---

### Luồng 13: Nam khoa

> **Vai trò:** Bác sĩ Nam khoa + KTV Lab

**Mục đích:** Khám nam khoa, xét nghiệm tinh dịch đồ, rửa tinh trùng.

#### Các bước:

**Bước 1 — Khám nam khoa**

1. Sidebar → 🔬 **Nam khoa** (`/andrology`)
2. Dashboard hiển thị danh sách chờ, kết quả XN gần đây

**Bước 2 — Xét nghiệm tinh dịch đồ**

1. Nhấn **Tạo XN mới** → `/andrology/analysis/new`
2. Chọn BN, Loại: **Trước rửa (PreWash)**
3. Nhập các chỉ số: thể tích, mật độ, di động, hình thái...
4. Nhấn **Lưu**

**Bước 3 — Rửa tinh trùng**

1. `/andrology/wash/new`
2. Chọn BN, Phương pháp: **Swim-up** hoặc **Gradient**
3. Nhập kết quả sau rửa
4. Nhấn **Lưu**

**Bước 4 — Xem chi tiết kết quả**

1. `/andrology/analysis/:id` → Xem chi tiết + In phiếu kết quả

---

### Luồng 14: Kho & Vật tư

> **Vai trò:** Thủ kho + Điều dưỡng

**Mục đích:** Quản lý vật tư y tế, nhập kho, xuất dùng, cảnh báo tồn kho.

#### Các bước:

**Bước 1 — Xem tồn kho**

1. Sidebar → 📦 **Kho vật tư** (`/inventory`)
2. Dashboard hiển thị danh sách vật tư, số lượng tồn, trạng thái

**Bước 2 — Nhập kho**

1. `/inventory/import` → Tạo phiếu nhập kho
2. Chọn vật tư, Số lượng, Lô, Hạn dùng, Nhà cung cấp
3. Nhấn **Lưu**

**Bước 3 — Xuất dùng**

1. `/inventory/usage` → Tạo phiếu xuất
2. Chọn vật tư, Số lượng xuất, Mục đích (VD: OPU, ET...)
3. Nhấn **Lưu**

**Bước 4 — Yêu cầu vật tư**

1. `/inventory/requests` → Khoa phòng gửi yêu cầu cấp vật tư
2. Thủ kho duyệt → Xuất kho

**Bước 5 — Cảnh báo tồn kho**

1. `/inventory/alerts` → Xem vật tư dưới mức tối thiểu
2. Hệ thống tự động cảnh báo khi tồn kho ≤ mức min

---

### Luồng 15: Tài chính

> **Vai trò:** Thu ngân (Cashier) + Kế toán

**Mục đích:** Tạo hóa đơn, thu phí dịch vụ, quản lý thanh toán.

#### Các bước:

**Bước 1 — Tạo hóa đơn**

1. Sidebar → 💰 **Hoá đơn** (`/billing`)
2. Nhấn **Tạo hóa đơn** → `/billing/create`
3. Chọn BN, Chu kỳ (nếu có)
4. Thêm dịch vụ (từ danh mục):
   - VD: Khám tư vấn IVF — 300,000₫
   - VD: XN hormone FSH — 200,000₫
   - VD: Chọc hút trứng (OPU) — 8,000,000₫
5. Nhấn **Lưu**

**Bước 2 — Thu tiền**

1. `/billing/payment/:invoiceId` → Tạo phiếu thu
2. Nhập số tiền, Phương thức (Tiền mặt / Chuyển khoản / Thẻ)
3. Nhấn **Thanh toán**

**Bước 3 — Xem lịch sử**

1. `/billing/history` → Xem toàn bộ lịch sử thanh toán
2. Lọc theo BN, thời gian, trạng thái

**Bước 4 — Xem chi tiết hóa đơn**

1. `/billing/:id` → Xem chi tiết + In hóa đơn

---

### Luồng 16: Phòng Lab

> **Vai trò:** KTV Lab (LabTech)

**Mục đích:** Nhận mẫu, thực hiện xét nghiệm, trả kết quả.

#### Các bước:

**Bước 1 — Dashboard Lab**

1. Sidebar → 🧫 **Phòng Lab** (`/lab`)
2. Dashboard hiển thị:
   - Phiếu XN chờ xử lý
   - Kết quả gần đây
   - Thống kê

**Bước 2 — Bàn giao mẫu**

1. `/lab/sample-handover` → Ghi nhận nhận mẫu từ lâm sàng
2. Chọn chu kỳ, loại mẫu, ghi chú
3. Nhấn **Xác nhận nhận mẫu**

**Bước 3 — Nhập kết quả XN**

1. Mở phiếu XN → Nhập kết quả từng test
2. VD: Beta HCG = 350 mIU/mL, FSH = 6.5 IU/L
3. Nhấn **Lưu** → Kết quả hiển thị cho bác sĩ

---

### Luồng 17: Nhà thuốc

> **Vai trò:** Dược sĩ (Pharmacist)

**Mục đích:** Nhận đơn thuốc từ bác sĩ, phát thuốc, ghi nhận sử dụng thuốc.

#### Các bước:

**Bước 1 — Dashboard nhà thuốc**

1. Sidebar → 💊 **Nhà thuốc** (`/pharmacy`)
2. Dashboard hiển thị:
   - Đơn thuốc chờ phát
   - Đơn thuốc đã phát trong ngày
   - Thuốc sắp hết hạn

**Bước 2 — Phát thuốc**

1. Chọn đơn thuốc → Kiểm tra danh sách thuốc
2. Xác nhận phát thuốc → Nhấn **Đã phát**

**Bước 3 — Ghi nhận tiêm thuốc**

1. Sidebar → 💉 **Tiêm** (`/injection`)
2. `/injection/log/new` → Ghi nhận:
   - Tên thuốc, Liều lượng, Đường dùng (SC/IM/PV)
   - Vị trí tiêm, Giờ tiêm
   - Đánh dấu trigger nếu là mũi rụng trứng
3. Nhấn **Lưu**

---

## 4. Thanh điều hướng

Sidebar menu gồm các mục chính (hiển thị theo quyền người dùng):

| Icon | Menu            | Đường dẫn             | Vai trò              |
| ---- | --------------- | --------------------- | -------------------- |
| 📊   | Dashboard       | `/dashboard`          | Tất cả               |
| 🏥   | Tiếp đón        | `/reception`          | Receptionist, Admin  |
| 👥   | Bệnh nhân       | `/patients`           | Doctor, Nurse, Admin |
| 📊   | Phân tích BN    | `/patients/analytics` | Doctor, Admin        |
| 💑   | Cặp đôi         | `/couples`            | Doctor, Nurse        |
| 🎫   | Hàng đợi        | `/queue/all`          | Receptionist, Nurse  |
| 🗣️   | Tư vấn          | `/consultation`       | Doctor               |
| 🔬   | Siêu âm         | `/ultrasound`         | Doctor               |
| 🧫   | Phòng Lab       | `/lab`                | LabTech, Doctor      |
| 🔬   | Nam khoa        | `/andrology`          | Doctor, LabTech      |
| 💉   | Tiêm            | `/injection`          | Nurse                |
| 🏦   | NHTT            | `/sperm-bank`         | Doctor, LabTech      |
| 💊   | Nhà thuốc       | `/pharmacy`           | Pharmacist           |
| 🔧   | Thủ thuật       | `/procedure`          | Doctor               |
| ❄️   | FET             | `/fet`                | Doctor               |
| 📋   | Đồng thuận      | `/consent`            | Doctor, Nurse        |
| 🥚   | Người cho trứng | `/egg-donor`          | Doctor               |
| 📦   | Kho vật tư      | `/inventory`          | Nurse, Admin         |
| 📁   | Hồ sơ giấy      | `/file-tracking`      | Receptionist         |
| 💰   | Hoá đơn         | `/billing`            | Cashier              |
| 📅   | Lịch hẹn        | `/appointments`       | All clinical staff   |
| 📈   | Báo cáo         | `/reports`            | Admin, Doctor        |

---

## 5. Quản trị hệ thống

> Menu **Quản trị** chỉ hiển thị cho vai trò **Admin**.

| Menu           | Đường dẫn                       | Mô tả                                                 |
| -------------- | ------------------------------- | ----------------------------------------------------- |
| Người dùng     | `/admin/enterprise-users`       | Quản lý tài khoản, phân quyền, đặt lại mật khẩu       |
| Phân quyền     | `/admin/permissions`            | Gán quyền (ViewPatients, ManageCycles...) cho vai trò |
| Cấu hình quyền | `/admin/permission-config`      | Tạo/sửa các quyền và nhóm quyền                       |
| Danh mục DV    | `/admin/services`               | Quản lý danh mục dịch vụ + giá (XN, SA, Thủ thuật...) |
| Biểu mẫu       | `/forms`                        | Tạo biểu mẫu động (Form Builder)                      |
| Nhật ký        | `/admin/audit-logs`             | Xem lịch sử thao tác của người dùng                   |
| Thông báo      | `/admin/notifications`          | Quản lý thông báo hệ thống                            |
| Ký số          | `/admin/digital-signing`        | Cấu hình chữ ký số PDF (SignServer + EJBCA)           |
| Danh mục thuốc | `/admin/drug-catalog`           | Quản lý danh mục thuốc                                |
| Mẫu toa thuốc  | `/admin/prescription-templates` | Tạo mẫu đơn thuốc nhanh                               |

---

## Phụ Lục

### A. Phương pháp điều trị

| Mã   | Tên                                | Mô tả                              |
| ---- | ---------------------------------- | ---------------------------------- |
| QHTN | Quan hệ tự nhiên                   | Theo dõi nang noãn chu kỳ tự nhiên |
| IUI  | Bơm tinh trùng                     | Intrauterine Insemination          |
| ICSI | Tiêm tinh trùng vào bào tương noãn | Intracytoplasmic Sperm Injection   |
| IVM  | Trưởng thành trứng non             | In Vitro Maturation                |
| FET  | Chuyển phôi đông lạnh              | Frozen Embryo Transfer             |

### B. Pha chu kỳ

| Pha                | Tên tiếng Việt   | Mô tả                        |
| ------------------ | ---------------- | ---------------------------- |
| Consultation       | Tư vấn           | Khám, tư vấn, chỉ định       |
| OvarianStimulation | KTBT             | Kích thích buồng trứng       |
| TriggerShot        | Tiêm rụng trứng  | Tiêm trigger (Ovitrelle/HCG) |
| EggRetrieval       | Chọc hút trứng   | OPU                          |
| EmbryoCulture      | Nuôi phôi        | Theo dõi phôi D1-D6          |
| EmbryoTransfer     | Chuyển phôi      | Chuyển phôi tươi hoặc FET    |
| LutealSupport      | Hỗ trợ hoàng thể | Progesterone sau chuyển phôi |
| PregnancyTest      | Thử thai         | Beta HCG 14 ngày sau CP      |
| Completed          | Hoàn thành       | Kết thúc chu kỳ              |

### C. Vai trò người dùng

| Vai trò      | Tên tiếng Việt | Phạm vi chính           |
| ------------ | -------------- | ----------------------- |
| Admin        | Quản trị viên  | Toàn bộ hệ thống        |
| Doctor       | Bác sĩ         | Khám, tư vấn, thủ thuật |
| Nurse        | Điều dưỡng     | Tiêm, hỗ trợ thủ thuật  |
| LabTech      | KTV Lab        | Xét nghiệm, kết quả     |
| Embryologist | Phôi học       | Nuôi cấy phôi, ICSI     |
| Receptionist | Tiếp đón       | Tiếp nhận BN, hàng đợi  |
| Cashier      | Thu ngân       | Hóa đơn, thanh toán     |
| Pharmacist   | Dược sĩ        | Phát thuốc, kho thuốc   |

### D. Phím tắt & Mẹo sử dụng

- **Tìm bệnh nhân nhanh:** Trên bất kỳ trang nào cần chọn BN, nhập mã BN hoặc tên để tìm kiếm nhanh
- **Chuyển pha chu kỳ:** Pha chu kỳ tự động chuyển khi hoàn thành thao tác tương ứng (VD: Lưu thủ thuật OPU → chu kỳ chuyển sang EggRetrieval)
- **Thông báo real-time:** Hệ thống gửi thông báo đẩy qua 🔔 khi có sự kiện (lịch hẹn, kết quả XN, đơn thuốc mới)
- **Hàng đợi real-time:** Màn hình hàng đợi tự động cập nhật khi có BN mới hoặc BN được gọi
- **Đồng thuận bắt buộc:** Một số thao tác (OPU, ET) yêu cầu BN đã ký đồng thuận trước khi thực hiện

---

> **Liên hệ hỗ trợ:** Liên hệ Quản trị viên hệ thống nếu gặp vấn đề đăng nhập, phân quyền, hoặc lỗi kỹ thuật.

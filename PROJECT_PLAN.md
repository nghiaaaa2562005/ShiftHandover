# 📋 KẾ HOẠCH DỰ ÁN: ShiftHandover — Hệ Thống Chốt Ca Tạp Hóa

> **Trạng thái:** 🚧 Đang lên kế hoạch  
> **Ngày bắt đầu:** 2026-09-24  
> **Phiên bản hiện tại:** v0.4 (Draft)

---

## 🏪 Giới Thiệu Dự Án

**ShiftHandover** là hệ thống quản lý & chốt ca dành cho cửa hàng tạp hóa. Mục tiêu cốt lõi:

1. **Chống gian lận tiền mặt & chuyển khoản** — Số liệu cuối ca trước được hệ thống tự động chuyển sang làm số liệu đầu ca tiếp theo, nhân viên **không thể tự ý sửa**, chỉ Admin mới có quyền điều chỉnh.
2. **Quản lý âm/dương tiền** — Tính toán chênh lệch giữa doanh thu thực tế và doanh thu ghi nhận trong ca.
3. **Đồng bộ bàn giao ca** — Đảm bảo tính liên tục và minh bạch giữa các ca làm việc.
4. **Định danh nhân viên theo ca** — Ghi nhận rõ ai đã làm việc trong mỗi ca, số lượng người.
5. **Thống kê báo cáo** — Theo dõi doanh thu theo ca / ngày / tháng cho Admin.

---

## 🔐 Nghiệp Vụ Cốt Lõi — Chống Gian Lận Bàn Giao Ca

### Vấn đề thực tế
Khi bàn giao ca trên giấy tờ, nhân viên có thể gian lận bằng cách ghi sai số liệu:

```
Ca Sáng kết thúc:  Doanh thu ngân hàng thực = 1.000.000đ  (ghi trên giấy)
Ca Chiều bắt đầu:  Nhân viên ghi lại        =   900.000đ  (fake)
                                              → Lấy cắp:   100.000đ ❌
```

### Giải pháp của hệ thống
```
Ca Sáng kết thúc → Nhân viên nhập số liệu cuối ca → Hệ thống LƯU & KHÓA
                                                              ↓
Ca Chiều bắt đầu → Hệ thống TỰ ĐỘNG điền số liệu từ cuối ca Sáng vào
                   → Trường này bị KHÓA, nhân viên KHÔNG thể sửa ✅
                   → Chỉ Admin mới có thể override nếu có lý do hợp lệ ✅
```

### Các trường bị khóa khi bàn giao
| Trường | Mô tả |
|--------|-------|
| `opening_bank_balance` | Số dư ngân hàng đầu ca (= cuối ca trước) |
| `opening_cash_balance` | Tiền mặt đầu ca (= cuối ca trước) |
| *(mở rộng thêm nếu cần)* | — |

---

## 🕐 Cấu Trúc Ca Làm Việc

| Ca | Ký hiệu |
|----|---------|
| ☀️ Sáng  | `MORNING` |
| 🌤️ Chiều | `AFTERNOON` |
| 🌙 Tối   | `EVENING` |
| 🌑 Đêm   | `NIGHT` |

- Ca **không cần giờ cụ thể**, chỉ cần phân biệt tên ca
- Mỗi ca có **1 hoặc 2 nhân viên**
- Mỗi nhân viên phải được **định danh** (tên + tài khoản) khi gắn vào ca

---

## 👥 Phân Quyền Người Dùng

| Role | Quyền |
|------|-------|
| **Admin** | Xem tất cả ca, sửa số liệu bị khóa (có log), quản lý nhân viên, xem báo cáo toàn bộ |
| **Nhân viên** | Mở ca, nhập thu/chi trong ca, chốt ca — chỉ thao tác ca của mình |

---

## 🌐 Hạ Tầng & Kết Nối Mạng

- **API Server + SQL Server** chạy trên máy Admin tại nhà (Docker)
- **WPF Client** chạy tại các máy cơ sở, kết nối đến API của Admin qua internet
- **Phương án kết nối**: ⏳ **Chưa xác định** — sẽ quyết định sau
  - 🔵 Cloudflare Tunnel — không cần mở port, HTTPS miễn phí
  - ⚙️ Port Forwarding + DDNS
  - 🔒 VPN (WireGuard)

---

## 🛠️ Công Nghệ Sử Dụng

### Backend
| Công nghệ | Phiên bản | Mục đích |
|-----------|-----------|----------|
| **ASP.NET Core Web API** | .NET 8 | REST API — xử lý nghiệp vụ chính |
| **Entity Framework Core** | 8.x | ORM — giao tiếp với database |
| **SQL Server** | 2022 | Cơ sở dữ liệu chính |
| **JWT Authentication** | — | Xác thực & phân quyền người dùng |

### Frontend (Desktop)
| Công nghệ | Phiên bản | Mục đích |
|-----------|-----------|----------|
| **WPF** (.NET 8) | — | Giao diện desktop cho nhân viên cửa hàng |
| **MVVM Pattern** | — | Kiến trúc UI (CommunityToolkit.Mvvm) |

### Infrastructure & DevOps
| Công nghệ | Mục đích |
|-----------|----------|
| **Docker** | Containerize API + SQL Server |
| **Docker Compose** | Orchestrate các service |
| **Cloudflare Tunnel** *(dự kiến)* | Expose API ra internet an toàn |

### Công Cụ Phát Triển
| Công cụ | Mục đích |
|---------|----------|
| Visual Studio 2022 | IDE chính |
| SQL Server Management Studio (SSMS) | Quản lý database |
| Postman | Test API |
| Git / GitHub | Version control |

---

## 📁 Cấu Trúc Dự Án *(Dự kiến)*

```
ShiftHandover/
├── src/
│   ├── ShiftHandover.API/            # ASP.NET Core Web API
│   ├── ShiftHandover.Domain/         # Entities, Enums, Interfaces
│   ├── ShiftHandover.Application/    # Business Logic, DTOs, Services
│   ├── ShiftHandover.Infrastructure/ # EF Core, Repositories, DB
│   └── ShiftHandover.WPF/            # WPF Desktop Client
├── docker-compose.yml
├── docker-compose.override.yml
└── PROJECT_PLAN.md
```

---

## 🧾 Nghiệp Vụ Chi Tiết — Bảng Chốt Ca (Biên Bản Giao Nhận Ca)

> Dựa trên mẫu giấy thực tế tại cửa hàng (Plus Mart), hệ thống số hóa toàn bộ bảng "Biên Bản Giao Nhận Ca".

### 1. 💵 Tiền Mặt

| Trường | Mô tả | Quy tắc |
|--------|-------|---------|
| `cash_opening` | Tiền mặt đầu ca | Admin set mặc định = **2.000.000đ**. Riêng ca Sáng thì **kế thừa từ `cash_closing` của ca Đêm** trước (không dùng mặc định). Nhân viên không thể tự sửa. |
| `cash_closing` | Tiền mặt cuối ca | Nhân viên nhập tay khi chốt ca |
| `cash_diff` | Chênh lệch tiền mặt | `= cash_closing - cash_opening` — Hiển thị âm/dương |

**Ghi chú đặc biệt:**
- Ca **Sáng** (MORNING): `cash_opening` = `cash_closing` của ca **Đêm** hôm trước (không phải 2.000.000đ mặc định)
- Ca **Chiều, Tối, Đêm**: `cash_opening` mặc định = **2.000.000đ** (Admin đã chuẩn bị két)
- Chỉ **Admin** mới có thể override giá trị `cash_opening` (có audit log)

---

### 2. 🏦 Tiền Tài Khoản (Chuyển Khoản Ngân Hàng)

Mỗi cơ sở sử dụng **tối đa 2 ngân hàng** để nhận chuyển khoản từ khách hàng (ví dụ: TingTing, Zalo Pay, MB Bank…). Tên ngân hàng do Admin cấu hình riêng cho từng cơ sở.

| Trường | Mô tả | Quy tắc |
|--------|-------|---------|
| `bank_1_name` | Tên ngân hàng thứ nhất | Admin đặt tên, per cơ sở |
| `bank_1_opening` | Số dư đầu ca ngân hàng 1 | **Bị khóa** — kế thừa từ `bank_1_closing` của ca trước |
| `bank_1_closing` | Số dư cuối ca ngân hàng 1 | Nhân viên nhập tay |
| `bank_2_name` | Tên ngân hàng thứ hai | Admin đặt tên, per cơ sở (có thể bỏ trống nếu chỉ dùng 1 bank) |
| `bank_2_opening` | Số dư đầu ca ngân hàng 2 | **Bị khóa** — kế thừa từ `bank_2_closing` của ca trước |
| `bank_2_closing` | Số dư cuối ca ngân hàng 2 | Nhân viên nhập tay |

**Quy tắc Reset Ngày — Nguyên nhân thực tế:**
- Bản thân **ứng dụng ngân hàng tự reset** số dư giao dịch về 0 khi sang ngày mới (giống app POS)
- Hệ thống phản ánh thực tế: ca đầu tiên của ngày mới → `bank_X_opening = 0` thay vì kế thừa
- Các ca còn lại trong ngày vẫn kế thừa `bank_X_closing` của ca trước bình thường

---

### 3. 📤 Thu Chi Khác

Phần ghi nhận các khoản chi phát sinh trong ca mà quán **lấy tiền mặt từ két để trả cho nhà cung cấp** (hàng hóa, vật tư, dịch vụ…).

| Trường | Mô tả |
|--------|---------|
| `expense_description` | Tên khoản chi / nhà cung cấp |
| `expense_amount` | Số tiền chi |
| `expense_note` | Ghi chú thêm (tuỳ chọn) |

- Một ca có thể có **nhiều khoản thu chi khác** (danh sách)
- Các khoản chi này **ảnh hưởng đến chênh lệch tiền mặt** cuối ca
- Nhân viên nhập trong ca, Admin có thể xem và audit

---

### 4. 🖥️ Phần Mềm Bán Hàng (POS / App Doanh Thu)

Cửa hàng sử dụng **nhiều phần mềm bán hàng**. Danh sách POS là **linh động hoàn toàn** — Admin có thể thêm, xóa, đổi tên bất kỳ lúc nào. Mỗi cơ sở có danh sách POS **riêng biệt, độc lập** với nhau.

| Trường | Mô tả | Quy tắc |
|--------|-------|---------|
| `pos_name` | Tên phần mềm (tùy đặt) | Admin tự đặt tên, CRUD tự do per cơ sở |
| `pos_opening` | Doanh số đầu ca trên app | **Bị khóa** — kế thừa từ `pos_closing` của ca trước |
| `pos_closing` | Doanh số cuối ca trên app | Nhân viên nhập tay (đọc từ màn hình app bán hàng) |
| `pos_revenue` | Doanh thu trong ca | `= pos_closing - pos_opening` |

**Quy tắc Reset Ngày — Nguyên nhân thực tế:**
- Bản thân **app POS tự reset doanh thu về 0 khi sang ngày mới** (đây là hành vi của phần mềm ngoài, không phải hệ thống làm)
- Tương tự, **tài khoản ngân hàng cũng tự reset** số dư giao dịch trong ngày về 0 khi sang ngày mới
- Hệ thống ShiftHandover **phản ánh thực tế này** bằng cách: ca đầu tiên của ngày mới sẽ có `pos_opening = 0` và `bank_opening = 0` (thay vì kế thừa từ ca trước)
- Tất cả các ca trong ngày (trừ ca đầu tiên) vẫn kế thừa bình thường

**Cấu hình POS per cơ sở:**
- Số lượng POS: **không giới hạn** cứng — Admin thêm bao nhiêu tùy ý
- Tên POS: **tự do đặt** (ví dụ: "Sapo POS", "KiotViet", "Bán tại quầy"…)
- Thay đổi danh sách POS của cơ sở này **không ảnh hưởng** cơ sở khác

---

### 5. 🌑 Quy Tắc Đặc Biệt — Ca Đêm (NIGHT)

Ca Đêm có đặc điểm khác biệt: **bắt đầu từ trước khi sang ngày mới và kết thúc sau khi sang ngày mới** (ví dụ: 22:00 hôm nay → 06:00 hôm sau).

**Đặc điểm nghiệp vụ:**
- Vào lúc 00:00, app POS và ngân hàng tự reset doanh thu trong ngày về 0.
- **Tuy nhiên**, trên app POS và app ngân hàng luôn xem lại được lịch sử/báo cáo của ngày hôm qua.
- **Nhân viên KHÔNG CẦN chốt ca giữa chừng lúc 23:59**, mà đợi **hết ca thực tế (lúc 06:00 sáng) mới làm thủ tục chốt ca 1 LẦN DUY NHẤT**.
- Khi chốt ca đêm, trên màn hình chốt ca sẽ hiển thị **2 ô kết ca** cho từng POS và Ngân hàng:
  - **Ô 1 (`Day1` - trước 00:00)**: Nhân viên chọn lọc ngày hôm trước trên app POS/Ngân hàng để nhập.
  - **Ô 2 (`Day2` - sau 00:00)**: Doanh số từ 00:00 đến lúc hết ca sáng hôm sau.

**Công thức tính doanh thu ca Đêm:**
- Doanh thu POS ca Đêm = `(PosClosingDay1 - PosOpening) + PosClosingDay2`
- Doanh thu Ngân hàng ca Đêm = `(BankClosingDay1 - BankOpening) + BankClosingDay2`
- Ca Sáng hôm sau sẽ kế thừa `CashClosing` từ ca Đêm; còn `BankOpening` và `PosOpening` sẽ bắt đầu ngày mới `= 0`.

---

### 6. 🔢 Công Thức Tổng Hợp Biên Bản Giao Nhận Ca & Âm Dương

```
[0] TỔNG DOANH SỐ KẾT CA   = (1) + (8)
[1] TỔNG DOANH SỐ ĐẦU CA   = Lấy từ ca trước (bị khóa)
[2] CHÊNH LỆCH TIỀN MẶT    = Tiền mặt kết ca thực tế - Tiền mặt lý thuyết
                            = cash_closing - [cash_opening + doanh_thu_tiền_mặt_bán_được - thu_chi_khác]
                            = Lưu trực tiếp vào cột `CashDifference` trong bảng Shifts!
[3] TIỀN MẶT ĐẦU CA        = cash_opening (admin set hoặc kế thừa từ ca đêm)
[4] TIỀN MẶT KẾT CA        = cash_closing (nhân viên đếm két thực tế)
[5] THU CHI KHÁC           = Tổng các khoản chi trả nhà cung cấp trong ca
[6] CHUYỂN KHOẢN (Bank 1)  = bank_1_closing - bank_1_opening (hoặc tổng 2 ngày nếu ca Đêm)
[7] CHUYỂN KHOẢN (Bank 2)  = bank_2_closing - bank_2_opening (hoặc tổng 2 ngày nếu ca Đêm)
[8] DOANH SỐ KẾT CA        = Tổng doanh thu thực từ tất cả POS
```

---

## 🗄️ Thiết Kế Cơ Sở Dữ Liệu (Database Schema v2.0)

> Cơ sở dữ liệu được tối ưu tinh gọn thành **8 bảng cốt lõi**, loại bỏ các bảng trung gian thừa, lưu trực tiếp số tiền âm/dương để xem báo cáo theo nhân viên trong khoảng thời gian siêu tốc.

### 1. `Users` — Tài khoản người dùng & Số lượng ca làm
| Tên cột | Kiểu dữ liệu | Ràng buộc | Ý nghĩa / Mô tả |
|---|---|---|---|
| `Id` | `INT` | PK, Identity(1,1) | Mã định danh người dùng |
| `Username` | `NVARCHAR(50)` | NOT NULL, UNIQUE | Tên đăng nhập |
| `PasswordHash` | `NVARCHAR(256)` | NOT NULL | Mật khẩu mã hóa BCrypt |
| `FullName` | `NVARCHAR(100)` | NOT NULL | Họ tên nhân viên / admin |
| `Role` | `NVARCHAR(20)` | NOT NULL, CHECK(`Admin`, `Employee`) | Phân quyền vai trò |
| `ShiftCount` | `INT` | NOT NULL, Default = 0 | **Số lượng ca làm** nhân viên đã hoàn thành |
| `IsActive` | `BIT` | NOT NULL, Default = 1 | Trạng thái hoạt động |
| `CreatedAt` | `DATETIME2` | NOT NULL, Default = UTC | Thời điểm tạo |

### 2. `Branches` — Cơ sở / Chi nhánh
| Tên cột | Kiểu dữ liệu | Ràng buộc | Ý nghĩa / Mô tả |
|---|---|---|---|
| `Id` | `INT` | PK, Identity(1,1) | Mã định danh cơ sở |
| `Name` | `NVARCHAR(100)` | NOT NULL | Tên cơ sở (VD: Plus Mart 96) |
| `DefaultCashOpening` | `DECIMAL(18,0)` | NOT NULL, Default = 2.000.000 | Mức tiền mặt đầu ca mặc định của quán |
| `IsActive` | `BIT` | NOT NULL, Default = 1 | Chi nhánh còn hoạt động hay không |

### 3. `BranchBanks` — Cấu hình Ngân hàng của cơ sở (Tối đa 2)
| Tên cột | Kiểu dữ liệu | Ràng buộc | Ý nghĩa / Mô tả |
|---|---|---|---|
| `Id` | `INT` | PK, Identity(1,1) | Mã định danh |
| `BranchId` | `INT` | NOT NULL, FK -> `Branches(Id)` | Thuộc cơ sở nào |
| `SlotIndex` | `TINYINT` | NOT NULL, CHECK(1, 2) | Slot 1 hoặc Slot 2 (mỗi cơ sở tối đa 2 bank) |
| `BankName` | `NVARCHAR(100)` | NOT NULL | Tên ngân hàng tự do (TingTing, Zalo Pay, MB...) |
| `IsActive` | `BIT` | NOT NULL, Default = 1 | Đang bật sử dụng hay không |
| *Ràng buộc:* | `UNIQUE(BranchId, SlotIndex)` | | Đảm bảo không trùng Slot |

### 4. `PosConfigs` — Cấu hình Phần mềm bán hàng theo cơ sở (Linh động)
| Tên cột | Kiểu dữ liệu | Ràng buộc | Ý nghĩa / Mô tả |
|---|---|---|---|
| `Id` | `INT` | PK, Identity(1,1) | Mã định danh |
| `BranchId` | `INT` | NOT NULL, FK -> `Branches(Id)` | Thuộc cơ sở nào (POS độc lập giữa các cơ sở) |
| `PosName` | `NVARCHAR(100)` | NOT NULL | Tên app POS (Sapo POS, KiotViet, Bán tại quầy...) |
| `DisplayOrder` | `TINYINT` | NOT NULL, Default = 1 | Thứ tự hiển thị trên form |
| `IsActive` | `BIT` | NOT NULL, Default = 1 | Đang sử dụng hay không |

### 5. `Shifts` — Ca làm việc & Tiền mặt
| Tên cột | Kiểu dữ liệu | Ràng buộc | Ý nghĩa / Mô tả |
|---|---|---|---|
| `Id` | `INT` | PK, Identity(1,1) | Mã ca |
| `BranchId` | `INT` | NOT NULL, FK -> `Branches(Id)` | Thuộc cơ sở nào |
| `ShiftDate` | `DATE` | NOT NULL | Ngày làm việc (yyyy-MM-dd) |
| `ShiftType` | `NVARCHAR(15)` | NOT NULL, CHECK(MORNING, AFTERNOON, EVENING, NIGHT) | Tên ca |
| `Status` | `NVARCHAR(15)` | NOT NULL, Default = OPEN | OPEN (đang làm) / CLOSED (đã chốt) |
| `CashOpening` | `DECIMAL(18,0)` | NOT NULL, Default = 2.000.000 | **Tiền mặt đầu ca (Bị khóa)**: ca Sáng lấy từ ca Đêm, ca khác mặc định |
| `CashClosing` | `DECIMAL(18,0)` | NULL | **Tiền mặt cuối ca**: do nhân viên đếm két nhập vào |
| `CashDifference` | `DECIMAL(18,0)` | NULL | **Số tiền Âm/Dương**: lưu cứng khi chốt ca để xem báo cáo theo thời gian |
| `IsNewDayFirstShift` | `BIT` | NOT NULL, Default = 0 | Ca đầu ngày mới -> POS & Bank reset đầu ca = 0 |
| `OpenedByUserId` | `INT` | NULL, FK -> `Users(Id)` | Người mở ca |
| `ClosedByUserId` | `INT` | NULL, FK -> `Users(Id)` | Người chốt ca |
| `OpenedAt`, `ClosedAt` | `DATETIME2` | NULL | Thời gian thực tế mở / chốt |
| `Note` | `NVARCHAR(500)` | NULL | Ghi chú ca |
| `CreatedAt`, `UpdatedAt` | `DATETIME2` | NOT NULL | Thời gian tạo / sửa |
| *Ràng buộc:* | `UNIQUE(BranchId, ShiftDate, ShiftType)` | | Mỗi cơ sở chỉ có 1 ca cùng loại trong ngày |

### 6. `ShiftEmployees` — Gắn nhân viên vào ca
| Tên cột | Kiểu dữ liệu | Ràng buộc | Ý nghĩa / Mô tả |
|---|---|---|---|
| `Id` | `INT` | PK, Identity(1,1) | Mã bản ghi |
| `ShiftId` | `INT` | NOT NULL, FK -> `Shifts(Id)` | Ca làm việc |
| `UserId` | `INT` | NOT NULL, FK -> `Users(Id)` | Nhân viên trực (1 ca có 1 - 2 người) |
| *Ràng buộc:* | `UNIQUE(ShiftId, UserId)` | | Không bị trùng nhân viên trong ca |

### 7. `ShiftBankEntries` — Số liệu Ngân hàng trong ca
| Tên cột | Kiểu dữ liệu | Ràng buộc | Ý nghĩa / Mô tả |
|---|---|---|---|
| `Id` | `INT` | PK, Identity(1,1) | Mã bản ghi |
| `ShiftId` | `INT` | NOT NULL, FK -> `Shifts(Id)` | Thuộc ca nào |
| `BranchBankId` | `INT` | NOT NULL, FK -> `BranchBanks(Id)` | Ngân hàng nào |
| `BankOpening` | `DECIMAL(18,0)` | NOT NULL, Default = 0 | **Đầu ca (Bị khóa)**: Đầu ngày = 0, ca khác = cuối ca trước |
| `BankClosing` | `DECIMAL(18,0)` | NULL | Cuối ca dành cho **Ca Thường** (Sáng/Chiều/Tối) |
| `BankClosingDay1` | `DECIMAL(18,0)` | NULL | Cuối ca **ngày cũ** trước 00:00 *(Dành cho Ca Đêm)* |
| `BankClosingDay2` | `DECIMAL(18,0)` | NULL | Cuối ca **ngày mới** sau 00:00 *(Dành cho Ca Đêm)* |
| *Ràng buộc:* | `UNIQUE(ShiftId, BranchBankId)` | | Mỗi ngân hàng 1 dòng per ca |

### 8. `ShiftPosEntries` — Doanh số App POS trong ca
| Tên cột | Kiểu dữ liệu | Ràng buộc | Ý nghĩa / Mô tả |
|---|---|---|---|
| `Id` | `INT` | PK, Identity(1,1) | Mã bản ghi |
| `ShiftId` | `INT` | NOT NULL, FK -> `Shifts(Id)` | Thuộc ca nào |
| `PosConfigId` | `INT` | NOT NULL, FK -> `PosConfigs(Id)` | App POS nào (Sapo, KiotViet...) |
| `PosOpening` | `DECIMAL(18,0)` | NOT NULL, Default = 0 | **Đầu ca (Bị khóa)**: Đầu ngày = 0, ca khác = cuối ca trước |
| `PosClosing` | `DECIMAL(18,0)` | NULL | Cuối ca dành cho **Ca Thường** (Sáng/Chiều/Tối) |
| `PosClosingDay1` | `DECIMAL(18,0)` | NULL | Cuối ca **ngày cũ** trước 00:00 *(Dành cho Ca Đêm)* |
| `PosClosingDay2` | `DECIMAL(18,0)` | NULL | Cuối ca **ngày mới** sau 00:00 *(Dành cho Ca Đêm)* |
| *Ràng buộc:* | `UNIQUE(ShiftId, PosConfigId)` | | Mỗi app POS 1 dòng per ca |

### 9. `ShiftExpenses` — Thu chi khác trong ca
| Tên cột | Kiểu dữ liệu | Ràng buộc | Ý nghĩa / Mô tả |
|---|---|---|---|
| `Id` | `INT` | PK, Identity(1,1) | Mã bản ghi |
| `ShiftId` | `INT` | NOT NULL, FK -> `Shifts(Id)` | Thuộc ca nào |
| `Description` | `NVARCHAR(255)` | NOT NULL | Khoản chi (lấy tiền két trả NCC bánh, rau, bia...) |
| `Amount` | `DECIMAL(18,0)` | NOT NULL | Số tiền chi ra |
| `Note` | `NVARCHAR(500)` | NULL | Ghi chú thêm |
| `CreatedByUserId` | `INT` | NOT NULL, FK -> `Users(Id)` | Nhân viên ghi nhận chi |
| `CreatedAt` | `DATETIME2` | NOT NULL, Default = UTC | Thời điểm chi |

---

## 📌 Các Module Chính

- [ ] **Xác thực & Phân quyền** — Đăng nhập JWT, Role Admin / Nhân viên
- [ ] **Quản lý Nhân viên & Số ca làm** — CRUD tài khoản, theo dõi `ShiftCount` của từng người
- [ ] **Quản lý Cơ sở, POS & Ngân hàng** — Cấu hình linh động per chi nhánh
- [ ] **Quản lý Ca làm việc** — Mở ca / Chốt ca / Bàn giao tự động
- [ ] **Chống gian lận bàn giao** — Khóa số liệu đầu ca, chỉ Admin override
- [ ] **Tiền mặt** — Đầu/cuối ca, ca Sáng kế thừa ca Đêm, ca khác mặc định 2.000.000đ
- [ ] **Ngân hàng (≤2 bank/cơ sở)** — Chốt theo ca, hỗ trợ 2 ô cho ca Đêm, reset đầu ngày = 0
- [ ] **Phần mềm bán hàng (linh động)** — Chốt theo ca, hỗ trợ 2 ô cho ca Đêm, reset đầu ngày = 0
- [ ] **Thu Chi Khác** — Ghi nhận các khoản chi lấy tiền két trả NCC
- [ ] **Nghiệp vụ Ca Đêm tinh gọn** — Nhập 1 lần lúc hết ca (xem lại lịch sử trước 00:00 & sau 00:00)
- [ ] **Quản lý Âm / Dương tiền** — Tính toán & lưu `CashDifference` vào ca, xem báo cáo theo nhân viên trong khoảng thời gian
- [ ] **Báo cáo & Thống kê** — Doanh thu, chênh lệch theo ca / ngày / tháng cho Admin

---

## ✅ Đã Xác Nhận

| # | Vấn đề | Quyết định |
|---|--------|------------|
| 1 | Giờ ca | Không cần giờ cụ thể — chỉ phân biệt tên: Sáng / Chiều / Tối / Đêm |
| 2 | Số cơ sở | Admin tự tạo & quản lý cơ sở — dữ liệu **phân theo cơ sở** |
| 3 | Ghi nhận doanh thu | Nhân viên **nhập tay** từ màn hình app POS, không tích hợp API ngoài |
| 4 | Tồn kho | Không cần kiểm tra tồn kho khi chốt ca |
| 5 | In phiếu | Không cần in phiếu / xuất PDF |
| 6 | Tiền mặt đầu ca mặc định | Admin set = **2.000.000đ**, trừ ca Sáng kế thừa từ ca Đêm |
| 7 | Số ngân hàng per cơ sở | Tối đa **2 ngân hàng** — Admin tự đặt tên |
| 8 | Reset ngân hàng đầu ngày | App ngân hàng **tự reset** — hệ thống phản ánh bằng `bank_opening = 0` ở ca đầu ngày |
| 9 | Reset POS đầu ngày | App POS **tự reset** — hệ thống phản ánh bằng `pos_opening = 0` ở ca đầu ngày |
| 10 | Ca Đêm tinh gọn | **Nhập 1 lần lúc hết ca**, xem lại lịch sử app để điền 2 ô: trước 00:00 & sau 00:00 |
| 11 | Thu chi khác | Quán dùng tiền mặt trong két để trả nhà cung cấp — ghi nhiều dòng per ca |
| 12 | POS linh động | Số lượng & tên POS **không giới hạn**, Admin CRUD tự do, **độc lập per cơ sở** |
| 13 | Quản lý Âm/Dương | Lưu trực tiếp `CashDifference` vào bảng `Shifts`, lọc báo cáo theo nhân viên + thời gian |
| 14 | Quản lý công ca | Cột `ShiftCount` trong bảng `Users` đếm số lượng ca làm của nhân viên |

## ❓ Còn Đang Thảo Luận

1. **Kết nối mạng**: Chưa xác định — cần quyết định Cloudflare Tunnel / Port Forwarding / VPN

---

*📝 File này sẽ được cập nhật liên tục trong suốt quá trình phát triển dự án.*


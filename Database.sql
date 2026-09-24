-- =============================================================================
--  ShiftHandover — Hệ Thống Chốt Ca Tạp Hóa
--  Database: SQL Server 2022
--  File    : Database.sql
--  Version : v2.0 (Tinh gọn — Tối ưu nghiệp vụ thực tế)
--  Updated : 2026-09-24
-- =============================================================================
-- Danh sách 8 bảng cốt lõi:
--   1. Users              : Quản lý Admin & Nhân viên, có ShiftCount đếm số ca
--   2. Branches           : Quản lý cơ sở / chi nhánh
--   3. BranchBanks        : Cấu hình ngân hàng theo cơ sở (tối đa 2 bank)
--   4. PosConfigs         : Cấu hình phần mềm bán hàng theo cơ sở (linh động)
--   5. Shifts             : Ca làm việc, có CashDifference để lưu âm/dương
--   6. ShiftEmployees     : Gắn 1 - 2 nhân viên vào ca
--   7. ShiftBankEntries   : Tiền ngân hàng (hỗ trợ cả ca thường lẫn 2 ô của ca Đêm)
--   8. ShiftPosEntries    : Doanh số POS (hỗ trợ cả ca thường lẫn 2 ô của ca Đêm)
--   9. ShiftExpenses      : Thu chi khác trong ca (tiền két trả nhà cung cấp)
-- =============================================================================

USE master;
GO

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'ShiftHandoverDB')
BEGIN
    CREATE DATABASE ShiftHandoverDB
    COLLATE Vietnamese_CI_AS;
END
GO

USE ShiftHandoverDB;
GO

-- =============================================================================
-- 1. USERS — Quản lý tài khoản Admin & Nhân viên
-- =============================================================================
IF OBJECT_ID('dbo.Users', 'U') IS NULL
BEGIN
    CREATE TABLE Users (
        Id            INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
        Username      NVARCHAR(50)    NOT NULL UNIQUE,
        PasswordHash  NVARCHAR(256)   NOT NULL,
        FullName      NVARCHAR(100)   NOT NULL,
        Role          NVARCHAR(20)    NOT NULL
            CONSTRAINT CHK_Users_Role CHECK (Role IN ('Admin', 'Employee')),
        ShiftCount    INT             NOT NULL DEFAULT 0, -- Số lượng ca làm đã hoàn thành
        IsActive      BIT             NOT NULL DEFAULT 1,
        CreatedAt     DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME()
    );
END
GO

-- =============================================================================
-- 2. BRANCHES — Cơ sở / Chi nhánh
-- =============================================================================
IF OBJECT_ID('dbo.Branches', 'U') IS NULL
BEGIN
    CREATE TABLE Branches (
        Id                 INT           NOT NULL IDENTITY(1,1) PRIMARY KEY,
        Name               NVARCHAR(100) NOT NULL,
        DefaultCashOpening DECIMAL(18,0) NOT NULL DEFAULT 2000000, -- Mức tiền mặt két mặc định
        IsActive           BIT           NOT NULL DEFAULT 1
    );
END
GO

-- =============================================================================
-- 3. BRANCH_BANKS — Cấu hình ngân hàng cho từng cơ sở (Tối đa 2 ngân hàng)
-- =============================================================================
IF OBJECT_ID('dbo.BranchBanks', 'U') IS NULL
BEGIN
    CREATE TABLE BranchBanks (
        Id          INT           NOT NULL IDENTITY(1,1) PRIMARY KEY,
        BranchId    INT           NOT NULL
            CONSTRAINT FK_BranchBanks_Branches FOREIGN KEY REFERENCES Branches(Id),
        SlotIndex   TINYINT       NOT NULL
            CONSTRAINT CHK_BranchBanks_Slot CHECK (SlotIndex IN (1, 2)),
        BankName    NVARCHAR(100) NOT NULL,              -- Tên ngân hàng: TingTing, Zalo Pay, MB...
        IsActive    BIT           NOT NULL DEFAULT 1,
        CONSTRAINT UQ_BranchBanks_BranchSlot UNIQUE (BranchId, SlotIndex)
    );
END
GO

-- =============================================================================
-- 4. POS_CONFIGS — Cấu hình phần mềm bán hàng theo cơ sở (Linh động, không giới hạn)
-- =============================================================================
IF OBJECT_ID('dbo.PosConfigs', 'U') IS NULL
BEGIN
    CREATE TABLE PosConfigs (
        Id           INT           NOT NULL IDENTITY(1,1) PRIMARY KEY,
        BranchId     INT           NOT NULL
            CONSTRAINT FK_PosConfigs_Branches FOREIGN KEY REFERENCES Branches(Id),
        PosName      NVARCHAR(100) NOT NULL,             -- Sapo POS, KiotViet, Bán tại quầy...
        DisplayOrder TINYINT       NOT NULL DEFAULT 1,
        IsActive     BIT           NOT NULL DEFAULT 1
    );
END
GO

-- =============================================================================
-- 5. SHIFTS — Ca làm việc & Tiền mặt
--    Bổ sung CashDifference: Lưu cứng số tiền âm/dương để xem báo cáo theo thời gian
-- =============================================================================
IF OBJECT_ID('dbo.Shifts', 'U') IS NULL
BEGIN
    CREATE TABLE Shifts (
        Id                 INT           NOT NULL IDENTITY(1,1) PRIMARY KEY,
        BranchId           INT           NOT NULL
            CONSTRAINT FK_Shifts_Branches FOREIGN KEY REFERENCES Branches(Id),
        ShiftDate          DATE          NOT NULL,           -- Ngày bắt đầu làm ca
        ShiftType          NVARCHAR(15)  NOT NULL            -- MORNING | AFTERNOON | EVENING | NIGHT
            CONSTRAINT CHK_Shifts_Type CHECK (ShiftType IN ('MORNING', 'AFTERNOON', 'EVENING', 'NIGHT')),
        Status             NVARCHAR(15)  NOT NULL DEFAULT 'OPEN'
            CONSTRAINT CHK_Shifts_Status CHECK (Status IN ('OPEN', 'CLOSED')),
        
        -- Tiền mặt đầu ca (Bị khóa: ca Sáng lấy từ ca Đêm, ca khác mặc định 2.000.000)
        CashOpening        DECIMAL(18,0) NOT NULL DEFAULT 2000000,
        -- Tiền mặt cuối ca (Nhân viên đếm két thực tế và nhập vào)
        CashClosing        DECIMAL(18,0) NULL,
        
        -- Số tiền âm/dương của ca (Lưu trực tiếp khi chốt ca, dùng lọc báo cáo cực nhanh)
        -- Âm: thiếu tiền (VD: -23.000đ), Dương: thừa tiền (VD: +50.000đ)
        CashDifference     DECIMAL(18,0) NULL,
        
        -- Đánh dấu ca đầu tiên của ngày mới (khi đó Bank & POS tự động reset đầu ca = 0)
        IsNewDayFirstShift BIT           NOT NULL DEFAULT 0,
        
        OpenedByUserId     INT           NULL
            CONSTRAINT FK_Shifts_OpenedBy FOREIGN KEY REFERENCES Users(Id),
        ClosedByUserId     INT           NULL
            CONSTRAINT FK_Shifts_ClosedBy FOREIGN KEY REFERENCES Users(Id),
        OpenedAt           DATETIME2     NULL,
        ClosedAt           DATETIME2     NULL,
        Note               NVARCHAR(500) NULL,
        CreatedAt          DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAt          DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
        
        CONSTRAINT UQ_Shifts_BranchDateType UNIQUE (BranchId, ShiftDate, ShiftType)
    );
END
GO

-- =============================================================================
-- 6. SHIFT_EMPLOYEES — Danh sách nhân viên trong ca (1 hoặc 2 người)
-- =============================================================================
IF OBJECT_ID('dbo.ShiftEmployees', 'U') IS NULL
BEGIN
    CREATE TABLE ShiftEmployees (
        Id        INT NOT NULL IDENTITY(1,1) PRIMARY KEY,
        ShiftId   INT NOT NULL
            CONSTRAINT FK_ShiftEmployees_Shifts FOREIGN KEY REFERENCES Shifts(Id),
        UserId    INT NOT NULL
            CONSTRAINT FK_ShiftEmployees_Users FOREIGN KEY REFERENCES Users(Id),
        CONSTRAINT UQ_ShiftEmployees_ShiftUser UNIQUE (ShiftId, UserId)
    );
END
GO

-- =============================================================================
-- 7. SHIFT_BANK_ENTRIES — Số liệu chuyển khoản ngân hàng trong ca
--    Chốt hết ca một lần:
--      • Ca thường (Sáng, Chiều, Tối): điền BankClosing
--      • Ca Đêm: điền cả BankClosingDay1 (trước 00:00) và BankClosingDay2 (sau 00:00)
-- =============================================================================
IF OBJECT_ID('dbo.ShiftBankEntries', 'U') IS NULL
BEGIN
    CREATE TABLE ShiftBankEntries (
        Id               INT           NOT NULL IDENTITY(1,1) PRIMARY KEY,
        ShiftId          INT           NOT NULL
            CONSTRAINT FK_ShiftBankEntries_Shifts FOREIGN KEY REFERENCES Shifts(Id),
        BranchBankId     INT           NOT NULL
            CONSTRAINT FK_ShiftBankEntries_BranchBanks FOREIGN KEY REFERENCES BranchBanks(Id),
        
        -- Số dư đầu ca (Bị khóa: đầu ngày mới = 0, ca khác = cuối ca trước)
        BankOpening      DECIMAL(18,0) NOT NULL DEFAULT 0,
        
        -- Dành cho Ca Thường (Sáng / Chiều / Tối)
        BankClosing      DECIMAL(18,0) NULL,
        
        -- Dành riêng cho Ca Đêm (Nhân viên xem lịch sử app và nhập 1 lần lúc hết ca)
        BankClosingDay1  DECIMAL(18,0) NULL, -- Tiền kết ngày hôm trước (trước 00:00)
        BankClosingDay2  DECIMAL(18,0) NULL, -- Tiền kết ngày hôm sau (sau 00:00)
        
        CONSTRAINT UQ_ShiftBankEntries_ShiftBank UNIQUE (ShiftId, BranchBankId)
    );
END
GO

-- =============================================================================
-- 8. SHIFT_POS_ENTRIES — Doanh số phần mềm bán hàng trong ca
--    Chốt hết ca một lần:
--      • Ca thường (Sáng, Chiều, Tối): điền PosClosing
--      • Ca Đêm: điền cả PosClosingDay1 (trước 00:00) và PosClosingDay2 (sau 00:00)
-- =============================================================================
IF OBJECT_ID('dbo.ShiftPosEntries', 'U') IS NULL
BEGIN
    CREATE TABLE ShiftPosEntries (
        Id               INT           NOT NULL IDENTITY(1,1) PRIMARY KEY,
        ShiftId          INT           NOT NULL
            CONSTRAINT FK_ShiftPosEntries_Shifts FOREIGN KEY REFERENCES Shifts(Id),
        PosConfigId      INT           NOT NULL
            CONSTRAINT FK_ShiftPosEntries_PosConfigs FOREIGN KEY REFERENCES PosConfigs(Id),
        
        -- Doanh số đầu ca (Bị khóa: đầu ngày mới = 0, ca khác = cuối ca trước)
        PosOpening       DECIMAL(18,0) NOT NULL DEFAULT 0,
        
        -- Dành cho Ca Thường (Sáng / Chiều / Tối)
        PosClosing       DECIMAL(18,0) NULL,
        
        -- Dành riêng cho Ca Đêm (Nhân viên xem lịch sử POS và nhập 1 lần lúc hết ca)
        PosClosingDay1   DECIMAL(18,0) NULL, -- Doanh số chốt ngày hôm trước (trước 00:00)
        PosClosingDay2   DECIMAL(18,0) NULL, -- Doanh số chốt ngày hôm sau (sau 00:00)
        
        CONSTRAINT UQ_ShiftPosEntries_ShiftPos UNIQUE (ShiftId, PosConfigId)
    );
END
GO

-- =============================================================================
-- 9. SHIFT_EXPENSES — Thu chi khác (lấy tiền mặt của quán trả NCC hàng, đồ,...)
-- =============================================================================
IF OBJECT_ID('dbo.ShiftExpenses', 'U') IS NULL
BEGIN
    CREATE TABLE ShiftExpenses (
        Id              INT           NOT NULL IDENTITY(1,1) PRIMARY KEY,
        ShiftId         INT           NOT NULL
            CONSTRAINT FK_ShiftExpenses_Shifts FOREIGN KEY REFERENCES Shifts(Id),
        Description     NVARCHAR(255) NOT NULL,          -- Khoản chi (trả bia, bánh, rau,...)
        Amount          DECIMAL(18,0) NOT NULL,          -- Số tiền lấy két ra trả
        Note            NVARCHAR(500) NULL,
        CreatedByUserId INT           NOT NULL
            CONSTRAINT FK_ShiftExpenses_Users FOREIGN KEY REFERENCES Users(Id),
        CreatedAt       DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME()
    );
END
GO

-- =============================================================================
-- INDEXES — Tối ưu hóa truy vấn tìm kiếm & báo cáo
-- =============================================================================
IF NOT EXISTS (SELECT name FROM sys.indexes WHERE name = N'IX_Shifts_BranchDate')
    CREATE INDEX IX_Shifts_BranchDate ON Shifts(BranchId, ShiftDate DESC);

IF NOT EXISTS (SELECT name FROM sys.indexes WHERE name = N'IX_Shifts_Status')
    CREATE INDEX IX_Shifts_Status ON Shifts(Status);

IF NOT EXISTS (SELECT name FROM sys.indexes WHERE name = N'IX_ShiftBankEntries_ShiftId')
    CREATE INDEX IX_ShiftBankEntries_ShiftId ON ShiftBankEntries(ShiftId);

IF NOT EXISTS (SELECT name FROM sys.indexes WHERE name = N'IX_ShiftPosEntries_ShiftId')
    CREATE INDEX IX_ShiftPosEntries_ShiftId ON ShiftPosEntries(ShiftId);

IF NOT EXISTS (SELECT name FROM sys.indexes WHERE name = N'IX_ShiftExpenses_ShiftId')
    CREATE INDEX IX_ShiftExpenses_ShiftId ON ShiftExpenses(ShiftId);

IF NOT EXISTS (SELECT name FROM sys.indexes WHERE name = N'IX_ShiftEmployees_UserId')
    CREATE INDEX IX_ShiftEmployees_UserId ON ShiftEmployees(UserId);
GO

-- =============================================================================
-- STORED PROCEDURE: BÁO CÁO ÂM DƯƠNG THEO NHÂN VIÊN TRONG KHOẢNG THỜI GIAN
-- =============================================================================
CREATE OR ALTER PROCEDURE sp_GetEmployeeCashVarianceReport
    @UserId INT,
    @FromDate DATE,
    @ToDate DATE
AS
BEGIN
    SET NOCOUNT ON;

    -- Danh sách từng ca nhân viên đã làm kèm số tiền âm / dương
    SELECT 
        u.FullName                          AS EmployeeName,
        b.Name                              AS BranchName,
        s.ShiftDate,
        s.ShiftType,
        s.CashOpening,
        s.CashClosing,
        s.CashDifference,                   -- Số tiền âm dương của ca
        CASE 
            WHEN s.CashDifference < 0 THEN N'Âm (Thiếu tiền)'
            WHEN s.CashDifference > 0 THEN N'Dương (Thừa tiền)'
            ELSE N'Khớp đủ'
        END                                 AS VarianceStatus,
        s.OpenedAt,
        s.ClosedAt
    FROM ShiftEmployees se
    JOIN Users u   ON u.Id = se.UserId
    JOIN Shifts s  ON s.Id = se.ShiftId
    JOIN Branches b ON b.Id = s.BranchId
    WHERE se.UserId = @UserId
      AND s.ShiftDate BETWEEN @FromDate AND @ToDate
      AND s.Status = 'CLOSED'
    ORDER BY s.ShiftDate DESC, s.OpenedAt DESC;

    -- Tổng kết âm dương trong khoảng thời gian của nhân viên
    SELECT 
        u.FullName                          AS EmployeeName,
        COUNT(s.Id)                         AS TotalShiftsWorked,
        SUM(ISNULL(s.CashDifference, 0))    AS TotalCashDifference,
        CASE 
            WHEN SUM(ISNULL(s.CashDifference, 0)) < 0 THEN N'Tổng Âm tiền'
            WHEN SUM(ISNULL(s.CashDifference, 0)) > 0 THEN N'Tổng Dương tiền'
            ELSE N'Tổng Khớp'
        END                                 AS OverallStatus
    FROM ShiftEmployees se
    JOIN Users u   ON u.Id = se.UserId
    JOIN Shifts s  ON s.Id = se.ShiftId
    WHERE se.UserId = @UserId
      AND s.ShiftDate BETWEEN @FromDate AND @ToDate
      AND s.Status = 'CLOSED'
    GROUP BY u.FullName;
END
GO

-- =============================================================================
-- DỮ LIỆU KHỞI TẠO MẪU (SEED DATA)
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM Users WHERE Username = 'admin')
BEGIN
    INSERT INTO Users (Username, PasswordHash, FullName, Role, ShiftCount) VALUES
        ('admin',   '$2a$12$PLACEHOLDER_HASH_REPLACE_IN_PRODUCTION', N'Quản Trị Viên',   'Admin',    0),
        ('nv_duy',  '$2a$12$PLACEHOLDER_HASH_REPLACE_IN_PRODUCTION', N'Nguyễn Văn Duy',  'Employee', 0),
        ('nv_khoa', '$2a$12$PLACEHOLDER_HASH_REPLACE_IN_PRODUCTION', N'Trần Minh Khoa', 'Employee', 0);
END

IF NOT EXISTS (SELECT 1 FROM Branches WHERE Name = N'Plus Mart - Cơ Sở 96')
BEGIN
    INSERT INTO Branches (Name, DefaultCashOpening) VALUES
        (N'Plus Mart - Cơ Sở 96', 2000000),
        (N'Plus Mart - Cơ Sở 2',  2000000);
END

IF NOT EXISTS (SELECT 1 FROM BranchBanks WHERE BranchId = 1)
BEGIN
    INSERT INTO BranchBanks (BranchId, SlotIndex, BankName) VALUES
        (1, 1, N'TingTing'),
        (1, 2, N'Zalo Pay');
END

IF NOT EXISTS (SELECT 1 FROM PosConfigs WHERE BranchId = 1)
BEGIN
    INSERT INTO PosConfigs (BranchId, PosName, DisplayOrder) VALUES
        (1, N'Sapo POS', 1),
        (1, N'KiotViet', 2);
END
GO

PRINT N'✅ Đã khởi tạo hoàn tất file Database.sql (v2.0 Tinh gọn) cho ShiftHandoverDB!';
GO

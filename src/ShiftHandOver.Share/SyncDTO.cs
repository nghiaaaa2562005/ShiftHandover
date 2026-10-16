using System;
using System.Collections.Generic;

namespace ShiftHandOver.Share
{
    /// <summary>
    /// Gói dữ liệu danh mục máy chủ cấp cho máy con (Bootstrapping / Cache offline)
    /// </summary>
    public class MasterDataBootstrapDTO
    {
        public BranchDTO? Branch { get; set; }
        public List<UserDTO> Users { get; set; } = new();
        public List<BranchBankSettingDTO> Banks { get; set; } = new();
        public List<PosConfigSettingDTO> PosConfigs { get; set; } = new();
        public List<ShiftTypeDTO> ShiftTypes { get; set; } = new();
        public ShiftHandoverDetailDTO? LastClosedShift { get; set; }
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Gói dữ liệu ca làm việc máy con đẩy lên máy chủ để đồng bộ
    /// </summary>
    public class SyncShiftUploadDTO
    {
        public string ClientShiftGuid { get; set; } = Guid.NewGuid().ToString();
        public int BranchId { get; set; }
        public DateTime ShiftDate { get; set; }
        public string ShiftType { get; set; } = "MORNING";
        public string Status { get; set; } = "Closed";
        public decimal CashOpening { get; set; }
        public decimal? CashClosing { get; set; }
        public decimal? CashDifference { get; set; }
        public int? OpenedByUserId { get; set; }
        public int? ClosedByUserId { get; set; }
        public DateTime? OpenedAt { get; set; }
        public DateTime? ClosedAt { get; set; }
        public string? Note { get; set; }
        public string? ActiveChannels { get; set; }

        public decimal Pos1Opening { get; set; }
        public decimal? Pos1Closing { get; set; }
        public decimal? Pos1Night { get; set; }

        public decimal Pos2Opening { get; set; }
        public decimal? Pos2Closing { get; set; }
        public decimal? Pos2Night { get; set; }

        public decimal Pos3Opening { get; set; }
        public decimal? Pos3Closing { get; set; }
        public decimal? Pos3Night { get; set; }

        public decimal Bank1Opening { get; set; }
        public decimal? Bank1Closing { get; set; }
        public decimal? Bank1Night { get; set; }

        public decimal Bank2Opening { get; set; }
        public decimal? Bank2Closing { get; set; }
        public decimal? Bank2Night { get; set; }

        public decimal Bank3Opening { get; set; }
        public decimal? Bank3Closing { get; set; }
        public decimal? Bank3Night { get; set; }

        public List<ShiftExpenseItemDTO> Expenses { get; set; } = new();
        public List<ShiftEmployeeSignDTO> Signatures { get; set; } = new();
    }

    /// <summary>
    /// Kết quả phản hồi đồng bộ ca từ máy chủ
    /// </summary>
    public class SyncShiftResponseDTO
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string ClientShiftGuid { get; set; } = string.Empty;
        public int ServerShiftId { get; set; }
    }
}

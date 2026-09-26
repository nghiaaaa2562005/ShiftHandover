using System;

namespace ShiftHandOver.Share
{
    public class ValidateShiftRequestDTO
    {
        public int BranchId { get; set; }
        public DateTime ShiftDate { get; set; }
        public string ShiftType { get; set; } = string.Empty; // MORNING | AFTERNOON | EVENING | NIGHT
    }

    public class ValidateShiftResponseDTO
    {
        public bool IsValid { get; set; }
        public bool IsReadOnly { get; set; } // true nếu là ca trong quá khứ hoặc ca đã chốt (chỉ xem)
        public string Message { get; set; } = string.Empty;
        public DateTime ServerTime { get; set; }
        public string ShiftStatus { get; set; } = "NONE"; // NONE | OPEN | CF | CF_NC | CLOSED
    }
}

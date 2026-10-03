namespace ShiftHandOver.Share
{
    public class BranchDTO
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal DefaultCashOpening { get; set; }
        public bool IsActive { get; set; }
    }

    public class BranchBankSettingDTO
    {
        public int Id { get; set; }
        public int BranchId { get; set; }
        public byte SlotIndex { get; set; }
        public string BankName { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }

    public class PosConfigSettingDTO
    {
        public int Id { get; set; }
        public int BranchId { get; set; }
        public string PosName { get; set; } = string.Empty;
        public byte DisplayOrder { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class UpdateCashOpeningDTO
    {
        public int BranchId { get; set; }
        public decimal DefaultCashOpening { get; set; }
    }

    public class ShiftScheduleConfigDTO
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string TimeRange { get; set; } = string.Empty;
        public int MaxEmployees { get; set; } = 2;
        public string Description { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }

    public class BranchHandoverConfigDTO
    {
        public int BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public decimal DefaultCashOpening { get; set; }
        public bool IsActive { get; set; } = true;
        public List<BranchBankSettingDTO> Banks { get; set; } = new();
        public List<PosConfigSettingDTO> PosConfigs { get; set; } = new();
    }
}


namespace ShiftHandOver.Share
{
    public class BranchDTO
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal DefaultCashOpening { get; set; }
        public bool IsActive { get; set; }
    }
}

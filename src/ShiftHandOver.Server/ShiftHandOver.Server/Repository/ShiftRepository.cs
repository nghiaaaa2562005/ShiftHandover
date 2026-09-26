using System.Collections.Generic;
using ShiftHandOver.Server.Models;
using ShiftHandOver.Share;

namespace ShiftHandOver.Server.Repository
{
    public class ShiftRepository : IShiftRepository
    {
        private readonly ShiftHandoverDbContext _context;

        public ShiftRepository(ShiftHandoverDbContext context)
        {
            _context = context;
        }

        public List<ShiftTypeDTO> GetShiftTypes()
        {
            return new List<ShiftTypeDTO>
            {
                new ShiftTypeDTO { Code = "MORNING",   Name = "Ca Sáng" },
                new ShiftTypeDTO { Code = "AFTERNOON", Name = "Ca Chiều" },
                new ShiftTypeDTO { Code = "EVENING",   Name = "Ca Tối" },
                new ShiftTypeDTO { Code = "NIGHT",     Name = "Ca Đêm" }
            };
        }
    }
}

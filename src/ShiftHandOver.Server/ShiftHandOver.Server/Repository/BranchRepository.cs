using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShiftHandOver.Server.Models;
using ShiftHandOver.Share;

namespace ShiftHandOver.Server.Repository
{
    public class BranchRepository : IBranchRepository
    {
        private readonly ShiftHandoverDbContext _context;

        public BranchRepository(ShiftHandoverDbContext context)
        {
            _context = context;
        }

        public async Task<List<BranchDTO>> GetActiveBranchesAsync()
        {
            return await _context.Branches
                .Where(b => b.IsActive)
                .Select(b => new BranchDTO
                {
                    Id = b.Id,
                    Name = b.Name,
                    DefaultCashOpening = b.DefaultCashOpening,
                    IsActive = b.IsActive
                })
                .ToListAsync();
        }
    }
}

using System.Collections.Generic;
using ShiftHandOver.Share;

namespace ShiftHandOver.Server.Repository
{
    public interface IShiftRepository
    {
        List<ShiftTypeDTO> GetShiftTypes();
    }
}

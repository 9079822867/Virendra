using System.Collections.Generic;
using VIRENDRA.Models;

namespace VIRENDRA.Data
{
    public interface ICommonRoutingRepository
    {
        List<CommonRouting> GetAll();
        CommonRouting GetById(int id);
        int  Create(CommonRouting model);
        void Update(CommonRouting model);
        void Delete(int id);
        void ToggleActive(int id, bool isActive);
    }
}

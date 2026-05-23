using System.Collections.Generic;
using VIRENDRA.Models;

namespace VIRENDRA.Data
{
    public interface IVendorRepository
    {
        List<ApiSource> GetAllVendors();
        ApiSource GetVendorById(int id);
        int CreateVendor(ApiSource vendor);
        void UpdateVendor(ApiSource vendor);
        void DeleteVendor(int id);
        void ToggleActive(int id, bool isActive);
        List<ApiUrl> GetVendorUrls(int apiSourceId);
        void SaveVendorUrls(int apiSourceId, List<ApiUrl> urls);
    }
}

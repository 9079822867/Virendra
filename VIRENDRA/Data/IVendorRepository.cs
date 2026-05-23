using System.Collections.Generic;
using VIRENDRA.Models;

namespace VIRENDRA.Data
{
    public interface IVendorRepository
    {
        List<Vendor> GetAllVendors();
        Vendor GetVendorById(int id);
        int CreateVendor(Vendor vendor);
        void UpdateVendor(Vendor vendor);
        void DeleteVendor(int id);
        void ToggleActive(int id, bool isActive);
        List<VendorUrl> GetVendorUrls(int vendorId);
        void SaveVendorUrls(int vendorId, List<VendorUrl> urls);
    }
}

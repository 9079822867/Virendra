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
        ResponseSettingViewModel GetResponseSetting(int urlId);
        void SaveTagValues(int urlId, int apiId, List<TagValueRow> rows, int userId);
        string GetCallbackId(int apiSourceId);
        void SetCallbackId(int apiSourceId, string callbackId);

        // ── Operator-code margin (vendor op-codes + commission) ──────────────
        /// <summary>
        /// Build the operator-code + commission grid for a vendor (ApiSource).
        /// Lists every active operator, left-joined with its OperatorCode row
        /// and the vendor package's PackageComm row.
        /// </summary>
        VendorMarginViewModel GetVendorMargins(int apiId);

        /// <summary>
        /// Persist the grid: ensure a vendor package exists (named "{ApiName}-{ApiId}"),
        /// link it via ApiSource.ApiPackid, then upsert OperatorCode (when OpCode set)
        /// and PackageComm (when CommAmt &gt; 0) for each row.
        /// </summary>
        void SaveVendorMargins(int apiId, int userId, List<OperatorMarginDto> rows);
    }
}

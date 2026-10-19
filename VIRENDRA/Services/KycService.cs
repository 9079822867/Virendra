using System;
using System.IO;
using System.Linq;
using System.Web.Hosting;
using VIRENDRA.Data;
using VIRENDRA.Models;

namespace VIRENDRA.Services
{
    /// <summary>
    /// KYC document upload / file access, shared by the web panel (KycController) and the
    /// mobile app API (MobileAppController). Files live in ~/App_Data/Kyc/{userId}/{random}.ext,
    /// which IIS never serves; callers stream them after checking who is asking.
    /// </summary>
    public class KycService
    {
        private readonly KycRepository _repo;

        public KycService(KycRepository repo)
        {
            _repo = repo;
        }

        /// <summary>Validates and stores one document. Returns null on success, else the error message.</summary>
        public string Upload(int userId, string category, string originalFileName, byte[] data)
        {
            if (!KycConstants.IsValidCategory(category))
                return "Invalid document type.";

            string title = KycConstants.CategoryTitle(category);
            var existing = _repo.GetDocument(userId, category);
            if (existing != null && existing.StatusId == KycConstants.Approved)
                return title + " is already approved and cannot be changed.";

            if (data == null || data.Length == 0)
                return "Please choose a file for " + title + ".";
            if (data.Length > KycConstants.MaxFileBytes)
                return title + ": file must be 2 MB or smaller.";

            string ext = Path.GetExtension(originalFileName ?? "").ToLowerInvariant();
            if (!KycConstants.AllowedExtensions.Contains(ext))
                return title + ": only JPG, JPEG, PNG or PDF files are allowed.";
            if (!HasValidSignature(data, ext))
                return title + ": the file content does not match its type.";

            try
            {
                string dir = UserFolder(userId);
                Directory.CreateDirectory(dir);

                string fileName = Guid.NewGuid().ToString("N") + ext;   // max 37 chars, fits VARCHAR(50)
                File.WriteAllBytes(Path.Combine(dir, fileName), data);

                _repo.SaveUpload(userId, category, fileName);

                if (existing != null) TryDelete(Path.Combine(dir, existing.ImgPath));
            }
            catch (Exception)
            {
                return title + " could not be uploaded. Please try again.";
            }
            return null;
        }

        /// <summary>Full path of the user's stored document, or null when there is none.</summary>
        public string GetFilePath(int userId, string category)
        {
            if (!KycConstants.IsValidCategory(category)) return null;

            var doc = _repo.GetDocument(userId, category);
            if (doc == null || string.IsNullOrEmpty(doc.ImgPath) || doc.ImgPath.IndexOfAny(new[] { '/', '\\' }) >= 0)
                return null;

            string path = Path.Combine(UserFolder(userId), doc.ImgPath);
            return File.Exists(path) ? path : null;
        }

        public static string MimeType(string path)
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".png": return "image/png";
                case ".pdf": return "application/pdf";
                default:     return "image/jpeg";
            }
        }

        private static string UserFolder(int userId) =>
            Path.Combine(HostingEnvironment.MapPath("~/App_Data/Kyc"), userId.ToString());

        /// <summary>Checks magic bytes so a renamed file can't pass as an image.</summary>
        private static bool HasValidSignature(byte[] d, string ext)
        {
            if (d.Length < 4) return false;
            switch (ext)
            {
                case ".jpg":
                case ".jpeg": return d[0] == 0xFF && d[1] == 0xD8 && d[2] == 0xFF;
                case ".png":  return d[0] == 0x89 && d[1] == 0x50 && d[2] == 0x4E && d[3] == 0x47;
                case ".pdf":  return d[0] == 0x25 && d[1] == 0x50 && d[2] == 0x44 && d[3] == 0x46;
                default:      return false;
            }
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { /* old file stays on disk; harmless */ }
        }
    }
}

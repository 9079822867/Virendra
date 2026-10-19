using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using VIRENDRA.Data;
using VIRENDRA.Models;

namespace VIRENDRA.Services
{
    public class KycIdResult
    {
        public bool   Ok      { get; set; }
        public string Message { get; set; }

        public static KycIdResult Fail(string m)    => new KycIdResult { Ok = false, Message = m };
        public static KycIdResult Success(string m) => new KycIdResult { Ok = true,  Message = m };
    }

    /// <summary>
    /// Aadhaar / PAN number submit and verification, shared by the web panel and the mobile app API.
    ///   Aadhaar: format + Verhoeff checksum, then Pending until an admin verifies it (Manual).
    ///   PAN    : verified instantly by the KYC API when configured (API); otherwise, or when the
    ///            API is unreachable, Pending until an admin verifies it (Manual).
    /// Admins can also enter / correct both numbers themselves (verified, Manual).
    /// </summary>
    public class KycIdService
    {
        private static readonly Regex PanPattern = new Regex("^[A-Z]{5}[0-9]{4}[A-Z]$");
        private static readonly string[] DobFormats = { "yyyy-MM-dd", "dd/MM/yyyy", "dd-MM-yyyy", "d/M/yyyy", "d-M-yyyy" };

        private readonly KycRepository _repo;

        public KycIdService(KycRepository repo)
        {
            _repo = repo;
        }

        public static bool ApiEnabled => SandboxKycApi.IsEnabled;

        // ── User submit ─────────────────────────────────────────────────────

        public KycIdResult SubmitAadhaar(int userId, string raw)
        {
            string aadhaar = NormalizeAadhaar(raw);
            string error = ValidateAadhaar(aadhaar);
            if (error != null) return KycIdResult.Fail(error);

            var detail = _repo.GetDetail(userId);
            if (detail?.AadhaarStatusId == KycConstants.Approved)
                return KycIdResult.Fail("Aadhaar number is already verified and cannot be changed.");
            if (_repo.AadhaarInUse(aadhaar, userId))
                return KycIdResult.Fail("This Aadhaar number is already registered with another account.");

            _repo.SaveAadhaar(userId, aadhaar, KycConstants.Pending, null, null, null);
            return KycIdResult.Success("Aadhaar number submitted. It will be verified by the admin.");
        }

        public async Task<KycIdResult> SubmitPanAsync(int userId, string rawPan, string name, string rawDob, bool consent)
        {
            string pan = NormalizePan(rawPan);
            name = CleanName(name);
            DateTime? dob = ParseDob(rawDob);

            string error = ValidatePan(pan) ?? ValidatePanName(name) ?? ValidateDob(dob);
            if (error != null) return KycIdResult.Fail(error);
            if (!consent) return KycIdResult.Fail("Please give consent to verify your PAN.");

            var detail = _repo.GetDetail(userId);
            if (detail?.PanStatusId == KycConstants.Approved)
                return KycIdResult.Fail("PAN is already verified and cannot be changed.");
            if (_repo.PanInUse(pan, userId))
                return KycIdResult.Fail("This PAN is already registered with another account.");

            if (ApiEnabled)
            {
                var r = await new SandboxKycApi().VerifyPanAsync(pan, name, dob.Value).ConfigureAwait(false);
                if (r.Completed && r.Valid)
                {
                    _repo.SavePan(userId, pan, name, dob, KycConstants.Approved, KycConstants.ModeApi, null, null);
                    return KycIdResult.Success("PAN verified successfully.");
                }
                if (r.Completed)
                {
                    _repo.SavePan(userId, pan, name, dob, KycConstants.Rejected, KycConstants.ModeApi, Trim250(r.Message), null);
                    return KycIdResult.Fail("PAN verification failed: " + r.Message + ". Please check the details and try again.");
                }
                // API unreachable: keep it for the admin
            }

            _repo.SavePan(userId, pan, name, dob, KycConstants.Pending, null, null, null);
            return KycIdResult.Success("PAN submitted. It will be verified by the admin.");
        }

        // ── Admin manual update ─────────────────────────────────────────────

        /// <summary>Admin enters / corrects numbers; each one given is saved as Verified (Manual).</summary>
        public KycIdResult AdminUpdate(int userId, string rawAadhaar, string rawPan, string panName, string rawDob, int adminId)
        {
            string aadhaar = NormalizeAadhaar(rawAadhaar);
            string pan = NormalizePan(rawPan);
            if (string.IsNullOrEmpty(aadhaar) && string.IsNullOrEmpty(pan))
                return KycIdResult.Fail("Enter an Aadhaar number and/or PAN.");

            if (!string.IsNullOrEmpty(aadhaar))
            {
                string e = ValidateAadhaar(aadhaar);
                if (e != null) return KycIdResult.Fail(e);
                if (_repo.AadhaarInUse(aadhaar, userId))
                    return KycIdResult.Fail("This Aadhaar number is already registered with another account.");
            }

            var current = _repo.GetDetail(userId);
            DateTime? dob = ParseDob(rawDob) ?? (string.IsNullOrWhiteSpace(rawDob) ? current?.PanDob : null);
            string name = CleanName(panName) ?? current?.PanName;
            if (!string.IsNullOrEmpty(pan))
            {
                string e = ValidatePan(pan);
                if (e != null) return KycIdResult.Fail(e);
                if (_repo.PanInUse(pan, userId))
                    return KycIdResult.Fail("This PAN is already registered with another account.");
                if (!string.IsNullOrWhiteSpace(rawDob) && dob == null)
                    return KycIdResult.Fail("Date of birth is not valid.");
            }

            if (!string.IsNullOrEmpty(aadhaar))
                _repo.SaveAadhaar(userId, aadhaar, KycConstants.Approved, KycConstants.ModeManual, null, adminId);
            if (!string.IsNullOrEmpty(pan))
                _repo.SavePan(userId, pan, name, dob, KycConstants.Approved, KycConstants.ModeManual, null, adminId);

            return KycIdResult.Success("Identity numbers updated and marked verified.");
        }

        // ── Validation ──────────────────────────────────────────────────────

        public static string NormalizeAadhaar(string s) =>
            new string((s ?? "").Where(char.IsDigit).ToArray());

        public static string NormalizePan(string s) =>
            (s ?? "").Replace(" ", "").Trim().ToUpperInvariant();

        public static string ValidateAadhaar(string a)
        {
            if (a.Length != 12) return "Aadhaar number must be 12 digits.";
            if (a[0] == '0' || a[0] == '1') return "Aadhaar number is not valid.";
            if (!VerhoeffValid(a)) return "Aadhaar number is not valid (checksum failed).";
            return null;
        }

        public static string ValidatePan(string p) =>
            PanPattern.IsMatch(p ?? "") ? null : "PAN must be in the format ABCDE1234F.";

        private static string ValidatePanName(string n) =>
            n != null && n.Length >= 2 && n.Length <= 100 ? null : "Enter your name exactly as printed on the PAN card.";

        private static string ValidateDob(DateTime? d)
        {
            if (d == null) return "Enter a valid date of birth.";
            if (d.Value > DateTime.Today.AddYears(-18)) return "You must be at least 18 years old.";
            if (d.Value < new DateTime(1900, 1, 1)) return "Enter a valid date of birth.";
            return null;
        }

        public static DateTime? ParseDob(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            return DateTime.TryParseExact(s.Trim(), DobFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                ? d.Date : (DateTime?)null;
        }

        private static string CleanName(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            return Regex.Replace(s.Trim(), @"\s+", " ");
        }

        private static string Trim250(string s) => s == null || s.Length <= 250 ? s : s.Substring(0, 250);

        // Verhoeff checksum (last Aadhaar digit)
        private static readonly int[,] _d =
        {
            {0,1,2,3,4,5,6,7,8,9},{1,2,3,4,0,6,7,8,9,5},{2,3,4,0,1,7,8,9,5,6},{3,4,0,1,2,8,9,5,6,7},
            {4,0,1,2,3,9,5,6,7,8},{5,9,8,7,6,0,4,3,2,1},{6,5,9,8,7,1,0,4,3,2},{7,6,5,9,8,2,1,0,4,3},
            {8,7,6,5,9,3,2,1,0,4},{9,8,7,6,5,4,3,2,1,0}
        };
        private static readonly int[,] _p =
        {
            {0,1,2,3,4,5,6,7,8,9},{1,5,7,6,2,8,3,0,9,4},{5,8,0,3,7,9,6,1,4,2},{8,9,1,6,0,4,3,5,2,7},
            {9,4,5,3,1,2,6,8,7,0},{4,2,8,6,5,7,3,9,0,1},{2,7,9,3,8,0,6,4,1,5},{7,0,4,6,9,1,3,2,5,8}
        };

        private static bool VerhoeffValid(string num)
        {
            int c = 0;
            for (int i = 0; i < num.Length; i++)
                c = _d[c, _p[i % 8, num[num.Length - 1 - i] - '0']];
            return c == 0;
        }
    }
}

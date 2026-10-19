using System;
using System.Configuration;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace VIRENDRA.Services
{
    /// <summary>Outcome of a PAN check by the KYC API.</summary>
    public class PanVerifyResult
    {
        /// <summary>False when the API could not be reached / returned an error (fall back to manual review).</summary>
        public bool   Completed { get; set; }
        public bool   Valid     { get; set; }   // PAN exists, name and DOB match
        public string Message   { get; set; }
    }

    /// <summary>
    /// PAN verification through Sandbox.co.in (https://developer.sandbox.co.in).
    /// Free test environment: https://test-api.sandbox.co.in (test keys); production: https://api.sandbox.co.in (paid).
    ///
    /// Web.config appSettings (API is off — manual verification only — while ApiKey / ApiSecret are blank):
    ///   KycApi:Provider  = Sandbox
    ///   KycApi:BaseUrl   = https://test-api.sandbox.co.in
    ///   KycApi:ApiKey    = key_test_...
    ///   KycApi:ApiSecret = secret_test_...
    ///
    /// Aadhaar: Sandbox's Aadhaar OTP (offline e-KYC) API is discontinued by UIDAI, so Aadhaar numbers
    /// are checked locally (format + Verhoeff checksum) and verified by an admin.
    /// </summary>
    public class SandboxKycApi
    {
        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        private static readonly SemaphoreSlim _tokenLock = new SemaphoreSlim(1, 1);
        private static string   _token;
        private static DateTime _tokenExpiry;

        private readonly string _baseUrl;
        private readonly string _apiKey;
        private readonly string _apiSecret;

        public SandboxKycApi()
        {
            _baseUrl   = (ConfigurationManager.AppSettings["KycApi:BaseUrl"] ?? "https://test-api.sandbox.co.in").TrimEnd('/');
            _apiKey    = ConfigurationManager.AppSettings["KycApi:ApiKey"];
            _apiSecret = ConfigurationManager.AppSettings["KycApi:ApiSecret"];
        }

        public static bool IsEnabled =>
            string.Equals(ConfigurationManager.AppSettings["KycApi:Provider"], "Sandbox", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(ConfigurationManager.AppSettings["KycApi:ApiKey"])
            && !string.IsNullOrWhiteSpace(ConfigurationManager.AppSettings["KycApi:ApiSecret"]);

        public async Task<PanVerifyResult> VerifyPanAsync(string pan, string nameAsPerPan, DateTime dob)
        {
            try
            {
                string token = await GetTokenAsync().ConfigureAwait(false);

                var body = new JObject
                {
                    ["@entity"]         = "in.co.sandbox.kyc.pan_verification.request",
                    ["pan"]             = pan,
                    ["name_as_per_pan"] = nameAsPerPan,
                    ["date_of_birth"]   = dob.ToString("dd/MM/yyyy"),
                    ["consent"]         = "Y",
                    ["reason"]          = "KYC verification for portal onboarding"
                };

                var req = new HttpRequestMessage(HttpMethod.Post, _baseUrl + "/kyc/pan/verify")
                {
                    Content = new StringContent(body.ToString(Formatting.None), Encoding.UTF8, "application/json")
                };
                req.Headers.TryAddWithoutValidation("authorization", token);   // not a Bearer token
                req.Headers.TryAddWithoutValidation("x-api-key", _apiKey);

                using (var resp = await _http.SendAsync(req).ConfigureAwait(false))
                {
                    string text = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                    JObject json = TryParse(text);

                    if ((int)resp.StatusCode == 422)   // invalid PAN pattern: definite answer
                        return new PanVerifyResult { Completed = true, Valid = false, Message = Msg(json, "Invalid PAN") };

                    if (!resp.IsSuccessStatusCode || json?["data"] == null)
                        return new PanVerifyResult { Completed = false, Message = Msg(json, "KYC API unavailable") };

                    var data = json["data"];
                    string status = (string)data["status"];
                    bool nameMatch = data["name_as_per_pan_match"]?.Type == JTokenType.Boolean && (bool)data["name_as_per_pan_match"];
                    bool dobMatch  = data["date_of_birth_match"]?.Type == JTokenType.Boolean && (bool)data["date_of_birth_match"];

                    if (!string.Equals(status, "valid", StringComparison.OrdinalIgnoreCase))
                        return new PanVerifyResult { Completed = true, Valid = false,
                            Message = "PAN is not valid" + ((string)data["remarks"] is string r && r.Length > 0 ? " (" + r + ")" : "") };
                    if (!nameMatch)
                        return new PanVerifyResult { Completed = true, Valid = false, Message = "Name does not match PAN records" };
                    if (!dobMatch)
                        return new PanVerifyResult { Completed = true, Valid = false, Message = "Date of birth does not match PAN records" };

                    return new PanVerifyResult { Completed = true, Valid = true, Message = "PAN verified" };
                }
            }
            catch (Exception)
            {
                return new PanVerifyResult { Completed = false, Message = "KYC API unavailable" };
            }
        }

        /// <summary>Access token (valid 24 h), cached and renewed after 23 h.</summary>
        private async Task<string> GetTokenAsync()
        {
            if (_token != null && DateTime.UtcNow < _tokenExpiry) return _token;

            await _tokenLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_token != null && DateTime.UtcNow < _tokenExpiry) return _token;

                var req = new HttpRequestMessage(HttpMethod.Post, _baseUrl + "/authenticate");
                req.Headers.TryAddWithoutValidation("x-api-key", _apiKey);
                req.Headers.TryAddWithoutValidation("x-api-secret", _apiSecret);

                using (var resp = await _http.SendAsync(req).ConfigureAwait(false))
                {
                    string text = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                    string token = (string)TryParse(text)?["data"]?["access_token"];
                    if (!resp.IsSuccessStatusCode || string.IsNullOrEmpty(token))
                        throw new InvalidOperationException("KYC API authentication failed");

                    _token       = token;
                    _tokenExpiry = DateTime.UtcNow.AddHours(23);
                    return _token;
                }
            }
            finally
            {
                _tokenLock.Release();
            }
        }

        private static JObject TryParse(string text)
        {
            try { return JObject.Parse(text); }
            catch { return null; }
        }

        private static string Msg(JObject json, string fallback) =>
            (string)json?["message"] ?? (string)json?["data"]?["message"] ?? fallback;
    }
}

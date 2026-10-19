using System;
using System.Configuration;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using XmlNodeType = System.Xml.XmlNodeType;
using XmlReader = System.Xml.XmlReader;
using XmlReaderSettings = System.Xml.XmlReaderSettings;
using DtdProcessing = System.Xml.DtdProcessing;
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

    /// <summary>A DigiLocker consent session: the user is sent to Url, then back to the redirect URL.</summary>
    public class DigiLockerSession
    {
        public bool   Completed { get; set; }   // false: API error (Message says why)
        public string SessionId { get; set; }
        public string Url       { get; set; }
        public string Message   { get; set; }
    }

    public enum DigiLockerState { Waiting, Failed, Fetched }

    /// <summary>
    /// The Aadhaar shared through DigiLocker. Only the masked number (last 4 digits visible) and the
    /// name are kept; the photo, address and the signed XML itself are discarded.
    /// </summary>
    public class DigiLockerAadhaarResult
    {
        public DigiLockerState State { get; set; }
        public string MaskedUid { get; set; }   // e.g. "xxxxxxxx1234"
        public string Name      { get; set; }
        public string Message   { get; set; }

        public string Last4 =>
            MaskedUid != null && MaskedUid.Length >= 4 ? MaskedUid.Substring(MaskedUid.Length - 4) : null;
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
    /// Aadhaar: Sandbox's Aadhaar OTP (offline e-KYC) API is discontinued by UIDAI. Instead the user shares
    /// their Aadhaar from DigiLocker (Aadhaar OTP login + consent on DigiLocker's own site):
    ///   POST /kyc/digilocker/sessions/init                    → authorization_url + session_id
    ///   GET  /kyc/digilocker/sessions/{id}/status             → created | succeeded | failed | expired
    ///   GET  /kyc/digilocker/sessions/{id}/documents/aadhaar  → pre-signed URL of the signed Aadhaar XML
    /// The XML carries only a masked number (last 4 digits), matched against the number the user entered.
    /// Without the API (or when DigiLocker fails) Aadhaar is verified by an admin as before.
    ///   KycApi:ReturnBaseUrl = https://your-portal-domain  (optional; public HTTPS base DigiLocker returns
    ///                          to, defaults to the current request's host, which must then be HTTPS)
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

        // ── DigiLocker (Aadhaar) ────────────────────────────────────────────

        /// <summary>Creates a DigiLocker session asking for the user's Aadhaar; redirectUrl must be public HTTPS.</summary>
        public async Task<DigiLockerSession> CreateDigiLockerSessionAsync(string redirectUrl)
        {
            try
            {
                var body = new JObject
                {
                    ["@entity"]      = "in.co.sandbox.kyc.digilocker.session.request",
                    ["flow"]         = "signin",
                    ["redirect_url"] = redirectUrl,
                    ["doc_types"]    = new JArray("aadhaar"),
                    ["purpose"]      = "Know Your Customer",
                    ["options"]      = new JObject { ["pinless"] = true }
                };

                JObject json = await SendAsync(HttpMethod.Post, "/kyc/digilocker/sessions/init", body).ConfigureAwait(false);
                string id  = (string)json?["data"]?["session_id"];
                string url = (string)json?["data"]?["authorization_url"];
                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(url))
                    return new DigiLockerSession { Completed = false, Message = Msg(json, "DigiLocker is unavailable") };

                return new DigiLockerSession { Completed = true, SessionId = id, Url = url };
            }
            catch (Exception)
            {
                return new DigiLockerSession { Completed = false, Message = "DigiLocker is unavailable" };
            }
        }

        /// <summary>
        /// Session status, then (once the user consented) the Aadhaar XML, reading only UidData/@uid (masked)
        /// and Poi/@name. Waiting while the user has not finished on DigiLocker.
        /// </summary>
        public async Task<DigiLockerAadhaarResult> FetchDigiLockerAadhaarAsync(string sessionId)
        {
            try
            {
                string sid = Uri.EscapeDataString(sessionId);
                JObject st = await SendAsync(HttpMethod.Get, "/kyc/digilocker/sessions/" + sid + "/status", null).ConfigureAwait(false);
                string status = (string)st?["data"]?["status"];

                if (string.Equals(status, "created", StringComparison.OrdinalIgnoreCase))
                    return new DigiLockerAadhaarResult { State = DigiLockerState.Waiting, Message = "DigiLocker consent is not completed yet." };
                if (!string.Equals(status, "succeeded", StringComparison.OrdinalIgnoreCase))
                    return new DigiLockerAadhaarResult
                    {
                        State   = DigiLockerState.Failed,
                        Message = status == null ? Msg(st, "DigiLocker is unavailable") : "DigiLocker session " + status
                    };

                JObject doc = await SendAsync(HttpMethod.Get, "/kyc/digilocker/sessions/" + sid + "/documents/aadhaar", null).ConfigureAwait(false);
                string fileUrl = null;
                if (doc?["data"]?["files"] is JArray files)
                    foreach (var f in files)
                    {
                        string u = (string)f["url"];
                        if (u == null) continue;
                        if (fileUrl == null || string.Equals((string)f["metadata"]?["ContentType"], "application/xml", StringComparison.OrdinalIgnoreCase))
                            fileUrl = u;
                    }
                if (fileUrl == null)
                    return new DigiLockerAadhaarResult { State = DigiLockerState.Failed, Message = Msg(doc, "Aadhaar was not shared from DigiLocker") };

                // Pre-signed download URL: no API headers. Parsed in memory, never stored.
                using (var resp = await _http.GetAsync(fileUrl).ConfigureAwait(false))
                {
                    if (!resp.IsSuccessStatusCode)
                        return new DigiLockerAadhaarResult { State = DigiLockerState.Failed, Message = "Could not download Aadhaar from DigiLocker" };
                    return ReadAadhaarXml(await resp.Content.ReadAsStringAsync().ConfigureAwait(false));
                }
            }
            catch (Exception)
            {
                return new DigiLockerAadhaarResult { State = DigiLockerState.Failed, Message = "DigiLocker is unavailable" };
            }
        }

        private static DigiLockerAadhaarResult ReadAadhaarXml(string xml)
        {
            string uid = null, name = null;
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, IgnoreComments = true };
            using (var reader = XmlReader.Create(new StringReader(xml), settings))
            {
                while (reader.Read())
                {
                    if (reader.NodeType != XmlNodeType.Element) continue;
                    if (reader.LocalName == "UidData") uid = reader.GetAttribute("uid");
                    else if (reader.LocalName == "Poi") name = reader.GetAttribute("name");
                    if (uid != null && name != null) break;
                }
            }

            uid = uid?.Trim();
            if (string.IsNullOrEmpty(uid) || uid.Length < 4)
                return new DigiLockerAadhaarResult { State = DigiLockerState.Failed, Message = "Aadhaar number not found in the DigiLocker document" };

            return new DigiLockerAadhaarResult
            {
                State     = DigiLockerState.Fetched,
                MaskedUid = uid,
                Name      = string.IsNullOrWhiteSpace(name) ? null : name.Trim(),
                Message   = "Aadhaar fetched from DigiLocker"
            };
        }

        /// <summary>Authorized JSON call; the parsed body (error bodies too) or null.</summary>
        private async Task<JObject> SendAsync(HttpMethod method, string path, JObject body)
        {
            string token = await GetTokenAsync().ConfigureAwait(false);
            var req = new HttpRequestMessage(method, _baseUrl + path);
            if (body != null)
                req.Content = new StringContent(body.ToString(Formatting.None), Encoding.UTF8, "application/json");
            req.Headers.TryAddWithoutValidation("authorization", token);   // not a Bearer token
            req.Headers.TryAddWithoutValidation("x-api-key", _apiKey);

            using (var resp = await _http.SendAsync(req).ConfigureAwait(false))
                return TryParse(await resp.Content.ReadAsStringAsync().ConfigureAwait(false));
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

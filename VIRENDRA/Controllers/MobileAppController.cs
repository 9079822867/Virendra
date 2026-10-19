using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Web.Http;
using Newtonsoft.Json;
using VIRENDRA.Data;
using VIRENDRA.Models;

namespace VIRENDRA.Controllers
{
    /// <summary>
    /// Mobile app API (same routes / response shape as the STC app):
    ///   POST App/webservice/Login | ChangePass | ResendOTP | ForgetPass
    ///        App/webservice/RechargeCall | Operator | Commission | OperatorSlabCommission
    ///        App/webservice/BalanceCheck | DashboardHome | RechargeReport | TransactionReport | BookComplain
    /// Login returns TOKENID (stored in User.AppToken); every other call sends it as Tokenid.
    ///
    /// ERROR codes: 0 OK, 1 invalid username, 2 not found / inactive, 3 locked,
    ///              4 invalid password / OTP, 5 unexpected, 9 authentication, 11 bad parameters.
    /// </summary>
    [RoutePrefix("App/webservice")]
    public class MobileAppController : ApiController
    {
        private const int  MAX_RETRY_COUNT    = 5;      // same as AuthController
        private const int  OTP_VALID_MINUTES  = 10;

        // App contract uses exact property names (ERROR, STATUSCODE…), not the API's camelCase
        private static readonly JsonSerializerSettings _json = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            DateFormatString  = "yyyy-MM-dd HH:mm:ss"
        };

        private readonly UserRepository        _userRepo = new UserRepository();
        private readonly TransactionRepository _txnRepo  = new TransactionRepository();
        private readonly PackageRepository     _pkgRepo  = new PackageRepository();
        private readonly MobileAppRepository   _appRepo  = new MobileAppRepository();
        private readonly Services.RechargeService _rechargeSvc =
            new Services.RechargeService(new RechargeRepository());
        private readonly Services.ComplaintService _complaintSvc =
            new Services.ComplaintService(new ComplaintRepository(), new RechargeRepository());

        #region Login

        [HttpPost]
        [Route("Login")]
        public IHttpActionResult Login([FromBody] AppLoginRequest ld)
        {
            if (ld == null || string.IsNullOrWhiteSpace(ld.Name) || string.IsNullOrWhiteSpace(ld.Pass))
                return Reply(LoginFail("11", "Username and password are required"));

            try
            {
                User user = _userRepo.GetUserByUsername(ld.Name.Trim());

                if (user == null || user.IsDeleted)
                    return Reply(LoginFail("2", "Record Does not Exists"));
                if (user.IsLocked)
                    return Reply(LoginFail("3", "USER LOCKED. Please contact administrator."));
                if (!user.IsActive)
                    return Reply(LoginFail("2", "USER INACTIVE. Please contact administrator."));

                if (user.Password != ld.Pass)
                {
                    int retry = user.RetryCount + 1;
                    _userRepo.UpdateRetryCount(user.Id, retry);

                    string msg = retry >= MAX_RETRY_COUNT
                        ? "Your account has been locked due to multiple failed login attempts."
                        : "Invalid password. " + (MAX_RETRY_COUNT - retry) + " attempts remaining.";
                    return Reply(LoginFail("4", msg));
                }

                string token = Guid.NewGuid().ToString("N");
                _appRepo.SaveAppLogin(user.Id, token, ld.FireToken);

                return Reply(new AppUserLogResponse
                {
                    ERROR        = "0",
                    STATUSCODE   = "1",
                    MESSAGE      = "Login Success",
                    TOKENID      = token,
                    USERID       = user.Id.ToString(CultureInfo.InvariantCulture),
                    SHOPNAME     = user.Username,
                    MOBILENUMBER = user.Username,
                    EMAIL        = string.Empty,
                    ROLEID       = (user.RoleId ?? RoleConstants.Retailer).ToString(CultureInfo.InvariantCulture),
                    USERTYPE     = user.RoleName ?? string.Empty,
                    BALANCE      = (user.UserBal ?? 0m).ToString("0.00", CultureInfo.InvariantCulture),
                    OTPCheck     = "True"
                });
            }
            catch (Exception)
            {
                return Reply(LoginFail("5", "Unexpected error"));
            }
        }

        private static AppUserLogResponse LoginFail(string error, string message)
            => new AppUserLogResponse { ERROR = error, STATUSCODE = "2", OTPCheck = "True", MESSAGE = message };

        #endregion

        #region Change / Forgot Password

        [HttpPost]
        [Route("ChangePass")]
        public IHttpActionResult ChangePass([FromBody] AppChangePassRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.OldPassword) || string.IsNullOrWhiteSpace(req.Password))
                return Reply(Signup("11", "3", "Old and new password are required"));
            if (req.Password.Length < 4)
                return Reply(Signup("11", "3", "New password must be at least 4 characters"));

            try
            {
                User user = AuthUser(req.Tokenid);
                if (user == null)
                    return Reply(Signup("9", "3", "Authentication error!"));

                if (user.Password != req.OldPassword)
                    return Reply(Signup("3", "3", "Old password is incorrect"));
                if (req.OldPassword == req.Password)
                    return Reply(Signup("3", "3", "New password must be different from old password"));

                _appRepo.UpdatePassword(user.Id, req.Password);
                return Reply(Signup("0", "1", "Password Change has been successfully"));
            }
            catch (Exception)
            {
                return Reply(Signup("5", "3", "Unexpected error"));
            }
        }

        /// <summary>Step 1 of forgot password: generates an OTP valid for 10 minutes.</summary>
        [HttpPost]
        [Route("ResendOTP")]
        public IHttpActionResult ResendOTP([FromBody] AppResendOtpRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Mobileno))
                return Reply(Signup("11", "3", "Invalid Request Parameters"));

            return Reply(SendForgotOtp(req.Mobileno.Trim()));
        }

        /// <summary>
        /// Step 2 of forgot password: Mobileno + OTP + new Password.
        /// Called without OTP it behaves like ResendOTP (generates and sends the OTP).
        /// </summary>
        [HttpPost]
        [Route("ForgetPass")]
        [Route("ForgotPass")]
        public IHttpActionResult ForgetPass([FromBody] AppForgetPassRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Mobileno))
                return Reply(Signup("11", "3", "Invalid Request Parameters"));

            if (string.IsNullOrWhiteSpace(req.OTP))
                return Reply(SendForgotOtp(req.Mobileno.Trim()));

            if (string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 4)
                return Reply(Signup("11", "3", "New password must be at least 4 characters"));

            try
            {
                User user = _userRepo.GetUserByUsername(req.Mobileno.Trim());
                if (user == null || user.IsDeleted || !user.IsActive)
                    return Reply(Signup("4", "3", "Try Again !!"));

                if (string.IsNullOrEmpty(user.OTP) || !OtpMatches(user.OTP, req.OTP.Trim()))
                    return Reply(Signup("3", "3", "Sorry OTP is InValid !!"));

                if (!DateTime.TryParseExact(user.ResetCode, "yyyyMMddHHmmss", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out DateTime expiry) || expiry < DateTime.Now)
                    return Reply(Signup("2", "2", "Sorry your OTP has been expire"));

                // Also clears OTP / ResetCode / RetryCount (unlocks a retry-locked password)
                _appRepo.UpdatePassword(user.Id, req.Password);
                return Reply(Signup("0", "1", "Password has been successfully changed"));
            }
            catch (Exception)
            {
                return Reply(Signup("5", "3", "Unexpected error"));
            }
        }

        private AppSignupResponse SendForgotOtp(string username)
        {
            try
            {
                User user = _userRepo.GetUserByUsername(username);

                // Same reply whether or not the user exists, so usernames can't be probed
                var reply = Signup("0", "1", "OTP has been sent to your registered mobile number");
                reply.Time = (OTP_VALID_MINUTES * 60).ToString(CultureInfo.InvariantCulture);

                if (user == null || user.IsDeleted || !user.IsActive)
                    return reply;

                string otp = NewOtp();
                _appRepo.SaveOtp(user.Id, otp, DateTime.Now.AddMinutes(OTP_VALID_MINUTES));
                SendOtpSms(user, otp);

                return reply;
            }
            catch (Exception)
            {
                return Signup("5", "3", "Unexpected error");
            }
        }

        /// <summary>
        /// TODO: plug in the SMS gateway here. Until then the OTP is only stored in User.OTP
        /// (admin can read it from the database) and is never returned in the API response.
        /// </summary>
        private static void SendOtpSms(User user, string otp)
        {
        }

        private static string NewOtp()
        {
            using (var rng = RandomNumberGenerator.Create())
            {
                var bytes = new byte[4];
                rng.GetBytes(bytes);
                return (BitConverter.ToUInt32(bytes, 0) % 1000000).ToString("D6", CultureInfo.InvariantCulture);
            }
        }

        private static bool OtpMatches(string expected, string actual)
        {
            if (expected.Length != actual.Length) return false;
            int diff = 0;
            for (int i = 0; i < expected.Length; i++) diff |= expected[i] ^ actual[i];
            return diff == 0;
        }

        #endregion

        #region Commission

        [HttpPost]
        [Route("Commission")]
        [Route("CommissionList")]
        public IHttpActionResult Commission([FromBody] AppTokenRequest req)
        {
            var res = new AppCommissionResponse();
            try
            {
                User user = AuthUser(req?.Tokenid);
                if (user == null)
                {
                    res.ERROR = "9"; res.STATUSCODE = "2"; res.MESSAGE = "Authentication error!";
                    return Reply(res);
                }

                var rows = _pkgRepo.GetCommissionsByUser(user.Id)
                    .Select(c => new AppCommissionRow
                    {
                        OperatorId   = c.OperatorId,
                        OperatorName = c.OperatorName,
                        OperatorCode = c.OperatorCode,
                        PackageName  = c.PackageName,
                        Commission   = c.CommValue,
                        CommType     = c.CommType
                    }).ToList();

                res.ERROR      = "0";
                res.STATUSCODE = "1";
                res.MESSAGE    = rows.Count > 0 ? "SUCCESS" : "No Record Availble";
                res.Commissionlist = rows;
            }
            catch (Exception)
            {
                res.ERROR = "5"; res.STATUSCODE = "2"; res.MESSAGE = "Unexpected error";
            }
            return Reply(res);
        }

        #endregion

        #region Reports

        [HttpPost]
        [Route("RechargeReport")]
        public IHttpActionResult RechargeReport([FromBody] AppRechargeReportRequest req)
        {
            var res = new AppReportResponse<AppRechargeRow>();
            try
            {
                User user = AuthUser(req?.Tokenid);
                if (user == null)
                {
                    res.ERROR = "9"; res.STATUSCODE = "2"; res.MESSAGE = "Authentication error!";
                    return Reply(res);
                }

                DateTime from = ParseDate(req.Startdate) ?? DateTime.Today;
                DateTime to   = ParseDate(req.Enddate)   ?? DateTime.Today;
                if (to < from) { var t = from; from = to; to = t; }

                var rows = _txnRepo.GetRechargeHistory(user.Id, from, to, StatusFilter(req.status), req.MobileNo)
                    .Select(r => new AppRechargeRow
                    {
                        RecId         = r.RecId,
                        TxnId         = r.TxnId,
                        OurRefTxnId   = r.OurRefTxnId,
                        OperatorTxnId = r.ApiTxnId,
                        MobileNo      = r.Number,
                        Operator      = r.Operator,
                        Amount        = r.Amount,
                        Commission    = r.Commission,
                        Status        = r.Status,
                        StatusMsg     = r.StatusMsg,
                        Date          = r.Date
                    }).ToList();

                res.ERROR      = "0";
                res.STATUSCODE = "1";
                res.MESSAGE    = rows.Count > 0 ? "SUCCESS" : "No Record Availble";
                res.TOTAL      = rows.Count;
                res.REPORT     = rows;
            }
            catch (Exception)
            {
                res.ERROR = "5"; res.STATUSCODE = "2"; res.MESSAGE = "Unexpected error";
            }
            return Reply(res);
        }

        [HttpPost]
        [Route("TransactionReport")]
        [Route("TxnReport")]
        public IHttpActionResult TransactionReport([FromBody] AppTxnReportRequest req)
        {
            var res = new AppReportResponse<AppTxnRow>();
            try
            {
                User user = AuthUser(req?.Tokenid);
                if (user == null)
                {
                    res.ERROR = "9"; res.STATUSCODE = "2"; res.MESSAGE = "Authentication error!";
                    return Reply(res);
                }

                // Default: last 30 days
                DateTime to   = ParseDate(req.Enddate)   ?? DateTime.Today;
                DateTime from = ParseDate(req.Startdate) ?? to.AddDays(-30);
                if (to < from) { var t = from; from = to; to = t; }

                int pageIndex = req.PageIndex > 0 ? req.PageIndex : 1;
                int pageSize  = req.PageSize  > 0 ? Math.Min(req.PageSize, 200) : 20;

                var all = _txnRepo.GetTransactionHistory(user.Id, from, to, null, null);

                var rows = all
                    .Skip((pageIndex - 1) * pageSize)
                    .Take(pageSize)
                    .Select(t => new AppTxnRow
                    {
                        TxnId       = t.TxnId,
                        Type        = t.Type,
                        Description = t.Description,
                        Credit      = t.Credit,
                        Debit       = t.Debit,
                        Balance     = t.Balance,
                        Date        = t.Date
                    }).ToList();

                res.ERROR      = "0";
                res.STATUSCODE = "1";
                res.MESSAGE    = rows.Count > 0 ? "SUCCESS" : "No Record Availble";
                res.TOTAL      = all.Count;
                res.REPORT     = rows;
            }
            catch (Exception)
            {
                res.ERROR = "5"; res.STATUSCODE = "2"; res.MESSAGE = "Unexpected error";
            }
            return Reply(res);
        }

        #endregion

        #region Book Complain

        [HttpPost]
        [Route("BookComplain")]
        public IHttpActionResult BookComplain([FromBody] AppComplainRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.RecID))
                return Reply(Signup("11", "3", "Recharge id is required"));

            try
            {
                User user = AuthUser(req.Tokenid);
                if (user == null)
                    return Reply(Signup("9", "2", "Authentication error!"));

                var result = _complaintSvc.Book(req.RecID, req.Remark, user.Id, user.Id);
                return Reply(result.Status == ComplaintBookStatus.Booked
                    ? Signup("0", "1", result.Message)
                    : Signup("0", "3", result.Message));
            }
            catch (Exception)
            {
                return Reply(Signup("5", "2", "Unexpected error"));
            }
        }

        #endregion

        #region Recharge

        /// <summary>
        /// Same pipeline as the web Recharge page (RechargeService: validation SP → routing →
        /// usp_RechargeCreate → vendor API → usp_UpdateRechargeStatus, refund on failure).
        /// </summary>
        [HttpPost]
        [Route("RechargeCall")]
        [Route("Recharge")]
        public IHttpActionResult RechargeCall([FromBody] AppRechargeRequest req)
        {
            var res = new AppRechargeResponse { STATUS = "Recharge Failed", STATUSCODE = "3" };

            if (req == null || string.IsNullOrWhiteSpace(req.MobileNo) || string.IsNullOrWhiteSpace(req.Operator))
            {
                res.ERROR = "11"; res.MESSAGE = "Invalid Request Parameters";
                return Reply(res);
            }

            User user = AuthUser(req.TokenID);
            if (user == null)
            {
                res.ERROR = "9"; res.MESSAGE = "Authentication error!";
                return Reply(res);
            }

            // Transaction PIN: checked only when the user has one set
            if (!string.IsNullOrWhiteSpace(user.UserPin) &&
                !OtpMatches(user.UserPin.Trim(), (req.Pin ?? string.Empty).Trim()))
            {
                res.ERROR = "12"; res.MESSAGE = "Pin Invaild";
                return Reply(res);
            }

            string mobile = req.MobileNo.Trim();
            if (mobile.Length < 6 || mobile.Length > 15 || !mobile.All(char.IsDigit))
            {
                res.ERROR = "11"; res.MESSAGE = "Invalid Mobile No";
                return Reply(res);
            }

            if (!int.TryParse(req.Operator.Trim(), out int opId) || opId <= 0)
            {
                res.ERROR = "11"; res.MESSAGE = "Invalid Operator";
                return Reply(res);
            }

            if (!decimal.TryParse(req.Amount, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal amount) ||
                amount <= 0 || amount > 99999)
            {
                res.ERROR = "10"; res.MESSAGE = "Amount Invaild";
                return Reply(res);
            }

            int.TryParse(!string.IsNullOrWhiteSpace(req.CircleId) ? req.CircleId : req.CricleID, out int circleId);

            try
            {
                var result = _rechargeSvc.ProcessRecharge(new RechargeRequestModel
                {
                    OperatorId = opId,
                    CircleId   = circleId,
                    CustomerNo = mobile,
                    Amount     = amount,
                    RCTypeId   = 1,
                    UserTxnId  = string.IsNullOrWhiteSpace(req.RefTxnId) ? null : req.RefTxnId.Trim()
                }, user.Id, user.Id);

                res.ERROR         = "0";
                res.MESSAGE       = result.Message;
                res.RECID         = result.RecId > 0 ? result.RecId.ToString(CultureInfo.InvariantCulture) : null;
                res.TXNID         = result.UserTxnId;
                res.OPERATORTXNID = result.ApiTxnId;

                if (result.Success)        { res.STATUS = "Recharge SUCCESS";          res.STATUSCODE = "1"; }
                else if (result.IsPending) { res.STATUS = "Recharge Under Processing"; res.STATUSCODE = "2"; }
                else                       { res.STATUS = "Recharge FAILED";           res.STATUSCODE = "3"; }
            }
            catch (Exception)
            {
                // Debit may already exist — tell the app to check the report, not to retry blindly
                res.ERROR      = "5";
                res.STATUS     = "Status Not Availble, Please Check Recharge Report";
                res.STATUSCODE = "2";
                res.MESSAGE    = "Unexpected error";
            }

            try { res.WBalance = _appRepo.GetUserBalance(user.Id).ToString("0.00", CultureInfo.InvariantCulture); }
            catch { /* balance is informational */ }

            return Reply(res);
        }

        #endregion

        #region Operator List / Slab Commission

        [HttpPost]
        [Route("Operator")]
        [Route("OperatorList")]
        public IHttpActionResult OperatorList([FromBody] AppOperatorRequest req)
        {
            var res = new AppOperatorResponse();
            try
            {
                if (AuthUser(req?.Tokenid) == null)
                {
                    res.ERROR = "9"; res.STATUSCODE = "2"; res.MESSAGE = "Authentication error!";
                    return Reply(res);
                }

                int? opTypeId = int.TryParse(req.mode, out int t) && t > 0 ? t : (int?)null;
                var rows = _appRepo.GetOperators(opTypeId);

                res.ERROR      = "0";
                res.STATUSCODE = "1";
                res.MESSAGE    = rows.Count > 0 ? "SUCCESS" : "No Data Found";
                res.Operator   = rows;
            }
            catch (Exception)
            {
                res.ERROR = "5"; res.STATUSCODE = "2"; res.MESSAGE = "Unexpected error";
            }
            return Reply(res);
        }

        /// <summary>Commission of the user's package for one operator.</summary>
        [HttpPost]
        [Route("OperatorSlabCommission")]
        public IHttpActionResult OperatorSlabCommission([FromBody] AppSlabCommissionRequest req)
        {
            var res = new AppCommissionResponse();
            try
            {
                User user = AuthUser(req?.Tokenid);
                if (user == null)
                {
                    res.ERROR = "9"; res.STATUSCODE = "2"; res.MESSAGE = "Authentication error!";
                    return Reply(res);
                }
                if (!int.TryParse(req.Operator, out int opId) || opId <= 0)
                {
                    res.ERROR = "11"; res.STATUSCODE = "2"; res.MESSAGE = "Invalid Operator";
                    return Reply(res);
                }

                var rows = _pkgRepo.GetCommissionsByUser(user.Id)
                    .Where(c => c.OperatorId == opId)
                    .Select(c => new AppCommissionRow
                    {
                        OperatorId   = c.OperatorId,
                        OperatorName = c.OperatorName,
                        OperatorCode = c.OperatorCode,
                        PackageName  = c.PackageName,
                        Commission   = c.CommValue,
                        CommType     = c.CommType
                    }).ToList();

                res.ERROR      = "0";
                res.STATUSCODE = "1";
                res.MESSAGE    = rows.Count > 0 ? "SUCCESS" : "No Record Availble";
                res.Commissionlist = rows;
            }
            catch (Exception)
            {
                res.ERROR = "5"; res.STATUSCODE = "2"; res.MESSAGE = "Unexpected error";
            }
            return Reply(res);
        }

        #endregion

        #region Balance Check

        [HttpPost]
        [Route("BalanceCheck")]
        [Route("Balance")]
        public IHttpActionResult BalanceCheck([FromBody] AppTokenRequest req)
        {
            var res = new AppBalanceResponse();
            try
            {
                User user = AuthUser(req?.Tokenid);
                if (user == null)
                {
                    res.ERROR = "9"; res.STATUSCODE = "2"; res.MESSAGE = "Authentication error!";
                    return Reply(res);
                }

                var (bal, outBal) = _appRepo.GetUserBalances(user.Id);

                res.ERROR              = "0";
                res.STATUSCODE         = "1";
                res.MESSAGE            = "SUCCESS";
                res.WBalance           = bal.ToString("0.00", CultureInfo.InvariantCulture);
                res.OutStandingBalance = outBal.ToString("0.00", CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                res.ERROR = "5"; res.STATUSCODE = "2"; res.MESSAGE = "Unexpected error";
            }
            return Reply(res);
        }

        #endregion

        #region Dashboard

        [HttpPost]
        [Route("DashboardHome")]
        [Route("Dashboard")]
        public IHttpActionResult DashboardHome([FromBody] AppDashboardRequest req)
        {
            var res = new AppDashboardResponse();
            try
            {
                User user = AuthUser(req?.Tokenid);
                if (user == null)
                {
                    res.ERROR = "9"; res.STATUSCODE = "2"; res.MESSAGE = "Authentication error!";
                    return Reply(res);
                }

                // Today's figures (same source as the web dashboard)
                var stats = _txnRepo.GetDashboardStats(user.Id);

                // Last 10 recharges in the requested range (default: last 30 days)
                DateTime to   = ParseDate(req.ToDate)   ?? DateTime.Today;
                DateTime from = ParseDate(req.FormDate) ?? to.AddDays(-30);
                if (to < from) { var t = from; from = to; to = t; }

                var last = _txnRepo.GetRechargeHistory(user.Id, from, to, null, null)
                    .Take(10)
                    .Select(r => new AppRechargeRow
                    {
                        RecId         = r.RecId,
                        TxnId         = r.TxnId,
                        OurRefTxnId   = r.OurRefTxnId,
                        OperatorTxnId = r.ApiTxnId,
                        MobileNo      = r.Number,
                        Operator      = r.Operator,
                        Amount        = r.Amount,
                        Commission    = r.Commission,
                        Status        = r.Status,
                        StatusMsg     = r.StatusMsg,
                        Date          = r.Date
                    }).ToList();

                res.ERROR          = "0";
                res.STATUSCODE     = "1";
                res.MESSAGE        = "SUCCESS";
                res.Commission     = stats.TodayEarning.ToString("0.00", CultureInfo.InvariantCulture);
                res.OpeningBalance = stats.OpeningBalance.ToString("0.00", CultureInfo.InvariantCulture);
                res.ClosingBalance = stats.ClosingBalance.ToString("0.00", CultureInfo.InvariantCulture);
                res.TopUpAmount    = stats.CreditBalance.ToString("0.00", CultureInfo.InvariantCulture);
                res.Notification   = string.Empty;
                res.TodayData      = new AppDashboardToday
                {
                    SuccessAmount = stats.SuccessAmount,
                    FailedAmount  = stats.FailedAmount,
                    TotalCount    = stats.TotalTxnCount,
                    SuccessCount  = stats.SuccessCount,
                    FailedCount   = stats.FailedCount,
                    PendingCount  = stats.PendingCount
                };
                res.LastRecharge = last;
            }
            catch (Exception)
            {
                res.ERROR = "5"; res.STATUSCODE = "2"; res.MESSAGE = "Unexpected error";
            }
            return Reply(res);
        }

        #endregion

        #region Helpers

        /// <summary>Active, unlocked user owning this app token, or null.</summary>
        private User AuthUser(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return null;
            User user = _appRepo.GetUserByAppToken(token.Trim());
            if (user == null || user.IsDeleted || user.IsLocked || !user.IsActive) return null;
            return user;
        }

        private static string StatusFilter(string status)
        {
            if (string.IsNullOrWhiteSpace(status)) return null;
            string s = status.Trim();

            if (s == RechargeStatusCodes.SUCCESS.ToString(CultureInfo.InvariantCulture)) return "Success";
            if (s == RechargeStatusCodes.FAILED.ToString(CultureInfo.InvariantCulture))  return "Failed";
            if (s == RechargeStatusCodes.PENDING.ToString(CultureInfo.InvariantCulture)) return "Pending";

            if (s.Equals("success", StringComparison.OrdinalIgnoreCase)) return "Success";
            if (s.Equals("failed",  StringComparison.OrdinalIgnoreCase) ||
                s.Equals("failure", StringComparison.OrdinalIgnoreCase)) return "Failed";
            if (s.Equals("pending", StringComparison.OrdinalIgnoreCase) ||
                s.Equals("process", StringComparison.OrdinalIgnoreCase) ||
                s.Equals("processing", StringComparison.OrdinalIgnoreCase)) return "Pending";
            if (s.Equals("all", StringComparison.OrdinalIgnoreCase)) return null;
            return s;
        }

        private static readonly string[] _dateFormats =
            { "yyyy-MM-dd", "dd-MM-yyyy", "dd/MM/yyyy", "MM/dd/yyyy", "yyyy/MM/dd", "dd-MMM-yyyy", "yyyyMMdd" };

        private static DateTime? ParseDate(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            if (DateTime.TryParseExact(value.Trim(), _dateFormats, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out DateTime d))
                return d.Date;
            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out d))
                return d.Date;
            return null;
        }

        private static AppSignupResponse Signup(string error, string status, string message)
            => new AppSignupResponse { Error = error, Status = status, Message = message };

        private IHttpActionResult Reply<T>(T body) => Json(body, _json);

        #endregion
    }
}

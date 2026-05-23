using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Web;
using System.Web.Http;
using Dapper;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VIRENDRA.Data;
using VIRENDRA.Models;

namespace VIRENDRA.Controllers
{
    [RoutePrefix("api")]
    public class MobileController : ApiController
    {
        private static readonly ConcurrentDictionary<string, RechargeRecord> RechargeRecords =
            new ConcurrentDictionary<string, RechargeRecord>(StringComparer.OrdinalIgnoreCase);

        private readonly IUserRepository _userRepository;
        private readonly string _connectionString;

        public MobileController()
            : this(ConfigurationManager.ConnectionStrings["sqlconn"]?.ConnectionString)
        {
        }

        public MobileController(string connectionString)
            : this(new UserRepository(connectionString), connectionString)
        {
        }

        public MobileController(IUserRepository userRepository)
            : this(userRepository, ConfigurationManager.ConnectionStrings["sqlconn"]?.ConnectionString)
        {
        }

        public MobileController(IUserRepository userRepository, string connectionString)
        {
            _userRepository = userRepository;
            _connectionString = connectionString;
        }

        [HttpGet]
        [HttpPost]
        [Route("balance")]
        [Route("balanceCheck")]
        public IHttpActionResult BalanceCheck(string username = null, string token = null)
        {
            IDictionary<string, string> values = ReadRequestValues();
            username = FirstValue(username, values, "username");
            token = FirstValue(token, values, "token");

            User user = ValidateApiUser(username, token, out string errorMessage);

            if (user == null)
            {
                return Ok(new { status = "0", message = errorMessage });
            }

            return Ok(new
            {
                status = "1",
                balance = FormatAmount(user.UserBal ?? 0)
            });
        }

        [HttpGet]
        [HttpPost]
        [Route("recharge")]
        public IHttpActionResult Recharge(
            string username = null,
            string token = null,
            string number = null,
            string operator_code = null,
            string circle_code = null,
            decimal? amount = null,
            string txn_id = null,
            string optional1 = null,
            string optional2 = null,
            string optional3 = null)
        {
            IDictionary<string, string> values = ReadRequestValues();
            username = FirstValue(username, values, "username");
            token = FirstValue(token, values, "token");
            number = FirstValue(number, values, "number");
            operator_code = FirstValue(operator_code, values, "operator_code");
            circle_code = FirstValue(circle_code, values, "circle_code");
            txn_id = FirstValue(txn_id, values, "txn_id");
            optional1 = FirstValue(optional1, values, "optional1");
            optional2 = FirstValue(optional2, values, "optional2");
            optional3 = FirstValue(optional3, values, "optional3");

            if (amount == null && values.TryGetValue("amount", out string amountValue) &&
                decimal.TryParse(amountValue, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal parsedAmount))
            {
                amount = parsedAmount;
            }

            User user = ValidateApiUser(username, token, out string errorMessage);

            if (user == null)
            {
                return Ok(new { status = "0", message = errorMessage });
            }

            if (string.IsNullOrWhiteSpace(number))
            {
                return Ok(new { status = "0", message = "number is required" });
            }

            if (string.IsNullOrWhiteSpace(operator_code))
            {
                return Ok(new { status = "0", message = "operator_code is required" });
            }

            if (amount == null || amount <= 0)
            {
                return Ok(new { status = "0", message = "valid amount is required" });
            }

            if ((user.UserBal ?? 0) < amount.Value)
            {
                return Ok(new { status = "0", message = "insufficient balance" });
            }

            string apiTxnId = string.IsNullOrWhiteSpace(txn_id)
                ? GenerateTxnId()
                : txn_id.Trim();

            var record = new RechargeRecord
            {
                Username = user.Username,
                ApiTxnId = apiTxnId,
                Number = number.Trim(),
                OperatorCode = operator_code.Trim(),
                CircleCode = (circle_code ?? string.Empty).Trim(),
                Amount = amount.Value,
                Status = "3",
                Message = "recharge_pending",
                OperatorReference = string.Empty,
                RequestTime = DateTime.Now,
                Optional1 = (optional1 ?? string.Empty).Trim(),
                Optional2 = (optional2 ?? string.Empty).Trim(),
                Optional3 = (optional3 ?? string.Empty).Trim()
            };

            RechargeRecords[BuildRecordKey(user.Username, apiTxnId, record.Number)] = record;

            return Ok(new
            {
                status = record.Status,
                api_txn_id = record.ApiTxnId,
                message = record.Message,
                operator_reference = record.OperatorReference
            });
        }

        [HttpGet]
        [HttpPost]
        [Route("statusCheck")]
        [Route("status")]
        public IHttpActionResult StatusCheck(
            string username = null,
            string token = null,
            string txn_id = null,
            string number = null)
        {
            IDictionary<string, string> values = ReadRequestValues();
            username = FirstValue(username, values, "username");
            token = FirstValue(token, values, "token");
            txn_id = FirstValue(txn_id, values, "txn_id");
            number = FirstValue(number, values, "number");

            User user = ValidateApiUser(username, token, out string errorMessage);

            if (user == null)
            {
                return Ok(new { status = "0", message = errorMessage });
            }

            if (string.IsNullOrWhiteSpace(txn_id))
            {
                return Ok(new { status = "0", message = "txn_id is required" });
            }

            if (string.IsNullOrWhiteSpace(number))
            {
                return Ok(new { status = "0", message = "number is required" });
            }

            string key = BuildRecordKey(user.Username, txn_id.Trim(), number.Trim());

            if (!RechargeRecords.TryGetValue(key, out RechargeRecord record))
            {
                record = GetRechargeRecordFromDatabase(user, txn_id.Trim(), number.Trim());
            }

            if (record == null)
            {
                return Ok(new { status = "0", message = "Transaction not found" });
            }

            return Ok(new
            {
                status = record.Status,
                amount = FormatAmount(record.Amount),
                txn_id = record.ApiTxnId,
                operator_reference = record.OperatorReference,
                message = record.Message
            });
        }

        private User ValidateApiUser(string username, string token, out string errorMessage)
        {
            errorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(username))
            {
                errorMessage = "username is required";
                return null;
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                errorMessage = "token is required";
                return null;
            }

            User user = _userRepository.GetUserByUsername(username.Trim());

            if (user == null || user.IsDeleted)
            {
                errorMessage = "Invalid username";
                return null;
            }

            if (!user.IsActive || user.IsLocked)
            {
                errorMessage = "User account is not active";
                return null;
            }

            if (!string.Equals(user.TokenAPI, token.Trim(), StringComparison.Ordinal))
            {
                errorMessage = "Invalid token";
                return null;
            }

            if (!IsRegisteredIp(user))
            {
                errorMessage = "IP address is not allowed";
                return null;
            }

            return user;
        }

        private IDictionary<string, string> ReadRequestValues()
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var pair in Request.GetQueryNameValuePairs())
            {
                values[pair.Key] = pair.Value;
            }

            HttpRequest request = HttpContext.Current?.Request;

            if (request != null)
            {
                foreach (string key in request.Form.AllKeys.Where(key => key != null))
                {
                    values[key] = request.Form[key];
                }
            }

            if (Request.Content != null &&
                Request.Content.Headers.ContentType != null &&
                string.Equals(Request.Content.Headers.ContentType.MediaType, "application/json", StringComparison.OrdinalIgnoreCase))
            {
                string body = Request.Content.ReadAsStringAsync().Result;

                if (!string.IsNullOrWhiteSpace(body))
                {
                    try
                    {
                        foreach (JProperty property in JObject.Parse(body).Properties())
                        {
                            values[property.Name] = property.Value.Type == JTokenType.Null
                                ? string.Empty
                                : property.Value.ToString();
                        }
                    }
                    catch (JsonReaderException)
                    {
                        // Validation below will return the normal missing/invalid parameter response.
                    }
                }
            }

            return values;
        }

        private static string FirstValue(string explicitValue, IDictionary<string, string> values, string key)
        {
            if (!string.IsNullOrWhiteSpace(explicitValue))
            {
                return explicitValue;
            }

            return values.TryGetValue(key, out string value) ? value : explicitValue;
        }

        private RechargeRecord GetRechargeRecordFromDatabase(User user, string txnId, string number)
        {
            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                return null;
            }

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    const string query = @"
                        SELECT TOP 1
                            UserTxnId,
                            CustomerNo,
                            Amount,
                            StatusId
                        FROM Recharge
                        WHERE UserId = @UserId
                            AND UserTxnId = @TxnId
                            AND CustomerNo = @Number
                            AND (IsDeleted IS NULL OR IsDeleted = 0)
                        ORDER BY RequestTime DESC";

                    dynamic row = connection.QueryFirstOrDefault(query, new
                    {
                        UserId = user.Id,
                        TxnId = txnId,
                        Number = number
                    });

                    if (row == null)
                    {
                        return null;
                    }

                    string status = Convert.ToString(row.StatusId, CultureInfo.InvariantCulture);

                    return new RechargeRecord
                    {
                        Username = user.Username,
                        ApiTxnId = Convert.ToString(row.UserTxnId, CultureInfo.InvariantCulture),
                        Number = Convert.ToString(row.CustomerNo, CultureInfo.InvariantCulture),
                        Amount = Convert.ToDecimal(row.Amount, CultureInfo.InvariantCulture),
                        Status = status,
                        Message = StatusMessage(status),
                        OperatorReference = string.Empty
                    };
                }
            }
            catch (SqlException)
            {
                return null;
            }
        }

        private static string StatusMessage(string status)
        {
            switch (status)
            {
                case "1":
                    return "recharge_success";
                case "2":
                    return "recharge_failed";
                case "3":
                    return "recharge_pending";
                case "4":
                    return "recharge_refund";
                default:
                    return "unknown";
            }
        }

        private bool IsRegisteredIp(User user)
        {
            if (string.IsNullOrWhiteSpace(user.LoginIP))
            {
                return true;
            }

            string clientIp = GetClientIp();
            return string.Equals(user.LoginIP.Trim(), clientIp, StringComparison.OrdinalIgnoreCase);
        }

        private static string GetClientIp()
        {
            HttpRequest request = HttpContext.Current?.Request;

            if (request == null)
            {
                return string.Empty;
            }

            string forwardedFor = request.Headers["X-Forwarded-For"];

            if (!string.IsNullOrWhiteSpace(forwardedFor))
            {
                return forwardedFor.Split(',')[0].Trim();
            }

            return request.UserHostAddress ?? string.Empty;
        }

        private static string BuildRecordKey(string username, string txnId, string number)
        {
            return string.Join("|", username ?? string.Empty, txnId ?? string.Empty, number ?? string.Empty);
        }

        private static string GenerateTxnId()
        {
            return DateTime.Now.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
        }

        private static string FormatAmount(decimal amount)
        {
            return amount.ToString("0.0000", CultureInfo.InvariantCulture);
        }

        private class RechargeRecord
        {
            public string Username { get; set; }
            public string ApiTxnId { get; set; }
            public string Number { get; set; }
            public string OperatorCode { get; set; }
            public string CircleCode { get; set; }
            public decimal Amount { get; set; }
            public string Status { get; set; }
            public string Message { get; set; }
            public string OperatorReference { get; set; }
            public DateTime RequestTime { get; set; }
            public string Optional1 { get; set; }
            public string Optional2 { get; set; }
            public string Optional3 { get; set; }
        }
    }
}

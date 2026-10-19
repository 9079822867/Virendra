using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using Dapper;
using VIRENDRA.Models;

namespace VIRENDRA.Data
{
    /// <summary>
    /// Switch settings (ported from STC CommonSwitch / OperatorSwitch): operator-wise and circle-wise
    /// vendors, block routes, stop-route messages, vendor replace and operator input validation.
    /// </summary>
    public class SwitchRepository
    {
        private readonly string _conn;

        public SwitchRepository()
        {
            _conn = ConfigurationManager.ConnectionStrings["sqlconn"]?.ConnectionString
                ?? throw new ConfigurationErrorsException("sqlconn connection string is missing in Web.config");
        }

        // ── Lookups ─────────────────────────────────────────────────────────

        public List<LookupItem> GetOperators()
        {
            using (var c = new SqlConnection(_conn))
                return c.Query<LookupItem>("SELECT Id, Name FROM [Operator] ORDER BY Name").ToList();
        }

        public List<LookupItem> GetCircles()
        {
            using (var c = new SqlConnection(_conn))
                return c.Query<LookupItem>("SELECT Id, CircleName AS Name FROM Circle ORDER BY CircleName").ToList();
        }

        public List<LookupItem> GetApis()
        {
            using (var c = new SqlConnection(_conn))
                return c.Query<LookupItem>("SELECT Id, ApiName AS Name FROM ApiSource ORDER BY ApiName").ToList();
        }

        public List<LookupItem> GetUsers()
        {
            using (var c = new SqlConnection(_conn))
                return c.Query<LookupItem>(@"
                    SELECT u.Id, ISNULL(up.FullName + ' - ', '') + u.Username AS Name
                    FROM [User] u
                    OUTER APPLY (SELECT TOP 1 p.FullName FROM UserProfile p WHERE p.UserId = u.Id) up
                    WHERE u.IsDeleted = 0
                    ORDER BY u.Username").ToList();
        }

        public List<LookupItem> GetSwitchTypes()
        {
            using (var c = new SqlConnection(_conn))
                return c.Query<LookupItem>("SELECT Id, TypeName AS Name FROM SwitchType ORDER BY Id").ToList();
        }

        // ── Operator switch ─────────────────────────────────────────────────

        public List<OperatorSwitchRow> GetOperatorSwitches()
        {
            using (var c = new SqlConnection(_conn))
                return c.Query<OperatorSwitchRow>(@"
                    SELECT o.Id AS OpId, o.Name AS OperatorName, t.TypeName AS OperatorType,
                           ISNULL(o.IsActive, 0) AS IsActive, ISNULL(o.SwitchTypeId, 1) AS SwitchTypeId,
                           ISNULL(o.API1_Id, 0) AS Api1, ISNULL(o.API2_Id, 0) AS Api2, ISNULL(o.API3_Id, 0) AS Api3,
                           (SELECT COUNT(*) FROM CircleRouting cr WHERE cr.OpId = o.Id) AS CircleRoutes
                    FROM [Operator] o
                    LEFT JOIN OperatorType t ON t.Id = o.OpTypeId
                    ORDER BY o.Name").ToList();
        }

        public void SaveOperatorSwitch(int opId, int switchTypeId, int api1, int api2, int api3, int userId)
        {
            using (var c = new SqlConnection(_conn))
                c.Execute(@"
                    UPDATE [Operator]
                    SET SwitchTypeId = @SwitchTypeId,
                        API1_Id = @Api1, API2_Id = @Api2, API3_Id = @Api3,
                        UpdatedById = @UserId, UpdatedDate = GETDATE()
                    WHERE Id = @OpId",
                    new
                    {
                        OpId = opId, SwitchTypeId = switchTypeId,
                        Api1 = NullIfZero(api1), Api2 = NullIfZero(api2), Api3 = NullIfZero(api3),
                        UserId = NullIfZero(userId)
                    });
        }

        // ── Circle switch ───────────────────────────────────────────────────

        public CircleSwitchViewModel GetCircleSwitch(int opId)
        {
            using (var c = new SqlConnection(_conn))
            {
                var op = c.QueryFirstOrDefault(
                    "SELECT Name, ISNULL(SwitchTypeId, 1) AS SwitchTypeId FROM [Operator] WHERE Id = @Id", new { Id = opId });
                if (op == null) return null;

                return new CircleSwitchViewModel
                {
                    OpId         = opId,
                    OperatorName = (string)op.Name,
                    SwitchTypeId = (int)op.SwitchTypeId,
                    Rows = c.Query<CircleSwitchRow>(@"
                        SELECT ci.Id AS CircleId, ci.CircleName,
                               ISNULL(cr.API1_Id, 0) AS Api1, ISNULL(cr.API2_Id, 0) AS Api2, ISNULL(cr.API3_Id, 0) AS Api3,
                               ISNULL(cr.IsROffer, 0) AS IsROffer
                        FROM Circle ci
                        LEFT JOIN CircleRouting cr ON cr.CircleId = ci.Id AND cr.OpId = @OpId
                        ORDER BY ci.CircleName", new { OpId = opId }).ToList()
                };
            }
        }

        /// <summary>Upserts every circle; a circle with no vendor at all is removed.</summary>
        public void SaveCircleSwitch(int opId, List<CircleSwitchRow> rows, int userId)
        {
            using (var c = new SqlConnection(_conn))
            {
                c.Open();
                using (var tx = c.BeginTransaction())
                {
                    foreach (var r in rows ?? new List<CircleSwitchRow>())
                    {
                        var p = new
                        {
                            OpId = opId, r.CircleId,
                            Api1 = NullIfZero(r.Api1), Api2 = NullIfZero(r.Api2), Api3 = NullIfZero(r.Api3),
                            r.IsROffer, UserId = NullIfZero(userId)
                        };

                        if (r.Api1 <= 0 && r.Api2 <= 0 && r.Api3 <= 0)
                        {
                            c.Execute("DELETE FROM CircleRouting WHERE OpId = @OpId AND CircleId = @CircleId", p, tx);
                            continue;
                        }

                        c.Execute(@"
                            IF EXISTS (SELECT 1 FROM CircleRouting WHERE OpId = @OpId AND CircleId = @CircleId)
                                UPDATE CircleRouting
                                SET API1_Id = @Api1, API2_Id = @Api2, API3_Id = @Api3, IsROffer = @IsROffer,
                                    UpdatedById = @UserId, UpdatedDate = GETDATE()
                                WHERE OpId = @OpId AND CircleId = @CircleId
                            ELSE
                                INSERT INTO CircleRouting (CircleId, OpId, API1_Id, API2_Id, API3_Id, IsROffer, AddedDate, AddedById)
                                VALUES (@CircleId, @OpId, @Api1, @Api2, @Api3, @IsROffer, GETDATE(), @UserId)", p, tx);
                    }
                    tx.Commit();
                }
            }
        }

        // ── Block route ─────────────────────────────────────────────────────

        public List<BlockRouteRow> GetBlockRoutes()
        {
            using (var c = new SqlConnection(_conn))
                return c.Query<BlockRouteRow>("SELECT * FROM BlockRoute ORDER BY Id DESC").ToList();
        }

        public BlockRouteRow GetBlockRoute(int id)
        {
            using (var c = new SqlConnection(_conn))
                return c.QueryFirstOrDefault<BlockRouteRow>("SELECT * FROM BlockRoute WHERE Id = @Id", new { Id = id });
        }

        public void SaveBlockRoute(BlockRouteRow r, int userId)
        {
            using (var c = new SqlConnection(_conn))
            {
                if (r.Id > 0)
                    c.Execute(@"
                        UPDATE BlockRoute
                        SET OpFilter = @OpFilter, CircleFilter = @CircleFilter, UserFilter = @UserFilter,
                            Amounts = @Amounts, AmountRanges = @AmountRanges,
                            SettingTypeId = @SettingTypeId, SendStatusId = @SendStatusId,
                            UpdatedById = @UserId, UpdatedDate = GETDATE()
                        WHERE Id = @Id",
                        new { r.Id, r.OpFilter, r.CircleFilter, r.UserFilter, r.Amounts, r.AmountRanges,
                              r.SettingTypeId, r.SendStatusId, UserId = NullIfZero(userId) });
                else
                    c.Execute(@"
                        INSERT INTO BlockRoute
                            (OpFilter, CircleFilter, UserFilter, Amounts, AmountRanges,
                             SettingTypeId, SendStatusId, AddedById, AddedDate)
                        VALUES
                            (@OpFilter, @CircleFilter, @UserFilter, @Amounts, @AmountRanges,
                             @SettingTypeId, @SendStatusId, @UserId, GETDATE())",
                        new { r.OpFilter, r.CircleFilter, r.UserFilter, r.Amounts, r.AmountRanges,
                              r.SettingTypeId, r.SendStatusId, UserId = NullIfZero(userId) });
            }
        }

        public void DeleteBlockRoute(int id)
        {
            using (var c = new SqlConnection(_conn))
                c.Execute("DELETE FROM BlockRoute WHERE Id = @Id", new { Id = id });
        }

        // ── Stop route message ──────────────────────────────────────────────

        public List<StopRouteMessageRow> GetStopRouteMessages()
        {
            using (var c = new SqlConnection(_conn))
                return c.Query<StopRouteMessageRow>("SELECT * FROM StopRouteMessage ORDER BY Id DESC").ToList();
        }

        public StopRouteMessageRow GetStopRouteMessage(int id)
        {
            using (var c = new SqlConnection(_conn))
                return c.QueryFirstOrDefault<StopRouteMessageRow>("SELECT * FROM StopRouteMessage WHERE Id = @Id", new { Id = id });
        }

        public void SaveStopRouteMessage(StopRouteMessageRow r, int userId)
        {
            using (var c = new SqlConnection(_conn))
            {
                if (r.Id > 0)
                    c.Execute(@"
                        UPDATE StopRouteMessage
                        SET OpFilter = @OpFilter, ApiFilter = @ApiFilter, MsgFilter = @MsgFilter,
                            UpdatedById = @UserId, UpdatedDate = GETDATE()
                        WHERE Id = @Id",
                        new { r.Id, r.OpFilter, r.ApiFilter, r.MsgFilter, UserId = NullIfZero(userId) });
                else
                    c.Execute(@"
                        INSERT INTO StopRouteMessage (OpFilter, ApiFilter, MsgFilter, AddedById, AddedDate)
                        VALUES (@OpFilter, @ApiFilter, @MsgFilter, @UserId, GETDATE())",
                        new { r.OpFilter, r.ApiFilter, r.MsgFilter, UserId = NullIfZero(userId) });
            }
        }

        public void DeleteStopRouteMessage(int id)
        {
            using (var c = new SqlConnection(_conn))
                c.Execute("DELETE FROM StopRouteMessage WHERE Id = @Id", new { Id = id });
        }

        /// <summary>
        /// True when a failed vendor reply contains a stop message set for this vendor + operator,
        /// i.e. the recharge must fail now instead of switching to the next vendor.
        /// </summary>
        public bool IsStopRouteMessage(string response, int apiId, int opId)
        {
            if (string.IsNullOrWhiteSpace(response)) return false;

            List<StopRouteMessageRow> rows;
            using (var c = new SqlConnection(_conn))
                rows = c.Query<StopRouteMessageRow>("SELECT OpFilter, ApiFilter, MsgFilter FROM StopRouteMessage").ToList();

            return rows.Any(r =>
                FilterHas(r.OpFilter, opId) && FilterHas(r.ApiFilter, apiId) &&
                (r.MsgFilter ?? string.Empty).Split(';')
                    .Select(m => m.Trim())
                    .Any(m => m.Length > 0 && response.IndexOf(m, StringComparison.OrdinalIgnoreCase) >= 0));
        }

        // ── Replace switch ──────────────────────────────────────────────────

        /// <summary>
        /// Moves one vendor's routes to another. switchTypeId 0 = everywhere; opId 0 = all operators.
        /// Common / Amount wise = CommanRouting.ApiId, Operator wise = Operator.API1..3, Circle wise = CircleRouting.API1..3.
        /// Returns the number of rows changed.
        /// </summary>
        public int ReplaceSwitch(int opId, int switchTypeId, int currentApiId, int newApiId, int userId)
        {
            var p = new { OpId = opId, Cur = currentApiId, New = newApiId, UserId = NullIfZero(userId) };
            bool all = switchTypeId <= 0;
            int changed = 0;

            using (var c = new SqlConnection(_conn))
            {
                c.Open();
                using (var tx = c.BeginTransaction())
                {
                    if (all || switchTypeId == SwitchTypes.Common || switchTypeId == SwitchTypes.Amount)
                        changed += c.Execute(@"
                            UPDATE CommanRouting SET ApiId = @New, UpdatedById = @UserId, UpdatedDate = GETDATE()
                            WHERE ApiId = @Cur AND (@OpId = 0 OR OpId = @OpId)", p, tx);

                    if (all || switchTypeId == SwitchTypes.Operator)
                        changed += c.Execute(@"
                            UPDATE [Operator]
                            SET API1_Id = CASE WHEN API1_Id = @Cur THEN @New ELSE API1_Id END,
                                API2_Id = CASE WHEN API2_Id = @Cur THEN @New ELSE API2_Id END,
                                API3_Id = CASE WHEN API3_Id = @Cur THEN @New ELSE API3_Id END,
                                UpdatedById = @UserId, UpdatedDate = GETDATE()
                            WHERE @Cur IN (ISNULL(API1_Id, 0), ISNULL(API2_Id, 0), ISNULL(API3_Id, 0))
                              AND (@OpId = 0 OR Id = @OpId)", p, tx);

                    if (all || switchTypeId == SwitchTypes.Circle)
                        changed += c.Execute(@"
                            UPDATE CircleRouting
                            SET API1_Id = CASE WHEN API1_Id = @Cur THEN @New ELSE API1_Id END,
                                API2_Id = CASE WHEN API2_Id = @Cur THEN @New ELSE API2_Id END,
                                API3_Id = CASE WHEN API3_Id = @Cur THEN @New ELSE API3_Id END,
                                UpdatedById = @UserId, UpdatedDate = GETDATE()
                            WHERE @Cur IN (ISNULL(API1_Id, 0), ISNULL(API2_Id, 0), ISNULL(API3_Id, 0))
                              AND (@OpId = 0 OR OpId = @OpId)", p, tx);

                    tx.Commit();
                }
            }
            return changed;
        }

        // ── Operator validation ─────────────────────────────────────────────

        public List<OperatorValidationRule> GetValidationRules(int opId)
        {
            using (var c = new SqlConnection(_conn))
                return c.Query<OperatorValidationRule>(@"
                    SELECT Id, ISNULL(OpId, 0) AS OpId, ColumnName, MinLenth, MaxLenth, MinVal, MaxVal, StartWith,
                           ISNULL(IsNum, 0) AS IsNum, ISNULL(IsMandatory, 0) AS IsMandatory, ErrorMsg, PlaceHolderText
                    FROM OperatorValidation WHERE OpId = @OpId", new { OpId = opId }).ToList();
        }

        /// <summary>Saves one column's rule; an empty rule (nothing set) is deleted.</summary>
        public void SaveValidationRule(int opId, OperatorValidationRule r, int userId)
        {
            bool empty = r.MinLenth == null && r.MaxLenth == null && r.MinVal == null && r.MaxVal == null &&
                         string.IsNullOrWhiteSpace(r.StartWith) && !r.IsNum && !r.IsMandatory &&
                         string.IsNullOrWhiteSpace(r.PlaceHolderText);

            using (var c = new SqlConnection(_conn))
            {
                if (empty)
                {
                    c.Execute("DELETE FROM OperatorValidation WHERE OpId = @OpId AND ColumnName = @Col",
                        new { OpId = opId, Col = r.ColumnName });
                    return;
                }

                c.Execute(@"
                    IF EXISTS (SELECT 1 FROM OperatorValidation WHERE OpId = @OpId AND ColumnName = @ColumnName)
                        UPDATE OperatorValidation
                        SET MinLenth = @MinLenth, MaxLenth = @MaxLenth, MinVal = @MinVal, MaxVal = @MaxVal,
                            StartWith = @StartWith, IsNum = @IsNum, IsMandatory = @IsMandatory,
                            ErrorMsg = @ErrorMsg, PlaceHolderText = @PlaceHolderText,
                            UpdatedById = @UserId, UpdatedDate = GETDATE()
                        WHERE OpId = @OpId AND ColumnName = @ColumnName
                    ELSE
                        INSERT INTO OperatorValidation
                            (OpId, ColumnName, MinLenth, MaxLenth, MinVal, MaxVal, StartWith, IsNum, IsMandatory,
                             ErrorMsg, PlaceHolderText, AddedById, AddedDate)
                        VALUES
                            (@OpId, @ColumnName, @MinLenth, @MaxLenth, @MinVal, @MaxVal, @StartWith, @IsNum, @IsMandatory,
                             @ErrorMsg, @PlaceHolderText, @UserId, GETDATE())",
                    new
                    {
                        OpId = opId, r.ColumnName, r.MinLenth, r.MaxLenth, r.MinVal, r.MaxVal,
                        StartWith = Trim(r.StartWith), r.IsNum, r.IsMandatory,
                        ErrorMsg = Trim(r.ErrorMsg), PlaceHolderText = Trim(r.PlaceHolderText),
                        UserId = NullIfZero(userId)
                    });
            }
        }

        /// <summary>
        /// Checks recharge input against the operator's rules. Returns null when valid, else the message to show.
        /// </summary>
        public string ValidateRechargeInput(int opId, string number, decimal amount, string account = null)
        {
            if (opId <= 0) return null;
            List<OperatorValidationRule> rules;
            try { rules = GetValidationRules(opId); }
            catch { return null; }   // validation table unavailable → don't block recharges

            foreach (var r in rules)
            {
                string value =
                    r.ColumnName == ValidationColumns.Number  ? number :
                    r.ColumnName == ValidationColumns.Account ? account :
                    r.ColumnName == ValidationColumns.Amount  ? amount.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) :
                    null;
                string error = Check(r, (value ?? string.Empty).Trim());
                if (error != null)
                    return string.IsNullOrWhiteSpace(r.ErrorMsg) ? error : r.ErrorMsg.Trim();
            }
            return null;
        }

        private static string Check(OperatorValidationRule r, string v)
        {
            string label = r.ColumnName;
            if (v.Length == 0)
                return r.IsMandatory ? label + " is required." : null;

            if (r.IsNum && !v.All(ch => char.IsDigit(ch) || (r.ColumnName == ValidationColumns.Amount && ch == '.')))
                return label + " must be numeric.";
            if (r.MinLenth > 0 && v.Length < r.MinLenth) return label + " must be at least " + r.MinLenth + " characters.";
            if (r.MaxLenth > 0 && v.Length > r.MaxLenth) return label + " must be at most " + r.MaxLenth + " characters.";

            if (r.MinVal != null || r.MaxVal != null)
            {
                if (!decimal.TryParse(v, System.Globalization.NumberStyles.Number,
                        System.Globalization.CultureInfo.InvariantCulture, out decimal n))
                    return label + " must be a number.";
                if (r.MinVal != null && n < r.MinVal) return label + " must be at least " + r.MinVal + ".";
                if (r.MaxVal != null && n > r.MaxVal) return label + " must be at most " + r.MaxVal + ".";
            }

            if (!string.IsNullOrWhiteSpace(r.StartWith))
            {
                var prefixes = r.StartWith.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
                if (prefixes.Count > 0 && !prefixes.Any(pfx => v.StartsWith(pfx, StringComparison.OrdinalIgnoreCase)))
                    return label + " must start with " + string.Join(" / ", prefixes) + ".";
            }
            return null;
        }

        // ── Helpers ─────────────────────────────────────────────────────────

        /// <summary>'All' / empty filter matches everything; else a comma list of ids.</summary>
        private static bool FilterHas(string filter, int id)
        {
            if (string.IsNullOrWhiteSpace(filter) || filter.IndexOf("all", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            string s = id.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return filter.Split(',').Any(x => x.Trim() == s);
        }

        private static int? NullIfZero(int v) => v > 0 ? v : (int?)null;

        private static string Trim(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }
}

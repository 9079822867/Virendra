using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;
using VIRENDRA.Data;
using VIRENDRA.Infrastructure;
using VIRENDRA.Models;

namespace VIRENDRA.Controllers
{
    /// <summary>
    /// Switch settings ported from STC (OperatorSwitch / CommonSwitch controllers):
    /// Operator Switch, Circle Switch, Block Route, Stop Route Message, Replace Switch, Operator Validation.
    /// Common routing itself stays on CommonRoutingController.
    /// </summary>
    [RoleAuthorize(RoleConstants.SuperAdmin, RoleConstants.Admin)]
    public class SwitchController : Controller
    {
        private readonly SwitchRepository _repo = new SwitchRepository();

        private int CurrentUserId => Session["UserId"] as int? ?? 0;

        // ── Operator switch ─────────────────────────────────────────────────

        [HttpGet]
        public ActionResult OperatorSwitch()
        {
            ViewBag.SwitchTypes = _repo.GetSwitchTypes();
            ViewBag.Apis        = _repo.GetApis();
            return View(_repo.GetOperatorSwitches());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult OperatorSwitch(int opId, int switchTypeId, int? api1, int? api2, int? api3)
        {
            if (switchTypeId < SwitchTypes.Common || switchTypeId > SwitchTypes.Amount)
            {
                TempData["ErrorMessage"] = "Choose a switch type.";
                return RedirectToAction("OperatorSwitch");
            }
            if (switchTypeId == SwitchTypes.Operator && (api1 ?? 0) + (api2 ?? 0) + (api3 ?? 0) == 0)
            {
                TempData["ErrorMessage"] = "Operator Wise needs at least one vendor (API 1 / 2 / 3).";
                return RedirectToAction("OperatorSwitch");
            }

            _repo.SaveOperatorSwitch(opId, switchTypeId, api1 ?? 0, api2 ?? 0, api3 ?? 0, CurrentUserId);
            TempData["SuccessMessage"] = "Operator switch saved.";
            return RedirectToAction("OperatorSwitch");
        }

        // ── Circle switch ───────────────────────────────────────────────────

        [HttpGet]
        public ActionResult CircleSwitch(int id)
        {
            var vm = _repo.GetCircleSwitch(id);
            if (vm == null) return HttpNotFound();
            ViewBag.Apis = _repo.GetApis();
            return View(vm);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult CircleSwitch(int opId, int[] circleId, int[] api1, int[] api2, int[] api3, int[] roffer)
        {
            var rows = new List<CircleSwitchRow>();
            var ro   = new HashSet<int>(roffer ?? new int[0]);
            for (int i = 0; circleId != null && i < circleId.Length; i++)
            {
                rows.Add(new CircleSwitchRow
                {
                    CircleId = circleId[i],
                    Api1     = At(api1, i),
                    Api2     = At(api2, i),
                    Api3     = At(api3, i),
                    IsROffer = ro.Contains(circleId[i])
                });
            }

            _repo.SaveCircleSwitch(opId, rows, CurrentUserId);
            TempData["SuccessMessage"] = "Circle switch saved.";
            return RedirectToAction("CircleSwitch", new { id = opId });
        }

        // ── Block route ─────────────────────────────────────────────────────

        [HttpGet]
        public ActionResult BlockRoute(int? editId)
        {
            FillFilterLists();
            ViewBag.Edit = editId > 0 ? _repo.GetBlockRoute(editId.Value) : null;
            return View(_repo.GetBlockRoutes());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult SaveBlockRoute(int id, int[] opIds, int[] circleIds, int[] userIds, string amounts)
        {
            SplitAmounts(amounts, out string list, out string ranges, out string error);
            if (error != null)
            {
                TempData["ErrorMessage"] = error;
                return RedirectToAction("BlockRoute", new { editId = id > 0 ? id : (int?)null });
            }
            if (list == null && ranges == null)
            {
                TempData["ErrorMessage"] = "Enter the amounts to block, e.g. 10,20 or 1-5000.";
                return RedirectToAction("BlockRoute", new { editId = id > 0 ? id : (int?)null });
            }

            // SettingTypeId 1 = block: SP_RechargeRequestValidation returns 103 "Service Down" and the
            // recharge fails before any debit. (STC's type 2 "callback thread" isn't handled by VIRENDRA's
            // recharge flow, so it isn't offered.)
            _repo.SaveBlockRoute(new BlockRouteRow
            {
                Id            = id,
                OpFilter      = JoinIds(opIds),
                CircleFilter  = JoinIds(circleIds),
                UserFilter    = JoinIds(userIds),
                Amounts       = list,
                AmountRanges  = ranges,
                SettingTypeId = 1,
                SendStatusId  = RechargeStatusCodes.FAILED
            }, CurrentUserId);

            TempData["SuccessMessage"] = id > 0 ? "Block route updated." : "Block route added.";
            return RedirectToAction("BlockRoute");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult DeleteBlockRoute(int id)
        {
            _repo.DeleteBlockRoute(id);
            TempData["SuccessMessage"] = "Block route deleted.";
            return RedirectToAction("BlockRoute");
        }

        // ── Stop route message ──────────────────────────────────────────────

        [HttpGet]
        public ActionResult StopRouteMsg(int? editId)
        {
            ViewBag.Operators = _repo.GetOperators();
            ViewBag.Apis      = _repo.GetApis();
            ViewBag.Edit      = editId > 0 ? _repo.GetStopRouteMessage(editId.Value) : null;
            return View(_repo.GetStopRouteMessages());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult SaveStopRouteMsg(int id, int[] opIds, int[] apiIds, string msgFilter)
        {
            string msgs = string.Join(";", (msgFilter ?? string.Empty)
                .Split(new[] { ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(m => m.Trim()).Where(m => m.Length > 0).Distinct());
            if (msgs.Length == 0)
            {
                TempData["ErrorMessage"] = "Enter at least one message text.";
                return RedirectToAction("StopRouteMsg", new { editId = id > 0 ? id : (int?)null });
            }

            _repo.SaveStopRouteMessage(new StopRouteMessageRow
            {
                Id        = id,
                OpFilter  = JoinIds(opIds),
                ApiFilter = JoinIds(apiIds),
                MsgFilter = msgs
            }, CurrentUserId);

            TempData["SuccessMessage"] = id > 0 ? "Stop route message updated." : "Stop route message added.";
            return RedirectToAction("StopRouteMsg");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult DeleteStopRouteMsg(int id)
        {
            _repo.DeleteStopRouteMessage(id);
            TempData["SuccessMessage"] = "Stop route message deleted.";
            return RedirectToAction("StopRouteMsg");
        }

        // ── Replace switch ──────────────────────────────────────────────────

        [HttpGet]
        public ActionResult ReplaceSwitch()
        {
            ViewBag.Operators   = _repo.GetOperators();
            ViewBag.Apis        = _repo.GetApis();
            ViewBag.SwitchTypes = _repo.GetSwitchTypes();
            return View();
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult ReplaceSwitch(int? opId, int? switchTypeId, int currentApiId, int newApiId)
        {
            if (currentApiId <= 0 || newApiId <= 0)
                TempData["ErrorMessage"] = "Choose both the current and the new vendor.";
            else if (currentApiId == newApiId)
                TempData["ErrorMessage"] = "Current vendor and new vendor must not be the same.";
            else
            {
                int n = _repo.ReplaceSwitch(opId ?? 0, switchTypeId ?? 0, currentApiId, newApiId, CurrentUserId);
                TempData["SuccessMessage"] = n > 0
                    ? "Vendor replaced in " + n + " route" + (n == 1 ? "" : "s") + "."
                    : "No routes use that vendor for this selection — nothing changed.";
            }
            return RedirectToAction("ReplaceSwitch");
        }

        // ── Operator validation ─────────────────────────────────────────────

        [HttpGet]
        public ActionResult Validation(int id)
        {
            var op = _repo.GetOperators().FirstOrDefault(o => o.Id == id);
            if (op == null) return HttpNotFound();

            var rules = _repo.GetValidationRules(id);
            OperatorValidationRule Get(string col) =>
                rules.FirstOrDefault(r => r.ColumnName == col) ?? new OperatorValidationRule { OpId = id, ColumnName = col };

            return View(new OperatorValidationViewModel
            {
                OpId         = id,
                OperatorName = op.Name,
                Number       = Get(ValidationColumns.Number),
                Account      = Get(ValidationColumns.Account),
                Amount       = Get(ValidationColumns.Amount)
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Validation(int opId, FormCollection form)
        {
            var errors = new List<string>();
            foreach (var col in ValidationColumns.All)
            {
                var rule = new OperatorValidationRule
                {
                    OpId            = opId,
                    ColumnName      = col,
                    StartWith       = form[col + ".StartWith"],
                    IsNum           = IsChecked(form[col + ".IsNum"]),
                    IsMandatory     = IsChecked(form[col + ".IsMandatory"]),
                    ErrorMsg        = form[col + ".ErrorMsg"],
                    PlaceHolderText = form[col + ".PlaceHolderText"]
                };
                if (!ParseRange(form[col + ".Length"], out int? minL, out int? maxL))
                    errors.Add(col + " length must look like 10 or 10-12.");
                if (!ParseRange(form[col + ".Range"], out int? minV, out int? maxV))
                    errors.Add(col + " range must look like 10-5000.");
                rule.MinLenth = minL; rule.MaxLenth = maxL; rule.MinVal = minV; rule.MaxVal = maxV;

                if (errors.Count == 0)
                    _repo.SaveValidationRule(opId, rule, CurrentUserId);
            }

            if (errors.Count > 0) TempData["ErrorMessage"] = string.Join(" ", errors);
            else TempData["SuccessMessage"] = "Validation rules saved.";
            return RedirectToAction("Validation", new { id = opId });
        }

        // ── Helpers ─────────────────────────────────────────────────────────

        private void FillFilterLists()
        {
            ViewBag.Operators = _repo.GetOperators();
            ViewBag.Circles   = _repo.GetCircles();
            ViewBag.Users     = _repo.GetUsers();
        }

        private static int At(int[] a, int i) => a != null && i < a.Length ? a[i] : 0;

        /// <summary>Empty selection or "0" (All) → "All"; else sorted comma list.</summary>
        private static string JoinIds(int[] ids)
        {
            if (ids == null || ids.Length == 0 || ids.Any(i => i <= 0)) return "All";
            return string.Join(",", ids.Distinct().OrderBy(i => i));
        }

        /// <summary>"10,20,1-500" → Amounts "10,20", AmountRanges "1-500" (the SP reads both).</summary>
        private static void SplitAmounts(string input, out string list, out string ranges, out string error)
        {
            list = ranges = error = null;
            var amounts = new List<string>();
            var rgs     = new List<string>();

            foreach (var raw in (input ?? string.Empty).Split(','))
            {
                string a = raw.Trim().Replace(" ", "");
                if (a.Length == 0) continue;

                int dash = a.IndexOf('-');
                if (dash > 0)
                {
                    if (!long.TryParse(a.Substring(0, dash), out long min) ||
                        !long.TryParse(a.Substring(dash + 1), out long max) || min > max)
                    { error = "Invalid amount range: " + a; return; }
                    rgs.Add(min + "-" + max);
                }
                else
                {
                    if (!int.TryParse(a, out int v) || v <= 0) { error = "Invalid amount: " + a; return; }
                    amounts.Add(v.ToString(CultureInfo.InvariantCulture));
                }
            }
            list   = amounts.Count > 0 ? string.Join(",", amounts.Distinct()) : null;
            ranges = rgs.Count     > 0 ? string.Join(",", rgs.Distinct())     : null;
        }

        /// <summary>"" → (null,null); "10" → (10,10); "10-12" → (10,12).</summary>
        private static bool ParseRange(string s, out int? min, out int? max)
        {
            min = max = null;
            s = (s ?? string.Empty).Replace(" ", "");
            if (s.Length == 0) return true;

            var parts = s.Split('-');
            if (parts.Length == 1 && int.TryParse(parts[0], out int one)) { min = max = one; return true; }
            if (parts.Length == 2 && int.TryParse(parts[0], out int a) && int.TryParse(parts[1], out int b) && a <= b)
            { min = a; max = b; return true; }
            return false;
        }

        // Html.CheckBox posts "true,false" when ticked
        private static bool IsChecked(string v) => !string.IsNullOrEmpty(v) && v.Split(',')[0] == "true";
    }
}

using System;
using System.Collections.Generic;
using VIRENDRA.Models;

namespace VIRENDRA.Data
{
    public interface ITransactionRepository
    {
        DashboardStats GetDashboardStats(int userId);

        List<RechargeHistoryItem> GetRecentRecharges(int top = 10);

        List<RechargeHistoryItem> GetRechargeHistory(
            int? userId, DateTime fromDate, DateTime toDate, string status, string searchText);

        List<TransactionHistoryItem> GetTransactionHistory(
            int? userId, DateTime fromDate, DateTime toDate, string type, string searchText);

        List<OutStandingItem> GetOutStandingReport(
            int? userId, DateTime fromDate, DateTime toDate, int? txnTypeId, int? amtTypeId, string searchText);

        decimal? GetOutStandingBalance(int userId);

        List<LookupItem> GetTxnTypes();
        List<LookupItem> GetAmtTypes();
    }
}

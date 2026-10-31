using System;
using System.Collections.Generic;
using VIRENDRA.Models;

namespace VIRENDRA.Data
{
    public interface ITransactionRepository
    {
        DashboardStats GetDashboardStats(int userId);

        // userId null = all users (admins only)
        List<RechargeHistoryItem> GetRecentRecharges(int? userId, int top = 10);

        List<RechargeHistoryItem> GetRechargeHistory(
            int? userId, DateTime fromDate, DateTime toDate, string status, string searchText);

        List<RequestResponseLogItem> GetRequestResponseLogs(long recId);

        List<TransactionHistoryItem> GetTransactionHistory(
            int? userId, DateTime fromDate, DateTime toDate, string type, string searchText);

        List<OutStandingItem> GetOutStandingReport(
            int? userId, DateTime fromDate, DateTime toDate, int? txnTypeId, int? amtTypeId, string searchText);

        decimal? GetOutStandingBalance(int userId);

        List<LookupItem> GetTxnTypes();
        List<LookupItem> GetAmtTypes();
    }
}

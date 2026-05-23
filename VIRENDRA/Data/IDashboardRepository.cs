using System;
using System.Collections.Generic;
using VIRENDRA.Models;

namespace VIRENDRA.Data
{
    public interface IDashboardRepository
    {
        UserDashboardInfo GetUserDashboardInfo(int userId);
        WalletSummary GetWalletSummary(int userId, DateTime fromDate, DateTime toDate);
        TransactionSummary GetTransactionSummary(int userId, DateTime fromDate, DateTime toDate);
        List<RecentTransaction> GetRecentTransactions(int userId, int pageSize = 10);
    }
}

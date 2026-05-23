using System;
using System.Collections.Generic;
using VIRENDRA.Models;

namespace VIRENDRA.Data
{
    public interface ITransactionRepository
    {
        List<RechargeHistoryItem> GetRechargeHistory(
            int? userId, DateTime fromDate, DateTime toDate, string status, string searchText);

        List<TransactionHistoryItem> GetTransactionHistory(
            int? userId, DateTime fromDate, DateTime toDate, string type, string searchText);
    }
}

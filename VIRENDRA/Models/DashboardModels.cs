using System;
using System.Collections.Generic;

namespace VIRENDRA.Models
{
    public class DashboardViewModel
    {
        public UserDashboardInfo UserInfo { get; set; }
        public WalletSummary WalletData { get; set; }
        public TransactionSummary TransactionData { get; set; }
    }

    public class UserDashboardInfo
    {
        public int UserId { get; set; }
        public string Username { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string MobileNumber { get; set; }
        public decimal? CurrentBalance { get; set; }
        public decimal? OutstandingBalance { get; set; }
        public int? PackageId { get; set; }
        public string PackageName { get; set; }
    }

    public class WalletSummary
    {
        public decimal OpeningBalance { get; set; }
        public decimal ClosingBalance { get; set; }
        public decimal TodayEarning { get; set; }
        public decimal CreditBalance { get; set; }
        public decimal RefundAmount { get; set; }
        public int TotalTransactionCount { get; set; }
    }

    public class TransactionSummary
    {
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public decimal SuccessAmount { get; set; }
        public decimal FailureAmount { get; set; }
        public decimal AverageTransactionValue { get; set; }
    }

    public class RecentTransaction
    {
        public long RecId { get; set; }
        public string CustomerNo { get; set; }
        public string OperatorName { get; set; }
        public string CircleName { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; }
        public DateTime RequestTime { get; set; }
        public string UserTxnId { get; set; }
        public decimal? Commission { get; set; }
    }

    public class ApiKeySettingsViewModel
    {
        public int UserId { get; set; }
        public string Username { get; set; }
        public string IpAddress { get; set; }
        public string Token { get; set; }
        public string CallbackUrl { get; set; }
        public bool IsVerified { get; set; }
    }

    public class OperatorwiseReportViewModel
    {
        public DateTime ReportDate { get; set; }
        public List<OperatorwiseReportItem> Items { get; set; }
        public int TotalCount { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal TotalCommission { get; set; }
        public decimal TotalSurcharge { get; set; }
    }

    public class OperatorwiseReportItem
    {
        public int SrNo { get; set; }
        public string Operator { get; set; }
        public int Count { get; set; }
        public decimal Amount { get; set; }
        public decimal Commission { get; set; }
        public decimal Surcharge { get; set; }
    }

    public class RechargeHistoryViewModel
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string Status { get; set; }
        public string SearchText { get; set; }
        public List<RechargeHistoryItem> Items { get; set; }
        public int TotalCount { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal TotalCommission { get; set; }
    }

    public class RechargeHistoryItem
    {
        public int SrNo { get; set; }
        public string TxnId { get; set; }
        public string Number { get; set; }
        public string Operator { get; set; }
        public decimal Amount { get; set; }
        public decimal Commission { get; set; }
        public string Status { get; set; }
        public DateTime Date { get; set; }
    }

    public class TransactionHistoryViewModel
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string Type { get; set; }
        public string SearchText { get; set; }
        public List<TransactionHistoryItem> Items { get; set; }
        public int TotalCount { get; set; }
        public decimal TotalCredit { get; set; }
        public decimal TotalDebit { get; set; }
    }

    public class TransactionHistoryItem
    {
        public int SrNo { get; set; }
        public string TxnId { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
        public decimal Credit { get; set; }
        public decimal Debit { get; set; }
        public decimal Balance { get; set; }
        public DateTime Date { get; set; }
    }
}

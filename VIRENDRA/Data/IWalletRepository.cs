using System.Collections.Generic;
using VIRENDRA.Models;

namespace VIRENDRA.Data
{
    public interface IWalletRepository
    {
        List<BankAccount> GetAllBankAccounts();
        void AddMoney(WalletTransaction txn);
        List<WalletTransaction> GetTransactions(int? userId = null);
    }
}

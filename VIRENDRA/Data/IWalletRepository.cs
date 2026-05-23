using System.Collections.Generic;
using VIRENDRA.Models;

namespace VIRENDRA.Data
{
    public interface IWalletRepository
    {
        List<BankAccount> GetAllBankAccounts();
        void AddMoney(WalletRequest req);
        List<WalletRequest> GetTransactions(int? userId = null);
    }
}

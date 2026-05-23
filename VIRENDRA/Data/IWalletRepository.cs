using System.Collections.Generic;
using VIRENDRA.Models;

namespace VIRENDRA.Data
{
    public interface IWalletRepository
    {
        // Bank Account CRUD
        List<BankAccount> GetAllBankAccounts();
        BankAccount GetBankAccountById(int id);
        int CreateBankAccount(BankAccount bank);
        void UpdateBankAccount(BankAccount bank);
        void DeleteBankAccount(int id);
        void ToggleBlockUser(int id, bool blockUser);

        // Wallet / Payment Request CRUD
        (bool Success, string Error, string Log) AddMoney(WalletRequest req);
        List<WalletRequest> GetTransactions(int? userId = null, int? statusId = null);
        WalletRequest GetWalletRequestById(int id);
        void UpdateWalletRequest(WalletRequest req);
        void DeleteWalletRequest(int id);
        void UpdateRequestStatus(int id, int statusId, int updatedById);
    }
}

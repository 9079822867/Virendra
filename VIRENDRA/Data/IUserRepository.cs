using System;
using System.Collections.Generic;
using VIRENDRA.Models;

namespace VIRENDRA.Data
{
    public interface IUserRepository
    {
        List<User> GetAllUsers();
        User GetUserByUsername(string username);
        User GetUserById(int id);
        void CreateUser(User user);
        void UpdateUser(User user);
        void DeleteUser(int id);
        void UpdateLoginIP(int userId, string ipAddress);
        void UpdateApiSettings(int userId, string ipAddress, string callbackUrl, string token);
        void UpdateRetryCount(int userId, int retryCount);
    }
}

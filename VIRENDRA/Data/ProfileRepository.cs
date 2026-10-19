using System.Configuration;
using System.Data.SqlClient;
using Dapper;
using VIRENDRA.Models;

namespace VIRENDRA.Data
{
    /// <summary>Logged-in user's own profile ([UserProfile], one row per UserId) and password.</summary>
    public class ProfileRepository
    {
        private readonly string _conn;

        public ProfileRepository()
        {
            _conn = ConfigurationManager.ConnectionStrings["sqlconn"]?.ConnectionString
                ?? throw new ConfigurationErrorsException("sqlconn missing in Web.config");
        }

        public UserProfileViewModel GetProfile(int userId)
        {
            using (var c = new SqlConnection(_conn))
                return c.QueryFirstOrDefault<UserProfileViewModel>(@"
                    SELECT u.Id, u.Username, r.RoleName, p.PackageName,
                           ISNULL(u.UserBal, 0) AS Balance,
                           up.FullName AS Name, up.Email AS EmailID, up.MobileNumber AS Mobileno,
                           up.PhoneNumber AS Contactno, up.ORGName, up.GSTNumber AS GSTNo,
                           up.GSTRegAddress AS GSTADDRESS, up.Address, up.StateID, up.City, up.Pincode
                    FROM [User] u
                    LEFT JOIN UserProfile up ON up.UserId = u.Id
                    LEFT JOIN [Role] r       ON r.Id = u.RoleId
                    LEFT JOIN Package p      ON p.Id = u.PackageId
                    WHERE u.Id = @UserId AND u.IsDeleted = 0",
                    new { UserId = userId });
        }

        /// <summary>Updates the UserProfile row, or creates it when the user has none yet.</summary>
        public void SaveProfile(int userId, UserProfileViewModel m)
        {
            using (var c = new SqlConnection(_conn))
                c.Execute(@"
                    IF EXISTS (SELECT 1 FROM UserProfile WHERE UserId = @UserId)
                        UPDATE UserProfile
                        SET FullName = @Name, Email = @EmailID, MobileNumber = @Mobileno,
                            PhoneNumber = @Contactno, ORGName = @ORGName, GSTNumber = @GSTNo,
                            GSTRegAddress = @GSTADDRESS, Address = @Address, StateID = @StateID,
                            City = @City, Pincode = @Pincode,
                            UpdatedDate = GETDATE(), UpdatedById = @UserId
                        WHERE UserId = @UserId
                    ELSE
                        INSERT INTO UserProfile
                            (UserId, FullName, Email, MobileNumber, PhoneNumber, ORGName, GSTNumber,
                             GSTRegAddress, Address, StateID, City, Pincode, AddedDate, AddedById, UserBalLock)
                        VALUES
                            (@UserId, @Name, @EmailID, @Mobileno, @Contactno, @ORGName, @GSTNo,
                             @GSTADDRESS, @Address, @StateID, @City, @Pincode, GETDATE(), @UserId, 0)",
                    new
                    {
                        UserId     = userId,
                        Name       = Clean(m.Name),
                        EmailID    = Clean(m.EmailID),
                        Mobileno   = Clean(m.Mobileno),
                        Contactno  = Clean(m.Contactno),
                        ORGName    = Clean(m.ORGName),
                        GSTNo      = Clean(m.GSTNo)?.ToUpperInvariant(),
                        GSTADDRESS = Clean(m.GSTADDRESS),
                        Address    = Clean(m.Address),
                        StateID    = m.StateID > 0 ? m.StateID : null,
                        City       = Clean(m.City),
                        Pincode    = Clean(m.Pincode)
                    });
        }

        public string GetPassword(int userId)
        {
            using (var c = new SqlConnection(_conn))
                return c.ExecuteScalar<string>(
                    "SELECT Password FROM [User] WHERE Id = @Id AND IsDeleted = 0", new { Id = userId });
        }

        public void UpdatePassword(int userId, string newPassword)
        {
            using (var c = new SqlConnection(_conn))
                c.Execute(@"
                    UPDATE [User]
                    SET Password = @Password, ResetCode = NULL, OTP = NULL,
                        UpdatedDate = GETDATE(), UpdatedById = @UserId
                    WHERE Id = @UserId",
                    new { UserId = userId, Password = newPassword });
        }

        private static string Clean(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }
}

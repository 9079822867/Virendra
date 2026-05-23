# Login Process with Dapper - Setup Guide for VIRENDRA (VRECHARGEDB)

## Overview
This implementation provides a complete login process using Dapper ORM with repository pattern and dependency injection, integrated with your existing VRECHARGEDB [User] table.

## Components Created

### 1. Data Access Layer (Repository Pattern)
- **IUserRepository.cs** - Interface defining data access operations
  - `GetUserByUsername(string username)` - Fetch user by username for login
  - `GetUserById(int id)` - Fetch user by ID
  - `UpdateRetryCount(int userId, int retryCount)` - Track failed login attempts
  - `UpdateLoginIP(int userId, string ipAddress)` - Log login IP address
  - Other CRUD operations

- **UserRepository.cs** - Dapper-based implementation for database operations
  - Uses parameterized queries (SQL injection safe)
  - Maps User entity to database columns

### 2. Models
- **User.cs** - User entity model matching VRECHARGEDB schema
  - Username (unique identifier for login)
  - Password
  - Status fields (IsActive, IsLocked, IsDeleted)
  - Account management fields (RetryCount, LoginIP, etc.)

- **LoginModel.cs** - Login form model with validation
  - Username field
  - Password field
  - RememberMe checkbox

### 3. Controller
- **AuthController.cs** - Login/Logout endpoints with security features
  - Account status validation (Active, Not Locked, Not Deleted)
  - Failed login attempt tracking
  - Account lockout after 5 failed attempts
  - IP address logging
  - Dependency injection of IUserRepository

### 4. Views
- **Views/Auth/Login.cshtml** - Bootstrap-styled login form

### 5. Configuration
- **DependencyInjectionConfig.cs** - Sets up dependency injection using custom controller factory
- **Global.asax.cs** - Initializes dependencies on application start

### 6. Database
- **Database/CreateUsersTable.sql** - Reference script (table already exists in VRECHARGEDB)

## Setup Steps

### 1. Install Dapper NuGet Package
```powershell
Install-Package Dapper
```

### 2. Configure Connection String
Update your `Web.config` to point to your VRECHARGEDB:
```xml
<connectionStrings>
  <add name="DefaultConnection" 
       connectionString="Server=YOUR_SERVER;Database=VRECHARGEDB;User Id=YOUR_USER;Password=YOUR_PASSWORD;" 
       providerName="System.Data.SqlClient" />
</connectionStrings>
```

### 3. Update DependencyInjectionConfig.cs
Ensure the connection string configuration matches your Web.config:
```csharp
var connectionString = ConfigurationManager.ConnectionStrings["DefaultConnection"]?.ConnectionString;
```

## Usage

### Login
- Navigate to `/Auth/Login`
- Enter username and password
- Click "Login"
- On successful authentication:
  - Forms authentication cookie is set
  - RetryCount is reset to 0
  - LoginIP is updated
  - User is redirected to dashboard

### Failed Login Attempts
- **1-4 failed attempts**: Show message with remaining attempts
- **5+ failed attempts**: Account is locked, show admin contact message

### Logout
- Navigate to `/Auth/Logout` (or click logout button)
- Authentication cookie is cleared
- User is redirected to login page

## Security Features Implemented

✅ **Implemented:**
- Account status validation (active, not locked, not deleted)
- Failed login attempt tracking
- Account lockout mechanism (5 attempts)
- IP address logging
- Parameterized queries (SQL injection prevention via Dapper)
- Anti-CSRF token validation
- Cookie-based authentication

⚠️ **Critical - NOT IMPLEMENTED (For Production):**
1. **Password Hashing**: Passwords are currently stored/compared in plain text
2. **HTTPS/TLS**: Use HTTPS in production only
3. **Account Recovery**: No password reset functionality
4. **Two-Factor Authentication**: Not implemented
5. **Session Timeout**: Not configured

## Production Security Checklist

### 1. Implement Password Hashing (REQUIRED)

Install BCrypt.Net-Next:
```powershell
Install-Package BCrypt.Net-Next
```

Update UserRepository to hash passwords:
```csharp
using BCrypt.Net;

// During user creation/password update:
string hashedPassword = BCrypt.Net.BCrypt.HashPassword(plainTextPassword);

// During login verification:
bool isValid = BCrypt.Net.BCrypt.Verify(model.Password, user.Password);
```

Update AuthController:
```csharp
// Replace plain text comparison with:
if (BCrypt.Net.BCrypt.Verify(model.Password, user.Password))
{
    // Login successful
}
```

### 2. Enable HTTPS
In Web.config:
```xml
<system.webServer>
  <security>
    <requestFiltering>
      <verbs>
        <add verb="*" allowed="true" />
      </verbs>
    </requestFiltering>
  </security>
  <rewrite>
    <rules>
      <rule name="Redirect to HTTPS" stopProcessing="true">
        <match url="(.*)" />
        <conditions>
          <add input="{HTTPS}" pattern="^OFF$" />
        </conditions>
        <action type="Redirect" url="https://{HTTP_HOST}{REQUEST_URI}" redirectType="Permanent" />
      </rule>
    </rules>
  </rewrite>
</system.webServer>
```

### 3. Configure Session & Cookie Security
In Web.config:
```xml
<system.web>
  <authentication mode="Forms">
    <forms loginUrl="/Auth/Login" timeout="30" requireSSL="true" httpOnlyCookies="true" />
  </authentication>
  <sessionState timeout="30" />
</system.web>
```

### 4. Add Authorization Filters
Create an AuthorizeAttribute for protected pages:
```csharp
[Authorize]
public ActionResult Dashboard()
{
    return View();
}
```

## Testing

### Test User Setup
Run this SQL to create a test user (use hashed password in production):
```sql
INSERT INTO [User] (Username, Password, RoleId, IsActive, IsLocked, IsDeleted, RetryCount, AddedDate, IsComm, IsOtpCheck)
VALUES ('testuser', 'password123', 1, 1, 0, 0, 0, GETDATE(), 1, 1)
```

### Test Login
1. Navigate to `http://localhost/Auth/Login`
2. Username: `testuser`
3. Password: `password123`
4. Click "Login"
5. Should redirect to dashboard

### Test Failed Attempts
1. Try login with wrong password 5 times
2. On 5th attempt, account should be locked
3. Verify `IsLocked = 1` in User table

## Troubleshooting

| Issue | Solution |
|-------|----------|
| Connection String Error | Verify DefaultConnection in Web.config exists and is correct |
| Dapper Not Found | Run `Install-Package Dapper` |
| User not found | Check username exists in [User] table with correct spelling |
| Password mismatch | Verify password stored is correct (consider hashing) |
| Cookie not set | Check FormsAuthentication is enabled in Web.config |
| Account locked unexpectedly | Check RetryCount in User table, reset to 0 to unlock |

## Related Tables & Fields

Your VRECHARGEDB already includes these useful related tables:
- **Role** - User roles for authorization
- **UserProfile** - Extended user information (email, mobile, address)
- **ActivityLog** - User activity tracking
- **TxnLedger** - User transaction history
- **WalletRequest** - User wallet transactions

You can extend the login system to utilize these existing tables for enhanced functionality.

## Next Steps

1. **Install Dapper** via NuGet
2. **Update Web.config** with connection string
3. **Test login** with existing user or create test user
4. **Implement password hashing** before production deployment
5. **Enable HTTPS** in production environment
6. **Add additional security measures** as needed

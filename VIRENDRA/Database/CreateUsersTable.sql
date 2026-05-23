-- Note: The User table already exists in VRECHARGEDB
-- This script documents the required table structure for login functionality

-- The [User] table structure (already created in your database):
/*
CREATE TABLE [dbo].[User](
    [Id] [int] IDENTITY(1,1) NOT NULL,
    [Username] [varchar](100) NOT NULL UNIQUE,
    [Password] [varchar](200) NOT NULL,
    [RoleId] [tinyint] NULL,
    [TokenAPI] [varchar](200) NULL,
    [IsActive] [bit] NOT NULL DEFAULT ((1)),
    [IsLocked] [bit] NOT NULL DEFAULT ((0)),
    [IsDeleted] [bit] NOT NULL DEFAULT ((0)),
    [RetryCount] [int] NOT NULL DEFAULT ((0)),
    [OTP] [varchar](10) NULL,
    [PassCode] [varchar](200) NULL,
    [LoginIP] [varchar](50) NULL,
    [AddedDate] [datetime2](7) NOT NULL DEFAULT (getdate()),
    [UpdatedDate] [datetime2](7) NULL,
    [AddedById] [int] NULL,
    [UpdatedById] [int] NULL,
    [CallbackURL] [varchar](200) NULL,
    [ResetCode] [varchar](200) NULL,
    [PackageId] [int] NULL,
    [HKey] [uniqueidentifier] NULL DEFAULT (newid()),
    [HPass] [uniqueidentifier] NULL DEFAULT (newid()),
    [UserBal] [decimal](18, 4) NULL,
    [ParentID] [int] NULL,
    [UserPin] [varchar](200) NULL,
    [AppToken] [nvarchar](200) NULL,
    [Firebasetoken] [nvarchar](1000) NULL,
    [ComplainCallbackURL] [varchar](500) NULL,
    [UserOutStandingBal] [decimal](18, 2) NULL,
    [IsComm] [bit] NOT NULL DEFAULT ((1)),
    [IsOtpCheck] [bit] NOT NULL DEFAULT ((1)),
    [IsJioActiveHigh] [bit] NULL DEFAULT ((0)),
    PRIMARY KEY CLUSTERED ([Id] ASC)
)
*/

-- Test data (insert a test user for login testing)
-- Password should be hashed in production. For testing only:
IF NOT EXISTS (SELECT 1 FROM [User] WHERE Username = 'testuser')
BEGIN
    INSERT INTO [User] (Username, Password, RoleId, IsActive, IsLocked, IsDeleted, RetryCount, AddedDate, IsComm, IsOtpCheck)
    VALUES ('testuser', 'password123', 1, 1, 0, 0, 0, GETDATE(), 1, 1)
END


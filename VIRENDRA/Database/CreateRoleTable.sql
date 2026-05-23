-- Role table
CREATE TABLE [dbo].[Role] (
    [Id]       [tinyint]      IDENTITY(1,1) NOT NULL,
    [RoleName] [varchar](100) NOT NULL,
    [IsActive] [bit]          NOT NULL DEFAULT (1),
    CONSTRAINT [PK_Role] PRIMARY KEY CLUSTERED ([Id] ASC)
);

-- Seed roles  (IDs must match RoleConstants.cs)
-- 1 = SuperAdmin, 2 = Admin, 3 = ApiUser, 4 = Retailer
SET IDENTITY_INSERT [dbo].[Role] ON;

INSERT INTO [dbo].[Role] (Id, RoleName, IsActive) VALUES
    (1, 'Super Admin', 1),
    (2, 'Admin',       1),
    (3, 'API User',    1),
    (4, 'Retailer',    1);

SET IDENTITY_INSERT [dbo].[Role] OFF;

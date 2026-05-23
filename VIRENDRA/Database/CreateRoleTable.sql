-- Role table
CREATE TABLE [dbo].[Role] (
    [Id]       [tinyint]     IDENTITY(1,1) NOT NULL,
    [RoleName] [varchar](100) NOT NULL,
    [IsActive] [bit]          NOT NULL DEFAULT (1),
    CONSTRAINT [PK_Role] PRIMARY KEY CLUSTERED ([Id] ASC)
);

-- Seed default roles
INSERT INTO [dbo].[Role] (RoleName, IsActive) VALUES
    ('Admin',       1),
    ('Retailer',    1),
    ('Distributor', 1),
    ('Super Distributor', 1),
    ('API User',    1);

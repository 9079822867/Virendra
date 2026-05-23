-- ============================================================
-- Package table
-- ============================================================
CREATE TABLE [dbo].[Package] (
    [Id]          INT           IDENTITY(1,1) NOT NULL,
    [PackageName] VARCHAR(100)  NOT NULL,
    [Description] VARCHAR(500)  NULL,
    [IsActive]    BIT           NOT NULL DEFAULT (1),
    [AddedDate]   DATETIME2(7)  NOT NULL DEFAULT (GETDATE()),
    CONSTRAINT [PK_Package] PRIMARY KEY CLUSTERED ([Id] ASC)
);

-- ============================================================
-- Operator table
-- ============================================================
CREATE TABLE [dbo].[Operator] (
    [Id]           INT           IDENTITY(1,1) NOT NULL,
    [OperatorName] VARCHAR(100)  NOT NULL,
    [OperatorCode] VARCHAR(50)   NULL,
    [OperatorType] VARCHAR(50)   NULL,   -- Mobile / DTH / Utility
    [IsActive]     BIT           NOT NULL DEFAULT (1),
    [AddedDate]    DATETIME2(7)  NOT NULL DEFAULT (GETDATE()),
    CONSTRAINT [PK_Operator] PRIMARY KEY CLUSTERED ([Id] ASC)
);

-- ============================================================
-- PackageComm  (commission per Package x Operator)
-- ============================================================
CREATE TABLE [dbo].[PackageComm] (
    [Id]         INT            IDENTITY(1,1) NOT NULL,
    [PackageId]  INT            NOT NULL,
    [OperatorId] INT            NOT NULL,
    [CommType]   VARCHAR(1)     NOT NULL DEFAULT ('P'),  -- P=Percent  F=Flat
    [CommValue]  DECIMAL(18,4)  NOT NULL DEFAULT (0),
    [IsActive]   BIT            NOT NULL DEFAULT (1),
    CONSTRAINT [PK_PackageComm]        PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_PackageComm_Pkg]    FOREIGN KEY ([PackageId])  REFERENCES [Package]([Id]),
    CONSTRAINT [FK_PackageComm_Op]     FOREIGN KEY ([OperatorId]) REFERENCES [Operator]([Id]),
    CONSTRAINT [UQ_PackageComm]        UNIQUE ([PackageId], [OperatorId])
);

-- ============================================================
-- Seed operators
-- ============================================================
INSERT INTO [dbo].[Operator] (OperatorName, OperatorCode, OperatorType, IsActive) VALUES
    ('Reliance Jio',        'JIO',   'Mobile', 1),
    ('Airtel',              'AIR',   'Mobile', 1),
    ('VI (Vodafone-Idea)',  'VI',    'Mobile', 1),
    ('BSNL',                'BSNL',  'Mobile', 1),
    ('Airtel DTH',          'ADTH',  'DTH',    1),
    ('Tata Play',           'TATA',  'DTH',    1),
    ('Dish TV',             'DISH',  'DTH',    1);

-- ============================================================
-- Seed packages
-- ============================================================
INSERT INTO [dbo].[Package] (PackageName, Description, IsActive) VALUES
    ('Default',    'Default commission package', 1),
    ('Premium',    'Higher commission rates',    1),
    ('Gold',       'Gold retailer package',      1);

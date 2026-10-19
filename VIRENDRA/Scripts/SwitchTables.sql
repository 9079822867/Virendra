-- Tables for Switch > Stop Route Message and Operator > Validation (ported from STC).
-- Safe to run more than once.
SET NOCOUNT ON;

IF OBJECT_ID('dbo.StopRouteMessage', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.StopRouteMessage
    (
        Id          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_StopRouteMessage PRIMARY KEY,
        OpFilter    VARCHAR(MAX) NULL,      -- 'All' or comma-separated Operator.Id
        ApiFilter   VARCHAR(MAX) NULL,      -- 'All' or comma-separated ApiSource.Id
        MsgFilter   VARCHAR(MAX) NULL,      -- ';'-separated texts; a vendor reply containing one stops switching
        AddedDate   DATETIME2 NULL CONSTRAINT DF_StopRouteMessage_AddedDate DEFAULT (GETDATE()),
        AddedById   INT NULL,
        UpdatedDate DATETIME2 NULL,
        UpdatedById INT NULL
    );
END

IF OBJECT_ID('dbo.OperatorValidation', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.OperatorValidation
    (
        Id              INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_OperatorValidation PRIMARY KEY,
        OpId            INT NULL CONSTRAINT FK_OperatorValidation_Operator REFERENCES dbo.Operator(Id),
        ColumnName      VARCHAR(50) NULL,   -- Number | Account | Amount
        MinLenth        INT NULL,
        MaxLenth        INT NULL,
        MinVal          INT NULL,
        MaxVal          INT NULL,
        StartWith       VARCHAR(100) NULL,  -- comma-separated allowed prefixes
        EndWith         VARCHAR(100) NULL,
        IsNum           BIT NULL,
        IsString        BIT NULL,
        ErrorMsg        VARCHAR(250) NULL,
        IsMandatory     BIT NULL,
        PlaceHolderText VARCHAR(100) NULL,
        AddedById       INT NULL,
        AddedDate       DATETIME2 NULL CONSTRAINT DF_OperatorValidation_AddedDate DEFAULT (GETDATE()),
        UpdatedDate     DATETIME2 NULL,
        UpdatedById     INT NULL
    );
    CREATE INDEX IX_OperatorValidation_OpId ON dbo.OperatorValidation (OpId, ColumnName);
END

SELECT name FROM sys.tables WHERE name IN ('StopRouteMessage', 'OperatorValidation');

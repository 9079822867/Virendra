-- =============================================
-- Vendor & VendorUrl Tables
-- Run on: VRECHARGEDB
-- =============================================

IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='Vendor' AND xtype='U')
BEGIN
    CREATE TABLE Vendor (
        Id                INT IDENTITY(1,1) PRIMARY KEY,
        VendorName        VARCHAR(200)  NOT NULL,
        VendorType        VARCHAR(50)   NULL,
        LoginId           VARCHAR(200)  NULL,
        Password          VARCHAR(200)  NULL,
        Optional          VARCHAR(500)  NULL,
        IsAutoStatusCheck BIT           NOT NULL DEFAULT 0,
        CheckTime         INT           NOT NULL DEFAULT 0,
        Balance           DECIMAL(18,2) NOT NULL DEFAULT 0,
        VBal              DECIMAL(18,2) NOT NULL DEFAULT 0,
        Remark            VARCHAR(500)  NULL,
        IsActive          BIT           NOT NULL DEFAULT 1,
        AddedDate         DATETIME      NOT NULL DEFAULT GETDATE()
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='VendorUrl' AND xtype='U')
BEGIN
    CREATE TABLE VendorUrl (
        Id            INT IDENTITY(1,1) PRIMARY KEY,
        VendorId      INT           NOT NULL,
        UrlType       VARCHAR(50)   NOT NULL,   -- RechargeRequest | BalanceCheck | StatusCheck | CallBack | Validation | ComplainRequest
        Url           VARCHAR(2000) NULL,
        Method        VARCHAR(10)   NOT NULL DEFAULT 'GET',
        ResponseType  VARCHAR(100)  NULL,
        PostParameter VARCHAR(2000) NULL,

        CONSTRAINT FK_VendorUrl_Vendor FOREIGN KEY (VendorId) REFERENCES Vendor(Id),
        CONSTRAINT UQ_VendorUrl       UNIQUE (VendorId, UrlType)
    );
END
GO

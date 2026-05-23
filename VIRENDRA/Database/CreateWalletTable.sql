-- =============================================
-- Wallet Module Tables
-- Run on: VRECHARGEDB
-- =============================================

-- Bank Account master table
IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='BankAccount' AND xtype='U')
BEGIN
    CREATE TABLE BankAccount (
        Id            INT IDENTITY(1,1) PRIMARY KEY,
        AccountName   VARCHAR(200)  NOT NULL,
        AccountNumber VARCHAR(50)   NOT NULL,
        BankName      VARCHAR(200)  NOT NULL,
        IsActive      BIT           NOT NULL DEFAULT 1,
        AddedDate     DATETIME      NOT NULL DEFAULT GETDATE()
    );

    -- Sample records
    INSERT INTO BankAccount (AccountName, AccountNumber, BankName) VALUES
        ('Sarita Gupta',  '001234567890', 'State Bank of India'),
        ('Virendra Main', '009876543210', 'HDFC Bank');
END
GO

-- Wallet Transaction table
IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='WalletTransaction' AND xtype='U')
BEGIN
    CREATE TABLE WalletTransaction (
        Id             INT IDENTITY(1,1) PRIMARY KEY,
        WalletType     VARCHAR(20)    NOT NULL,            -- 'Main' | 'BillPayment'
        UserId         INT            NOT NULL,
        Amount         DECIMAL(18,4)  NOT NULL,
        IsPullOut      BIT            NOT NULL DEFAULT 0,
        IsCredit       BIT            NOT NULL DEFAULT 0,
        IsDebit        BIT            NOT NULL DEFAULT 0,
        SMS            BIT            NOT NULL DEFAULT 0,
        TransferType   VARCHAR(20)    NULL,                -- IMPS, NEFT, RTGS, UPI, Cash, Cheque
        PaymentDate    DATE           NULL,
        PaymentRemark  VARCHAR(500)   NULL,
        BankAccountId  INT            NULL,
        ChequeRefNo    VARCHAR(100)   NOT NULL,
        Remark         VARCHAR(500)   NOT NULL,
        AddedDate      DATETIME       NOT NULL DEFAULT GETDATE(),
        AddedBy        INT            NULL,

        CONSTRAINT FK_WalletTxn_User        FOREIGN KEY (UserId)        REFERENCES [User](Id),
        CONSTRAINT FK_WalletTxn_BankAccount FOREIGN KEY (BankAccountId) REFERENCES BankAccount(Id)
    );
END
GO

-- =============================================
-- RechargeTransaction + UserLedger Tables
-- Run on: VRECHARGEDB
-- =============================================

-- Recharge history table
IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='RechargeTransaction' AND xtype='U')
BEGIN
    CREATE TABLE RechargeTransaction (
        RecId        BIGINT IDENTITY(1,1) PRIMARY KEY,
        UserId       INT            NOT NULL,
        UserTxnId    VARCHAR(50)    NOT NULL,       -- user-facing transaction ID
        VendorTxnId  VARCHAR(100)   NULL,           -- vendor's transaction ID
        CustomerNo   VARCHAR(20)    NOT NULL,       -- mobile / account number
        OperatorId   INT            NULL,
        OperatorName VARCHAR(200)   NULL,
        CircleId     INT            NULL,
        CircleName   VARCHAR(100)   NULL,
        Amount       DECIMAL(18,4)  NOT NULL,
        Commission   DECIMAL(18,4)  NULL DEFAULT 0,
        Surcharge    DECIMAL(18,4)  NULL DEFAULT 0,
        Status       VARCHAR(20)    NOT NULL DEFAULT 'Pending',  -- Success | Pending | Failed
        Remark       VARCHAR(500)   NULL,
        RequestTime  DATETIME       NOT NULL DEFAULT GETDATE(),
        ResponseTime DATETIME       NULL,

        CONSTRAINT FK_RechTxn_User FOREIGN KEY (UserId) REFERENCES [User](Id)
    );

    CREATE INDEX IX_RechargeTransaction_UserId      ON RechargeTransaction (UserId);
    CREATE INDEX IX_RechargeTransaction_RequestTime ON RechargeTransaction (RequestTime);
    CREATE INDEX IX_RechargeTransaction_Status      ON RechargeTransaction (Status);
END
GO

-- Wallet / ledger table
IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='UserLedger' AND xtype='U')
BEGIN
    CREATE TABLE UserLedger (
        Id          BIGINT IDENTITY(1,1) PRIMARY KEY,
        UserId      INT            NOT NULL,
        TxnId       VARCHAR(50)    NOT NULL,
        Type        VARCHAR(30)    NOT NULL,        -- Credit | Debit | Commission | Refund | Adjustment
        Description VARCHAR(500)   NULL,
        Credit      DECIMAL(18,4)  NOT NULL DEFAULT 0,
        Debit       DECIMAL(18,4)  NOT NULL DEFAULT 0,
        Balance     DECIMAL(18,4)  NOT NULL DEFAULT 0,
        RefId       VARCHAR(50)    NULL,            -- links to RechargeTransaction.UserTxnId
        AddedDate   DATETIME       NOT NULL DEFAULT GETDATE(),

        CONSTRAINT FK_UserLedger_User FOREIGN KEY (UserId) REFERENCES [User](Id)
    );

    CREATE INDEX IX_UserLedger_UserId    ON UserLedger (UserId);
    CREATE INDEX IX_UserLedger_AddedDate ON UserLedger (AddedDate);
    CREATE INDEX IX_UserLedger_Type      ON UserLedger (Type);
END
GO

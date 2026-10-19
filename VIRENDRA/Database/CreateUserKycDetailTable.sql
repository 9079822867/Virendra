-- =============================================
-- User KYC identity numbers (Aadhaar + PAN), one row per user
-- Run on: VRECHARGEDB (after CreateUserKycTable.sql)
--
-- *StatusId  : 1 Pending | 2 Verified | 3 Rejected        (Models/KycConstants.cs)
-- *VerifyMode: 'API' (verified by the KYC API) | 'Manual' (verified / updated by an admin)
-- KYC is approved only when all 4 UserKyc documents are approved AND both numbers are verified.
-- =============================================

IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='UserKycDetail' AND xtype='U')
BEGIN
    CREATE TABLE UserKycDetail (
        UserId              INT           NOT NULL PRIMARY KEY,

        AadhaarNumber       VARCHAR(12)   NULL,
        AadhaarStatusId     INT           NULL,
        AadhaarVerifyMode   VARCHAR(10)   NULL,
        AadhaarRemark       VARCHAR(250)  NULL,
        AadhaarVerifiedDate DATETIME2     NULL,

        PanNumber           VARCHAR(10)   NULL,
        PanName             VARCHAR(100)  NULL,
        PanDob              DATE          NULL,
        PanStatusId         INT           NULL,
        PanVerifyMode       VARCHAR(10)   NULL,
        PanRemark           VARCHAR(250)  NULL,
        PanVerifiedDate     DATETIME2     NULL,

        ActionById          INT           NULL,
        AddedDate           DATETIME2     NOT NULL DEFAULT GETDATE(),
        UpdatedDate         DATETIME2     NULL
    );

    CREATE INDEX IX_UserKycDetail_Aadhaar ON UserKycDetail (AadhaarNumber);
    CREATE INDEX IX_UserKycDetail_Pan     ON UserKycDetail (PanNumber);
END
GO

-- =============================================
-- User KYC documents
-- Run on: VRECHARGEDB
--
-- One row per (UserId, Category). Re-uploading a rejected / pending document
-- updates the same row.
--
-- Category : AadhaarFront | AadhaarBack | PanCard | Selfie   (all 4 required)
-- StatusId : 1 Pending | 2 Approved | 3 Rejected             (Models/KycConstants.cs)
-- ImgPath  : file name only, e.g. "3f2b...e9.jpg" (stored under ~/App_Data/Kyc/{UserId}/,
--            which IIS never serves; files are streamed by KycController.Document)
--
-- Columns requested: Id, UserId, Category, ImgPath, StatusId, AddedbyDate
-- Extra columns    : Remark (rejection reason), ActionById / ActionDate (admin who
--                    approved/rejected and when)
-- =============================================

IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='UserKyc' AND xtype='U')
BEGIN
    CREATE TABLE UserKyc (
        Id           INT IDENTITY(1,1) PRIMARY KEY,
        UserId       INT           NOT NULL,
        Category     VARCHAR(50)   NOT NULL,
        ImgPath      VARCHAR(50)   NOT NULL,
        StatusId     INT           NOT NULL DEFAULT 1,
        AddedbyDate  DATETIME2     NOT NULL DEFAULT GETDATE(),
        Remark       VARCHAR(250)  NULL,
        ActionById   INT           NULL,
        ActionDate   DATETIME2     NULL
    );

    CREATE UNIQUE INDEX UX_UserKyc_UserId_Category ON UserKyc (UserId, Category);
END
GO

-- =============================================
-- usp_InsertPendingRecharge
-- Creates the Recharge row (Process status), writes the TxnLedger debit
-- and deducts User.UserBal in one transaction.
-- Run on: VRECHARGEDB
--
-- @ErrorCode: 0 = OK, 9 = Insufficient balance, -1 = SQL error
-- =============================================

IF OBJECT_ID('dbo.usp_InsertPendingRecharge', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_InsertPendingRecharge;
GO

CREATE PROCEDURE dbo.usp_InsertPendingRecharge
    @UserId      INT,
    @OpId        INT,
    @CircleId    INT,
    @CustomerNo  VARCHAR(50),
    @Amount      DECIMAL(18, 4),
    @RCTypeId    TINYINT,
    @UserTxnId   VARCHAR(50),
    @ApiId       INT,
    @AddedById   INT,
    @StatusId    INT = 3,                 -- Recharge status for a new request (Process)

    @RecId       BIGINT          OUTPUT,
    @TxnId       BIGINT          OUTPUT,
    @OpBal       DECIMAL(18, 4)  OUTPUT,
    @ErrorCode   INT             OUTPUT,
    @ErrorDesc   VARCHAR(250)    OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SELECT @RecId = 0, @TxnId = 0, @OpBal = 0, @ErrorCode = 0, @ErrorDesc = 'OK';

    BEGIN TRY
        BEGIN TRANSACTION;

        SELECT @OpBal = ISNULL(UserBal, 0)
        FROM [User] WITH (UPDLOCK, ROWLOCK)
        WHERE Id = @UserId;

        IF @OpBal < @Amount
        BEGIN
            ROLLBACK TRANSACTION;
            SET @ErrorCode = 9;
            SET @ErrorDesc = 'Insufficient balance. Available: '
                           + CONVERT(VARCHAR(30), CAST(@OpBal AS DECIMAL(18, 2)));
            RETURN;
        END

        INSERT INTO Recharge
            (UserId, ApiId, CustomerNo, OpId, CircleId, Amount, RCTypeId,
             StatusId, RequestTime, MediumId, UserTxnId, OurRefTxnId, cashback)
        VALUES
            (@UserId, @ApiId, @CustomerNo, @OpId, @CircleId, @Amount, @RCTypeId,
             @StatusId, GETDATE(), 1, @UserTxnId, @UserTxnId, 0);

        SET @RecId = CAST(SCOPE_IDENTITY() AS BIGINT);

        INSERT INTO TxnLedger
            (RecId, UserId, RefTxnId, TxnDate, OP_Bal, CR_Amt, DB_Amt,
             TxnTypeId, AmtTypeId, Remark, AddedById)
        VALUES
            (@RecId, @UserId, @UserTxnId, GETDATE(), @OpBal, 0, @Amount,
             1, 1, 'Recharge Debit - ' + @CustomerNo, @AddedById);

        SET @TxnId = CAST(SCOPE_IDENTITY() AS BIGINT);

        UPDATE Recharge SET TxnId = @TxnId WHERE Id = @RecId;

        UPDATE [User] SET UserBal = UserBal - @Amount WHERE Id = @UserId;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;

        SELECT @RecId = 0, @TxnId = 0;
        SET @ErrorCode = -1;
        SET @ErrorDesc = LEFT(ERROR_MESSAGE(), 250);
    END CATCH
END
GO

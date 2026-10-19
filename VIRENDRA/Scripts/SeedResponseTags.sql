-- Tag names used on Vendor > Edit > (URL) > Setting (TagValue rows).
-- Safe to run more than once: only adds names that are missing.
SET NOCOUNT ON;

DECLARE @tags TABLE (Name VARCHAR(50));
INSERT INTO @tags (Name) VALUES
    ('SUCCESS'),        -- Check Value = vendor's success value
    ('FAILED'),         -- Check Value = vendor's failure value
    ('PENDING'),        -- Check Value = vendor's pending / processing value
    ('RequestTxnID'),   -- our reference echoed back by the vendor
    ('ApiTxnID'),       -- vendor's own transaction id
    ('OperatorTxnID'),  -- operator reference
    ('Message'),        -- status message text
    ('Balance'),        -- vendor balance after the call
    ('LapuNo'),
    ('ROffer');

INSERT INTO Tag (Name)
SELECT t.Name FROM @tags t
WHERE NOT EXISTS (SELECT 1 FROM Tag g WHERE g.Name = t.Name);

SELECT Id, Name FROM Tag ORDER BY Id;

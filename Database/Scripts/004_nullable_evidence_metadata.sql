-- Apply to existing databases before deploying metadata-only evidence creation.
-- The bootstrap script creates new databases with these columns nullable.
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.Evidence', N'SharePointItemId') IS NULL
   OR COL_LENGTH(N'dbo.Evidence', N'SharePointUrl') IS NULL
   OR COL_LENGTH(N'dbo.Evidence', N'FileHash') IS NULL
    THROW 50000, 'Evidence table or required metadata columns are missing.', 1;

ALTER TABLE dbo.Evidence ALTER COLUMN SharePointItemId NVARCHAR(255) NULL;
ALTER TABLE dbo.Evidence ALTER COLUMN SharePointUrl NVARCHAR(500) NULL;
ALTER TABLE dbo.Evidence ALTER COLUMN FileHash CHAR(64) NULL;
COMMIT TRANSACTION;

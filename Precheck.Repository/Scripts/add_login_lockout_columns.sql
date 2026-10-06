-- Brute-force lockout support. Run this BEFORE deploying the new API build.
IF COL_LENGTH('dbo.tbl_users', 'failedloginattempts') IS NULL
    ALTER TABLE dbo.tbl_users ADD failedloginattempts INT NOT NULL CONSTRAINT DF_tbl_users_failedloginattempts DEFAULT 0;

IF COL_LENGTH('dbo.tbl_users', 'lockoutend') IS NULL
    ALTER TABLE dbo.tbl_users ADD lockoutend DATETIME NULL;

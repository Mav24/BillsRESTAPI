-- ========================================
-- Migration Script for SQL Server: AddHouseholdSupport
-- Run this script on your production SQL Server database
-- ========================================

-- Switch to the correct database
USE [BillsDb];
GO

-- First, verify we're on the correct database
PRINT 'Current Database: ' + DB_NAME();
PRINT '';

-- Check if Users table exists
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Users')
BEGIN
    PRINT 'ERROR: Users table not found in database ' + DB_NAME();
    PRINT 'Please verify you are connected to the correct database.';
    PRINT 'Available tables:';
    SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE';
    RETURN; -- Exit script
END

BEGIN TRANSACTION;

-- Step 1: Create Households table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Households')
BEGIN
    CREATE TABLE [Households] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        [Name] NVARCHAR(100) NOT NULL,
        [CreatedAt] DATETIME2 NOT NULL
    );
    PRINT 'Created Households table';
END

-- Step 2: Add Email column to Users if not exists
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Users]') AND name = 'Email')
BEGIN
    ALTER TABLE [Users] ADD [Email] NVARCHAR(256) NOT NULL DEFAULT '';
    CREATE UNIQUE INDEX [IX_Users_Email] ON [Users] ([Email]);
    PRINT 'Added Email column to Users';
END

-- Step 3: Add HouseholdId to Users
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Users]') AND name = 'HouseholdId')
BEGIN
    ALTER TABLE [Users] ADD [HouseholdId] UNIQUEIDENTIFIER NULL;
    CREATE INDEX [IX_Users_HouseholdId] ON [Users] ([HouseholdId]);
    PRINT 'Added HouseholdId to Users';
END

-- Step 4: Add HouseholdId to Bills
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Bills]') AND name = 'HouseholdId')
BEGIN
    ALTER TABLE [Bills] ADD [HouseholdId] UNIQUEIDENTIFIER NULL;
    CREATE INDEX [IX_Bills_HouseholdId] ON [Bills] ([HouseholdId]);
    PRINT 'Added HouseholdId to Bills';
END

-- Step 5: Add IsPaid to Bills if not exists
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Bills]') AND name = 'IsPaid')
BEGIN
    ALTER TABLE [Bills] ADD [IsPaid] BIT NOT NULL DEFAULT 0;
    PRINT 'Added IsPaid to Bills';
END

-- Step 6: Add PaidDate to Bills if not exists
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Bills]') AND name = 'PaidDate')
BEGIN
    ALTER TABLE [Bills] ADD [PaidDate] DATETIME2 NULL;
    PRINT 'Added PaidDate to Bills';
END

-- Step 7: Create RefreshTokens table if not exists
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'RefreshTokens')
BEGIN
    CREATE TABLE [RefreshTokens] (
        [Id] NVARCHAR(450) NOT NULL PRIMARY KEY,
        [Token] NVARCHAR(256) NOT NULL,
        [UserId] NVARCHAR(450) NOT NULL,
        [ExpiresAt] DATETIME2 NOT NULL,
        [CreatedAt] DATETIME2 NOT NULL,
        [IsRevoked] BIT NOT NULL,
        CONSTRAINT [FK_RefreshTokens_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
    CREATE UNIQUE INDEX [IX_RefreshTokens_Token] ON [RefreshTokens] ([Token]);
    CREATE INDEX [IX_RefreshTokens_UserId] ON [RefreshTokens] ([UserId]);
    PRINT 'Created RefreshTokens table';
END

-- Step 8: Create PasswordResetTokens table if not exists
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PasswordResetTokens')
BEGIN
    CREATE TABLE [PasswordResetTokens] (
        [Id] NVARCHAR(450) NOT NULL PRIMARY KEY,
        [UserId] NVARCHAR(450) NOT NULL,
        [TokenHash] NVARCHAR(128) NOT NULL,
        [ExpiresAt] DATETIME2 NOT NULL,
        [Used] BIT NOT NULL DEFAULT 0,
        CONSTRAINT [FK_PasswordResetTokens_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_PasswordResetTokens_UserId] ON [PasswordResetTokens] ([UserId]);
    PRINT 'Created PasswordResetTokens table';
END

-- Step 9: Add foreign key constraints for Households
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Users_Households_HouseholdId')
BEGIN
    ALTER TABLE [Users] ADD CONSTRAINT [FK_Users_Households_HouseholdId] 
        FOREIGN KEY ([HouseholdId]) REFERENCES [Households] ([Id]) ON DELETE SET NULL;
    PRINT 'Added FK constraint for Users -> Households';
END

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Bills_Households_HouseholdId')
BEGIN
    ALTER TABLE [Bills] ADD CONSTRAINT [FK_Bills_Households_HouseholdId] 
        FOREIGN KEY ([HouseholdId]) REFERENCES [Households] ([Id]) ON DELETE SET NULL;
    PRINT 'Added FK constraint for Bills -> Households';
END

-- Step 10: Update migration history
IF NOT EXISTS (SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20260214191651_AddHouseholdSupport')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES ('20260214191651_AddHouseholdSupport', '10.0.1');
    PRINT 'Updated migration history';
END

COMMIT TRANSACTION;

PRINT 'Migration completed successfully!';

-- ========================================
-- Verification queries (run these to verify the changes)
-- ========================================
PRINT '';
PRINT 'Verification Results:';
SELECT 'Households Table' AS [Object], COUNT(*) AS [Count] FROM sys.tables WHERE name = 'Households';
SELECT 'RefreshTokens Table' AS [Object], COUNT(*) AS [Count] FROM sys.tables WHERE name = 'RefreshTokens';
SELECT 'PasswordResetTokens Table' AS [Object], COUNT(*) AS [Count] FROM sys.tables WHERE name = 'PasswordResetTokens';
SELECT 'Users.HouseholdId Column' AS [Object], COUNT(*) AS [Count] FROM sys.columns WHERE object_id = OBJECT_ID(N'[Users]') AND name = 'HouseholdId';
SELECT 'Bills.HouseholdId Column' AS [Object], COUNT(*) AS [Count] FROM sys.columns WHERE object_id = OBJECT_ID(N'[Bills]') AND name = 'HouseholdId';
SELECT 'Total Users' AS [Object], COUNT(*) AS [Count] FROM Users;
SELECT 'Total Bills' AS [Object], COUNT(*) AS [Count] FROM Bills;

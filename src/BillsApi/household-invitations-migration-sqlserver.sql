-- ========================================
-- Migration Script for SQL Server: AddHouseholdInvitations
-- Run this script on your production SQL Server database (BillsDb)
-- ========================================

-- Switch to the correct database
USE [BillsDb];
GO

-- Verify we're on the correct database
PRINT 'Current Database: ' + DB_NAME();
PRINT '';

BEGIN TRANSACTION;

-- Step 1: Create HouseholdInvitations table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'HouseholdInvitations')
BEGIN
    CREATE TABLE [HouseholdInvitations] (
        [Id] NVARCHAR(450) NOT NULL PRIMARY KEY,
        [HouseholdId] UNIQUEIDENTIFIER NOT NULL,
        [Email] NVARCHAR(256) NOT NULL,
        [TokenHash] NVARCHAR(128) NOT NULL,
        [InvitedByUserId] NVARCHAR(450) NOT NULL,
        [CreatedAt] DATETIME2 NOT NULL,
        [ExpiresAt] DATETIME2 NOT NULL,
        [Accepted] BIT NOT NULL DEFAULT 0,
        [AcceptedAt] DATETIME2 NULL,
        CONSTRAINT [FK_HouseholdInvitations_Households_HouseholdId] 
            FOREIGN KEY ([HouseholdId]) REFERENCES [Households] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_HouseholdInvitations_Users_InvitedByUserId] 
            FOREIGN KEY ([InvitedByUserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
    
    -- Create indexes for better query performance
    CREATE INDEX [IX_HouseholdInvitations_Email] ON [HouseholdInvitations] ([Email]);
    CREATE INDEX [IX_HouseholdInvitations_HouseholdId] ON [HouseholdInvitations] ([HouseholdId]);
    CREATE INDEX [IX_HouseholdInvitations_InvitedByUserId] ON [HouseholdInvitations] ([InvitedByUserId]);
    
    PRINT 'Created HouseholdInvitations table with indexes';
END
ELSE
BEGIN
    PRINT 'HouseholdInvitations table already exists, skipping creation';
END

-- Step 2: Update migration history
IF NOT EXISTS (SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] LIKE '2026%_AddHouseholdInvitations')
BEGIN
    -- Get the actual migration ID from the migrations folder
    DECLARE @MigrationId NVARCHAR(150);
    SELECT TOP 1 @MigrationId = name 
    FROM sys.objects 
    WHERE name LIKE '%AddHouseholdInvitations%';
    
    -- Use a placeholder if we can't determine it automatically
    IF @MigrationId IS NULL
        SET @MigrationId = '20260214000000_AddHouseholdInvitations';
    
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (@MigrationId, '10.0.1');
    
    PRINT 'Updated migration history';
END
ELSE
BEGIN
    PRINT 'Migration history already contains AddHouseholdInvitations';
END

COMMIT TRANSACTION;

PRINT '';
PRINT 'Migration completed successfully!';
PRINT '';

-- ========================================
-- Verification queries
-- ========================================
PRINT 'Verification Results:';
PRINT '';

-- Check if table exists
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'HouseholdInvitations')
BEGIN
    PRINT '? HouseholdInvitations table exists';
    
    -- Check indexes
    SELECT '? Index: ' + name AS [Status]
    FROM sys.indexes
    WHERE object_id = OBJECT_ID('HouseholdInvitations') AND name IS NOT NULL;
    
    -- Check foreign keys
    SELECT '? Foreign Key: ' + name AS [Status]
    FROM sys.foreign_keys
    WHERE parent_object_id = OBJECT_ID('HouseholdInvitations');
    
    -- Check row count (should be 0 initially)
    DECLARE @InvitationCount INT;
    SELECT @InvitationCount = COUNT(*) FROM HouseholdInvitations;
    PRINT '? Current invitation count: ' + CAST(@InvitationCount AS NVARCHAR(10));
END
ELSE
BEGIN
    PRINT '? HouseholdInvitations table does NOT exist - migration failed!';
END

PRINT '';
PRINT 'Summary of all household-related tables:';
SELECT 
    TABLE_NAME AS [Table],
    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = t.TABLE_NAME) AS [Columns]
FROM INFORMATION_SCHEMA.TABLES t
WHERE TABLE_NAME IN ('Households', 'HouseholdInvitations', 'Users', 'Bills')
ORDER BY TABLE_NAME;

PRINT '';
PRINT '--- Migration Complete ---';
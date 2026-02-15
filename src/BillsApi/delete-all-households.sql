-- ========================================
-- Script to Delete All Households
-- Run this on your production SQL Server database (BillsDb)
-- WARNING: This will remove all households and reset users/bills to personal mode
-- ========================================

USE [BillsDb];
GO

-- Verify we're on the correct database
PRINT 'Current Database: ' + DB_NAME();
PRINT '';

BEGIN TRANSACTION;

-- Get count before deletion
DECLARE @HouseholdCount INT;
DECLARE @InvitationCount INT;

SELECT @HouseholdCount = COUNT(*) FROM Households;
SELECT @InvitationCount = COUNT(*) FROM HouseholdInvitations;

PRINT 'Before deletion:';
PRINT '  Households: ' + CAST(@HouseholdCount AS NVARCHAR(10));
PRINT '  Invitations: ' + CAST(@InvitationCount AS NVARCHAR(10));
PRINT '';

-- Step 1: Delete all household invitations (will cascade automatically, but doing explicitly for clarity)
DELETE FROM HouseholdInvitations;
PRINT 'Deleted all household invitations';

-- Step 2: Delete all households
-- This will automatically set HouseholdId to NULL in Users and Bills tables (SetNull foreign key constraint)
DELETE FROM Households;
PRINT 'Deleted all households';

-- Verify deletion
SELECT @HouseholdCount = COUNT(*) FROM Households;
SELECT @InvitationCount = COUNT(*) FROM HouseholdInvitations;

PRINT '';
PRINT 'After deletion:';
PRINT '  Households: ' + CAST(@HouseholdCount AS NVARCHAR(10));
PRINT '  Invitations: ' + CAST(@InvitationCount AS NVARCHAR(10));
PRINT '';

-- Check how many users were affected (now have NULL HouseholdId)
DECLARE @AffectedUsers INT;
SELECT @AffectedUsers = COUNT(*) FROM Users WHERE HouseholdId IS NULL;
PRINT 'Users now in personal mode (HouseholdId = NULL): ' + CAST(@AffectedUsers AS NVARCHAR(10));

-- Check how many bills were affected (now have NULL HouseholdId)
DECLARE @AffectedBills INT;
SELECT @AffectedBills = COUNT(*) FROM Bills WHERE HouseholdId IS NULL;
PRINT 'Bills now in personal mode (HouseholdId = NULL): ' + CAST(@AffectedBills AS NVARCHAR(10));

COMMIT TRANSACTION;

PRINT '';
PRINT 'All households deleted successfully!';
PRINT 'Users and bills have been returned to personal mode.';

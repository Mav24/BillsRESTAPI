# Production Deployment Guide - Household Invitations

## ?? Quick Deployment Checklist

### ?? Step 1: Backup Database
```sql
-- Run this in SQL Server Management Studio or Azure Data Studio
BACKUP DATABASE [BillsDb] 
TO DISK = 'C:\Backups\BillsDb_BeforeHouseholdInvitations.bak'
WITH FORMAT, COMPRESSION;
```

### ?? Step 2: Apply Migration

**Option A: Using the SQL Script (Recommended)**
1. Open **Azure Data Studio** or **SQL Server Management Studio**
2. Connect to your production SQL Server
3. Select database: **BillsDb**
4. Open file: `src/BillsApi/household-invitations-migration-sqlserver.sql`
5. Click **Execute** (F5)
6. Verify success messages in output

**Option B: Using EF Core CLI**
```bash
cd "G:\Developement\Visual Studio Projects\BillsRESTAPI\src\BillsApi"
dotnet ef database update --connection "YourProductionConnectionString"
```

### ?? Step 3: Verify Migration

Run these queries in SQL Server:

```sql
-- Check table was created
SELECT * FROM sys.tables WHERE name = 'HouseholdInvitations';

-- Check table structure
SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE, CHARACTER_MAXIMUM_LENGTH
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'HouseholdInvitations'
ORDER BY ORDINAL_POSITION;

-- Check foreign keys
SELECT name, delete_referential_action_desc
FROM sys.foreign_keys
WHERE parent_object_id = OBJECT_ID('HouseholdInvitations');

-- Check indexes
SELECT name, type_desc
FROM sys.indexes
WHERE object_id = OBJECT_ID('HouseholdInvitations') AND name IS NOT NULL;
```

Expected Results:
- ? 1 table found
- ? 9 columns (Id, HouseholdId, Email, TokenHash, InvitedByUserId, CreatedAt, ExpiresAt, Accepted, AcceptedAt)
- ? 2 foreign keys (to Households and Users)
- ? 3 indexes (Email, HouseholdId, InvitedByUserId)

### ?? Step 4: Deploy Updated Code

Deploy these files to your server:

**New Files:**
- `Models/HouseholdInvitation.cs`
- `Models/AcceptInvitationRequest.cs`
- `wwwroot/` (if serving static content)

**Updated Files:**
- `Data/BillsDbContext.cs`
- `Program.cs`

**How to Deploy:**
```bash
# If using Git on server
git pull origin main

# Then restart your API service
sudo systemctl restart billsapi
# OR (Windows)
Restart-Service BillsApi
```

### ?? Step 5: Test the Feature

Use the `.http` file to test:

1. **Login as User 1**
```http
POST https://bills.dukesducks.ca/billsapi/auth/login
Content-Type: application/json

{
  "username": "user1",
  "password": "password123"
}
```

2. **Create a household (if not already in one)**
```http
POST https://bills.dukesducks.ca/billsapi/households
Content-Type: application/json
Authorization: Bearer {token}

{
  "name": "Test Family"
}
```

3. **Invite User 2**
```http
POST https://bills.dukesducks.ca/billsapi/households/invite
Content-Type: application/json
Authorization: Bearer {token}

{
  "email": "user2@example.com"
}
```

4. **Check email** - User 2 should receive invitation email

5. **Click invitation link** - Should open acceptance page

6. **Accept invitation** - User 2 joins household

7. **Verify** - Both users should see shared bills

---

## ?? Troubleshooting

### Problem: Migration fails with "table already exists"
**Solution:** The script has `IF NOT EXISTS` checks, so this shouldn't happen. If it does, the table was created manually. Skip to verification step.

### Problem: Foreign key constraint fails
**Solution:** Ensure `Households` and `Users` tables exist first. Run the household support migration if needed.

### Problem: Email not sending
**Solution:** 
1. Check SMTP settings in `appsettings.json`
2. Check email sender logs in application logs
3. Verify email service is configured correctly

### Problem: Acceptance page doesn't load
**Solution:**
1. Verify URL is correct: `https://bills.dukesducks.ca/billsapi/household-invitation?token=...`
2. Check token is properly URL encoded
3. Check API logs for errors

### Problem: "Invalid or expired invitation" error
**Solution:**
1. Check invitation hasn't expired (7 days)
2. Verify token matches database (compare hashes)
3. Ensure user isn't already in a household
4. Check user email matches invitation email

---

## ?? Database Schema

### HouseholdInvitations Table

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| Id | NVARCHAR(450) | No | Primary key (GUID) |
| HouseholdId | UNIQUEIDENTIFIER | No | FK to Households |
| Email | NVARCHAR(256) | No | Email of invited user |
| TokenHash | NVARCHAR(128) | No | SHA-256 hash of token |
| InvitedByUserId | NVARCHAR(450) | No | FK to Users (inviter) |
| CreatedAt | DATETIME2 | No | When invitation was created |
| ExpiresAt | DATETIME2 | No | When invitation expires |
| Accepted | BIT | No | Whether invitation was accepted |
| AcceptedAt | DATETIME2 | Yes | When invitation was accepted |

**Indexes:**
- `IX_HouseholdInvitations_Email` - For looking up by email
- `IX_HouseholdInvitations_HouseholdId` - For finding household invitations
- `IX_HouseholdInvitations_InvitedByUserId` - For tracking who sent invitations

**Foreign Keys:**
- `FK_HouseholdInvitations_Households_HouseholdId` - CASCADE delete
- `FK_HouseholdInvitations_Users_InvitedByUserId` - CASCADE delete

---

## ?? Post-Deployment Testing Script

```sql
-- 1. Check migration history
SELECT TOP 5 MigrationId, ProductVersion 
FROM __EFMigrationsHistory 
ORDER BY MigrationId DESC;

-- 2. Verify HouseholdInvitations table
SELECT COUNT(*) AS InvitationCount FROM HouseholdInvitations;

-- 3. Test query performance (should be instant)
SELECT TOP 10 
    i.Email,
    h.Name AS HouseholdName,
    u.Username AS InvitedBy,
    i.CreatedAt,
    i.Accepted
FROM HouseholdInvitations i
JOIN Households h ON i.HouseholdId = h.Id
JOIN Users u ON i.InvitedByUserId = u.Id
ORDER BY i.CreatedAt DESC;

-- 4. Check for expired invitations (cleanup opportunity)
SELECT 
    COUNT(*) AS ExpiredCount,
    MIN(ExpiresAt) AS OldestExpiration
FROM HouseholdInvitations
WHERE ExpiresAt < GETUTCDATE() AND Accepted = 0;
```

---

## ?? Optional: Cleanup Old Invitations

Add this as a scheduled task or manual cleanup:

```sql
-- Delete expired, unaccepted invitations older than 30 days
DELETE FROM HouseholdInvitations
WHERE Accepted = 0 
  AND ExpiresAt < DATEADD(day, -30, GETUTCDATE());

-- View what would be deleted (run first to verify)
SELECT 
    Email,
    HouseholdId,
    CreatedAt,
    ExpiresAt,
    DATEDIFF(day, ExpiresAt, GETUTCDATE()) AS DaysExpired
FROM HouseholdInvitations
WHERE Accepted = 0 
  AND ExpiresAt < DATEADD(day, -30, GETUTCDATE());
```

---

## ? Deployment Complete!

After completing all steps:
- ? Database has HouseholdInvitations table
- ? Code deployed to production
- ? API restarted
- ? Invitation emails working
- ? Acceptance page accessible
- ? Users can join households

**Your household invitation system is live!** ??

---

## ?? Support

If you encounter issues:
1. Check application logs
2. Check email service logs
3. Verify database migration was successful
4. Test with `.http` file endpoints
5. Check browser console for JavaScript errors (on acceptance page)

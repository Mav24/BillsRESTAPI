# BillsAPI Deployment Guide

This guide covers deploying the BillsAPI to a Windows Server with IIS and SQL Server.

## Prerequisites

### Required Software

1. **Windows Server** (2016 or later recommended)
2. **IIS (Internet Information Services)**
   - Enable via Server Manager ? Add Roles and Features
   - Or via PowerShell: `Install-WindowsFeature -name Web-Server -IncludeManagementTools`

3. **.NET 10 Hosting Bundle** ?? **CRITICAL**
   - Download from: https://dotnet.microsoft.com/download/dotnet/10.0
   - Look for **"Hosting Bundle"** under Windows hosting
   - **Not** the SDK or Runtime alone
   - After installation, run: `iisreset`

4. **SQL Server Express** (or full SQL Server)
   - Download from: https://www.microsoft.com/en-us/sql-server/sql-server-downloads
   - SQL Server Management Studio (SSMS) recommended for management

## Step-by-Step Deployment

### 1. Prepare the Database

#### Option A: Manually Create Database and Tables (Recommended)

Run this SQL script in SQL Server Management Studio:

```sql
-- Create the database
CREATE DATABASE BillsDb;
GO

USE BillsDb;
GO

-- Create Users table
CREATE TABLE [Users] (
    [Id] NVARCHAR(450) NOT NULL,
    [Username] NVARCHAR(100) NOT NULL,
    [PasswordHash] NVARCHAR(MAX) NOT NULL,
    CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
);

CREATE UNIQUE INDEX [IX_Users_Username] ON [Users] ([Username]);
GO

-- Create Bills table
CREATE TABLE [Bills] (
    [Id] INT IDENTITY(1,1) NOT NULL,
    [UserId] NVARCHAR(450) NOT NULL,
    [BillName] NVARCHAR(100) NOT NULL,
    [Amount] DECIMAL(18,2) NOT NULL,
    [Date] DATETIME2 NOT NULL,
    [AmountOverMinimum] DECIMAL(18,2) NOT NULL,
    CONSTRAINT [PK_Bills] PRIMARY KEY ([Id])
);

CREATE INDEX [IX_Bills_UserId] ON [Bills] ([UserId]);
GO

-- Create RefreshTokens table
CREATE TABLE [RefreshTokens] (
    [Id] NVARCHAR(450) NOT NULL,
    [Token] NVARCHAR(256) NOT NULL,
    [UserId] NVARCHAR(450) NOT NULL,
    [ExpiresAt] DATETIME2 NOT NULL,
    [CreatedAt] DATETIME2 NOT NULL,
    [IsRevoked] BIT NOT NULL,
    CONSTRAINT [PK_RefreshTokens] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_RefreshTokens_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);

CREATE UNIQUE INDEX [IX_RefreshTokens_Token] ON [RefreshTokens] ([Token]);
GO

-- Create migrations history table
CREATE TABLE [__EFMigrationsHistory] (
    [MigrationId] NVARCHAR(150) NOT NULL,
    [ProductVersion] NVARCHAR(32) NOT NULL,
    CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
);

-- Mark migrations as applied (update with your actual migration names)
INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES 
    (N'20260118235656_AddRefreshTokens', N'10.0.1'),
    (N'20260118235927_InitialCreateWithRefreshTokens', N'10.0.1'),
    (N'20260119000824_SyncPendingChanges', N'10.0.1');
GO
```

#### Option B: Let the App Create the Database

If you enable automatic migrations in `Program.cs`, ensure the IIS App Pool identity has database creation permissions (see Step 3).

### 2. Configure SQL Server Permissions

Grant the IIS Application Pool identity access to SQL Server:

```sql
USE master;
GO

-- Replace 'DefaultAppPool' with your actual app pool name
DECLARE @AppPoolIdentity NVARCHAR(128) = 'IIS APPPOOL\DefaultAppPool';

-- Create login if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.server_principals WHERE name = @AppPoolIdentity)
BEGIN
    EXEC('CREATE LOGIN [' + @AppPoolIdentity + '] FROM WINDOWS');
END

-- Grant database creation permission (only if using Option B above)
-- ALTER SERVER ROLE dbcreator ADD MEMBER [IIS APPPOOL\DefaultAppPool];

USE BillsDb;
GO

-- Create user and grant permissions
EXEC('CREATE USER [' + @AppPoolIdentity + '] FOR LOGIN [' + @AppPoolIdentity + ']');
EXEC sp_addrolemember 'db_owner', @AppPoolIdentity;
GO
```

**Note:** Find your app pool name in IIS Manager ? Application Pools.

### 3. Publish the Application

From your development machine:

```powershell
# Navigate to project directory
cd "path\to\BillsRESTAPI"

# Publish the application
dotnet publish src/BillsApi/BillsApi.csproj -c Release -o ./publish
```

### 4. Deploy Files to Server

1. Copy all files from the `publish` folder to the server (e.g., `C:\inetpub\wwwroot\billsapi\`)

2. **Create `appsettings.Secrets.json`** (NOT included in publish):
   ```json
   {
     "Jwt": {
       "SecretKey": "YOUR-SUPER-SECRET-KEY-AT-LEAST-32-CHARS-LONG"
     }
   }
   ```
   
   ?? **Security:** Keep this file secure. Never commit it to Git.

3. **Update `appsettings.Production.json`** with your server's details:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=YOUR-SERVER\\SQLEXPRESS;Database=BillsDb;Trusted_Connection=True;TrustServerCertificate=true"
     },
     "Cors": {
       "AllowedOrigins": [ "http://your-frontend-url" ]
     }
   }
   ```

4. **Verify `web.config`** contains the environment variable:
   ```xml
   <environmentVariables>
     <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
   </environmentVariables>
   ```

5. **Create logs folder:**
   ```
   C:\inetpub\wwwroot\billsapi\logs\
   ```

### 5. Configure IIS

#### Create Application Pool

1. Open **IIS Manager**
2. Right-click **Application Pools** ? **Add Application Pool**
   - Name: `BillsApiAppPool`
   - .NET CLR version: **No Managed Code**
   - Click **OK**

#### Create Website or Application

**Option A: New Website**
1. Right-click **Sites** ? **Add Website**
   - Site name: `BillsApi`
   - Application pool: `BillsApiAppPool`
   - Physical path: `C:\inetpub\wwwroot\billsapi`
   - Binding: HTTP, Port 80 (or your choice)
   - Host name: `bills.yourdomain.com` (optional)

**Option B: Application under Default Web Site**
1. Right-click **Default Web Site** ? **Add Application**
   - Alias: `billsapi`
   - Application pool: `BillsApiAppPool`
   - Physical path: `C:\inetpub\wwwroot\billsapi`

### 6. Configure SSL (HTTPS) - Recommended

1. Obtain an SSL certificate
2. In IIS Manager, select your site ? **Bindings** ? **Add**
   - Type: `https`
   - Port: `443`
   - SSL certificate: Select your certificate

### 7. Start the Application

```powershell
# Restart IIS
iisreset
```

### 8. Test the Deployment

1. **Health Check:**
   ```
   GET https://your-server/billsapi/health
   ```
   Should return: `Healthy`

2. **Register a User:**
   ```http
   POST https://your-server/billsapi/auth/register
   Content-Type: application/json

   {
     "username": "testuser",
     "password": "testpass123"
   }
   ```

3. **Login:**
   ```http
   POST https://your-server/billsapi/auth/login
   Content-Type: application/json

   {
     "username": "testuser",
     "password": "testpass123"
   }
   ```

   Should return an `accessToken` and `refreshToken`.

## Troubleshooting

### Common Issues

#### 1. "500.30 - ASP.NET Core app failed to start"

**Cause:** .NET 10 Hosting Bundle not installed or app pool environment not set.

**Solution:**
- Install .NET 10 Hosting Bundle
- Run `iisreset`
- Verify `web.config` has `<environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />`

#### 2. "failed to load coreclr"

**Cause:** .NET 10 runtime not installed.

**Solution:** Install .NET 10 Hosting Bundle and restart IIS.

#### 3. App uses SQLite instead of SQL Server

**Symptom:** Errors showing `TEXT` and `INTEGER` types instead of `NVARCHAR` and `BIT`.

**Cause:** App running in Development mode.

**Solution:** 
- Set environment to Production in `web.config`
- Or add environment variable in IIS Configuration Editor

#### 4. "Column 'Id' in table 'RefreshTokens' is of a type that is invalid"

**Cause:** App trying to create tables with SQLite syntax on SQL Server.

**Solution:** Ensure app is running in Production mode (see #3 above).

#### 5. Database connection fails

**Cause:** App pool identity lacks SQL Server permissions.

**Solution:** Grant permissions using the SQL script in Step 2.

#### 6. "JWT SecretKey is not configured"

**Cause:** `appsettings.Secrets.json` missing or not deployed.

**Solution:** Create the file with a strong secret key (see Step 4.2).

### Enable Detailed Logging

If the app fails to start, enable stdout logging to see detailed errors:

1. Edit `web.config`:
   ```xml
   <aspNetCore ... stdoutLogEnabled="true" stdoutLogFile=".\logs\stdout" ...>
   ```

2. Restart the app

3. Check logs in `C:\inetpub\wwwroot\billsapi\logs\`

### Check Windows Event Viewer

Detailed error messages are logged here:
- **Event Viewer** ? **Windows Logs** ? **Application**
- Look for errors from source: `ASP.NET Core` or `IIS`

## Security Considerations

1. ? **Never commit `appsettings.Secrets.json`** to source control
2. ? Use **HTTPS** in production
3. ? Use a **strong JWT secret key** (32+ characters, random)
4. ? Configure **CORS** to only allow your frontend domain
5. ? Keep the **.NET runtime updated** for security patches
6. ? Use **SQL Authentication** instead of Windows Auth if deploying to cloud
7. ? Set appropriate **file permissions** on the deployment folder
8. ? Consider using **Azure Key Vault** or similar for secrets management

## Updating the Application

To deploy updates:

1. Publish the new version
2. Stop the IIS Application Pool
3. Copy files to the server (overwrite existing)
4. Verify `appsettings.Secrets.json` still exists
5. Start the Application Pool
6. Run `iisreset`
7. Test the deployment

## Monitoring

- Set up **Application Insights** or similar monitoring
- Monitor **Windows Event Viewer** for errors
- Review **IIS logs** and **stdout logs** regularly
- Set up **health check monitoring** to alert if `/health` endpoint fails

## Support

For issues or questions, contact the development team or create an issue in the repository.

---

**Last Updated:** January 2026  
**Version:** 1.0  
**.NET Version:** 10.0

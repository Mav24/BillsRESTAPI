IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260118235927_InitialCreateWithRefreshTokens'
)
BEGIN
    CREATE TABLE [Bills] (
        [Id] INTEGER NOT NULL,
        [UserId] TEXT NOT NULL,
        [BillName] TEXT NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Date] TEXT NOT NULL,
        [AmountOverMinimum] decimal(18,2) NOT NULL,
        CONSTRAINT [PK_Bills] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260118235927_InitialCreateWithRefreshTokens'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] TEXT NOT NULL,
        [Username] TEXT NOT NULL,
        [PasswordHash] TEXT NOT NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260118235927_InitialCreateWithRefreshTokens'
)
BEGIN
    CREATE INDEX [IX_Bills_UserId] ON [Bills] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260118235927_InitialCreateWithRefreshTokens'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_Username] ON [Users] ([Username]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260118235927_InitialCreateWithRefreshTokens'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260118235927_InitialCreateWithRefreshTokens', N'10.0.1');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214191651_AddHouseholdSupport'
)
BEGIN
    ALTER TABLE [Users] ADD [Email] TEXT NOT NULL DEFAULT '';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214191651_AddHouseholdSupport'
)
BEGIN
    ALTER TABLE [Users] ADD [HouseholdId] TEXT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214191651_AddHouseholdSupport'
)
BEGIN
    ALTER TABLE [Bills] ADD [HouseholdId] TEXT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214191651_AddHouseholdSupport'
)
BEGIN
    ALTER TABLE [Bills] ADD [IsPaid] INTEGER NOT NULL DEFAULT CAST(0 AS INTEGER);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214191651_AddHouseholdSupport'
)
BEGIN
    ALTER TABLE [Bills] ADD [PaidDate] TEXT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214191651_AddHouseholdSupport'
)
BEGIN
    CREATE TABLE [Households] (
        [Id] TEXT NOT NULL,
        [Name] TEXT NOT NULL,
        [CreatedAt] TEXT NOT NULL,
        CONSTRAINT [PK_Households] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214191651_AddHouseholdSupport'
)
BEGIN
    CREATE TABLE [PasswordResetTokens] (
        [Id] TEXT NOT NULL,
        [UserId] TEXT NOT NULL,
        [TokenHash] TEXT NOT NULL,
        [ExpiresAt] TEXT NOT NULL,
        [Used] INTEGER NOT NULL DEFAULT CAST(0 AS INTEGER),
        CONSTRAINT [PK_PasswordResetTokens] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PasswordResetTokens_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214191651_AddHouseholdSupport'
)
BEGIN
    CREATE TABLE [RefreshTokens] (
        [Id] TEXT NOT NULL,
        [Token] TEXT NOT NULL,
        [UserId] TEXT NOT NULL,
        [ExpiresAt] TEXT NOT NULL,
        [CreatedAt] TEXT NOT NULL,
        [IsRevoked] INTEGER NOT NULL,
        CONSTRAINT [PK_RefreshTokens] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RefreshTokens_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214191651_AddHouseholdSupport'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_Email] ON [Users] ([Email]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214191651_AddHouseholdSupport'
)
BEGIN
    CREATE INDEX [IX_Users_HouseholdId] ON [Users] ([HouseholdId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214191651_AddHouseholdSupport'
)
BEGIN
    CREATE INDEX [IX_Bills_HouseholdId] ON [Bills] ([HouseholdId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214191651_AddHouseholdSupport'
)
BEGIN
    CREATE INDEX [IX_PasswordResetTokens_UserId] ON [PasswordResetTokens] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214191651_AddHouseholdSupport'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RefreshTokens_Token] ON [RefreshTokens] ([Token]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214191651_AddHouseholdSupport'
)
BEGIN
    CREATE INDEX [IX_RefreshTokens_UserId] ON [RefreshTokens] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214191651_AddHouseholdSupport'
)
BEGIN
    ALTER TABLE [Bills] ADD CONSTRAINT [FK_Bills_Households_HouseholdId] FOREIGN KEY ([HouseholdId]) REFERENCES [Households] ([Id]) ON DELETE SET NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214191651_AddHouseholdSupport'
)
BEGIN
    ALTER TABLE [Users] ADD CONSTRAINT [FK_Users_Households_HouseholdId] FOREIGN KEY ([HouseholdId]) REFERENCES [Households] ([Id]) ON DELETE SET NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214191651_AddHouseholdSupport'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260214191651_AddHouseholdSupport', N'10.0.1');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214202016_AddHouseholdInvitations'
)
BEGIN
    CREATE TABLE [HouseholdInvitations] (
        [Id] TEXT NOT NULL,
        [HouseholdId] TEXT NOT NULL,
        [Email] TEXT NOT NULL,
        [TokenHash] TEXT NOT NULL,
        [InvitedByUserId] TEXT NOT NULL,
        [CreatedAt] TEXT NOT NULL,
        [ExpiresAt] TEXT NOT NULL,
        [Accepted] INTEGER NOT NULL DEFAULT CAST(0 AS INTEGER),
        [AcceptedAt] TEXT NULL,
        CONSTRAINT [PK_HouseholdInvitations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_HouseholdInvitations_Households_HouseholdId] FOREIGN KEY ([HouseholdId]) REFERENCES [Households] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_HouseholdInvitations_Users_InvitedByUserId] FOREIGN KEY ([InvitedByUserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214202016_AddHouseholdInvitations'
)
BEGIN
    CREATE INDEX [IX_HouseholdInvitations_Email] ON [HouseholdInvitations] ([Email]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214202016_AddHouseholdInvitations'
)
BEGIN
    CREATE INDEX [IX_HouseholdInvitations_HouseholdId] ON [HouseholdInvitations] ([HouseholdId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214202016_AddHouseholdInvitations'
)
BEGIN
    CREATE INDEX [IX_HouseholdInvitations_InvitedByUserId] ON [HouseholdInvitations] ([InvitedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214202016_AddHouseholdInvitations'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260214202016_AddHouseholdInvitations', N'10.0.1');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260322230937_AddBillSharing'
)
BEGIN
    CREATE TABLE [BillShares] (
        [Id] INTEGER NOT NULL,
        [BillId] INTEGER NOT NULL,
        [SharedByUserId] TEXT NOT NULL,
        [SharedWithUserId] TEXT NOT NULL,
        [SharedAt] TEXT NOT NULL,
        CONSTRAINT [PK_BillShares] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BillShares_Bills_BillId] FOREIGN KEY ([BillId]) REFERENCES [Bills] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_BillShares_Users_SharedByUserId] FOREIGN KEY ([SharedByUserId]) REFERENCES [Users] ([Id]),
        CONSTRAINT [FK_BillShares_Users_SharedWithUserId] FOREIGN KEY ([SharedWithUserId]) REFERENCES [Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260322230937_AddBillSharing'
)
BEGIN
    CREATE UNIQUE INDEX [IX_BillShares_BillId_SharedWithUserId] ON [BillShares] ([BillId], [SharedWithUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260322230937_AddBillSharing'
)
BEGIN
    CREATE INDEX [IX_BillShares_SharedByUserId] ON [BillShares] ([SharedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260322230937_AddBillSharing'
)
BEGIN
    CREATE INDEX [IX_BillShares_SharedWithUserId] ON [BillShares] ([SharedWithUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260322230937_AddBillSharing'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260322230937_AddBillSharing', N'10.0.1');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107211123_AddHouseholdOwner'
)
BEGIN
    ALTER TABLE [Households] ADD [OwnerUserId] TEXT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107211123_AddHouseholdOwner'
)
BEGIN

                        UPDATE h
                        SET h.OwnerUserId = u.Id
                        FROM Households h
                        CROSS APPLY (
                            SELECT TOP 1 Id
                            FROM Users
                            WHERE HouseholdId = h.Id
                            ORDER BY Id
                        ) u
                        WHERE h.OwnerUserId IS NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107211123_AddHouseholdOwner'
)
BEGIN
    ALTER TABLE [HouseholdInvitations] ADD [ShareInviterBills] INTEGER NOT NULL DEFAULT CAST(0 AS INTEGER);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107211123_AddHouseholdOwner'
)
BEGIN
    CREATE INDEX [IX_Households_OwnerUserId] ON [Households] ([OwnerUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107211123_AddHouseholdOwner'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261107211123_AddHouseholdOwner', N'10.0.1');
END;

COMMIT;
GO


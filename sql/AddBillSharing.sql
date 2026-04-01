-- Migration: AddBillSharing
-- Run this on your production SQL Server to create the BillShares table

BEGIN TRANSACTION;

CREATE TABLE [BillShares] (
    [Id] int NOT NULL IDENTITY,
    [BillId] int NOT NULL,
    [SharedByUserId] nvarchar(450) NOT NULL,
    [SharedWithUserId] nvarchar(450) NOT NULL,
    [SharedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_BillShares] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_BillShares_Bills_BillId] FOREIGN KEY ([BillId]) REFERENCES [Bills] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_BillShares_Users_SharedByUserId] FOREIGN KEY ([SharedByUserId]) REFERENCES [Users] ([Id]),
    CONSTRAINT [FK_BillShares_Users_SharedWithUserId] FOREIGN KEY ([SharedWithUserId]) REFERENCES [Users] ([Id])
);

CREATE UNIQUE INDEX [IX_BillShares_BillId_SharedWithUserId] ON [BillShares] ([BillId], [SharedWithUserId]);

CREATE INDEX [IX_BillShares_SharedByUserId] ON [BillShares] ([SharedByUserId]);

CREATE INDEX [IX_BillShares_SharedWithUserId] ON [BillShares] ([SharedWithUserId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260322230937_AddBillSharing', N'10.0.1');

COMMIT;

BEGIN TRANSACTION;
ALTER TABLE [Households] ADD [OwnerUserId] TEXT NULL;


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

ALTER TABLE [HouseholdInvitations] ADD [ShareInviterBills] INTEGER NOT NULL DEFAULT CAST(0 AS INTEGER);

CREATE INDEX [IX_Households_OwnerUserId] ON [Households] ([OwnerUserId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261107211123_AddHouseholdOwner', N'10.0.1');

COMMIT;
GO


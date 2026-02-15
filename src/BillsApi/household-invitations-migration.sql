BEGIN TRANSACTION;
CREATE TABLE "HouseholdInvitations" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_HouseholdInvitations" PRIMARY KEY,
    "HouseholdId" TEXT NOT NULL,
    "Email" TEXT NOT NULL,
    "TokenHash" TEXT NOT NULL,
    "InvitedByUserId" TEXT NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    "ExpiresAt" TEXT NOT NULL,
    "Accepted" INTEGER NOT NULL DEFAULT 0,
    "AcceptedAt" TEXT NULL,
    CONSTRAINT "FK_HouseholdInvitations_Households_HouseholdId" FOREIGN KEY ("HouseholdId") REFERENCES "Households" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_HouseholdInvitations_Users_InvitedByUserId" FOREIGN KEY ("InvitedByUserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);

CREATE INDEX "IX_HouseholdInvitations_Email" ON "HouseholdInvitations" ("Email");

CREATE INDEX "IX_HouseholdInvitations_HouseholdId" ON "HouseholdInvitations" ("HouseholdId");

CREATE INDEX "IX_HouseholdInvitations_InvitedByUserId" ON "HouseholdInvitations" ("InvitedByUserId");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260214202016_AddHouseholdInvitations', '10.0.1');

COMMIT;


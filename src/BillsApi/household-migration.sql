BEGIN TRANSACTION;
ALTER TABLE "Users" ADD "Email" TEXT NOT NULL DEFAULT '';

ALTER TABLE "Users" ADD "HouseholdId" TEXT NULL;

ALTER TABLE "Bills" ADD "HouseholdId" TEXT NULL;

ALTER TABLE "Bills" ADD "IsPaid" INTEGER NOT NULL DEFAULT 0;

ALTER TABLE "Bills" ADD "PaidDate" TEXT NULL;

CREATE TABLE "Households" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Households" PRIMARY KEY,
    "Name" TEXT NOT NULL,
    "CreatedAt" TEXT NOT NULL
);

CREATE TABLE "PasswordResetTokens" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_PasswordResetTokens" PRIMARY KEY,
    "UserId" TEXT NOT NULL,
    "TokenHash" TEXT NOT NULL,
    "ExpiresAt" TEXT NOT NULL,
    "Used" INTEGER NOT NULL DEFAULT 0,
    CONSTRAINT "FK_PasswordResetTokens_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);

CREATE TABLE "RefreshTokens" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_RefreshTokens" PRIMARY KEY,
    "Token" TEXT NOT NULL,
    "UserId" TEXT NOT NULL,
    "ExpiresAt" TEXT NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    "IsRevoked" INTEGER NOT NULL,
    CONSTRAINT "FK_RefreshTokens_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);

CREATE UNIQUE INDEX "IX_Users_Email" ON "Users" ("Email");

CREATE INDEX "IX_Users_HouseholdId" ON "Users" ("HouseholdId");

CREATE INDEX "IX_Bills_HouseholdId" ON "Bills" ("HouseholdId");

CREATE INDEX "IX_PasswordResetTokens_UserId" ON "PasswordResetTokens" ("UserId");

CREATE UNIQUE INDEX "IX_RefreshTokens_Token" ON "RefreshTokens" ("Token");

CREATE INDEX "IX_RefreshTokens_UserId" ON "RefreshTokens" ("UserId");

CREATE TABLE "ef_temp_Bills" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Bills" PRIMARY KEY AUTOINCREMENT,
    "Amount" decimal(18,2) NOT NULL,
    "AmountOverMinimum" decimal(18,2) NOT NULL,
    "BillName" TEXT NOT NULL,
    "Date" TEXT NOT NULL,
    "HouseholdId" TEXT NULL,
    "IsPaid" INTEGER NOT NULL,
    "PaidDate" TEXT NULL,
    "UserId" TEXT NOT NULL,
    CONSTRAINT "FK_Bills_Households_HouseholdId" FOREIGN KEY ("HouseholdId") REFERENCES "Households" ("Id") ON DELETE SET NULL
);

INSERT INTO "ef_temp_Bills" ("Id", "Amount", "AmountOverMinimum", "BillName", "Date", "HouseholdId", "IsPaid", "PaidDate", "UserId")
SELECT "Id", "Amount", "AmountOverMinimum", "BillName", "Date", "HouseholdId", "IsPaid", "PaidDate", "UserId"
FROM "Bills";

CREATE TABLE "ef_temp_Users" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Users" PRIMARY KEY,
    "Email" TEXT NOT NULL,
    "HouseholdId" TEXT NULL,
    "PasswordHash" TEXT NOT NULL,
    "Username" TEXT NOT NULL,
    CONSTRAINT "FK_Users_Households_HouseholdId" FOREIGN KEY ("HouseholdId") REFERENCES "Households" ("Id") ON DELETE SET NULL
);

INSERT INTO "ef_temp_Users" ("Id", "Email", "HouseholdId", "PasswordHash", "Username")
SELECT "Id", "Email", "HouseholdId", "PasswordHash", "Username"
FROM "Users";

COMMIT;

PRAGMA foreign_keys = 0;

BEGIN TRANSACTION;
DROP TABLE "Bills";

ALTER TABLE "ef_temp_Bills" RENAME TO "Bills";

DROP TABLE "Users";

ALTER TABLE "ef_temp_Users" RENAME TO "Users";

COMMIT;

PRAGMA foreign_keys = 1;

BEGIN TRANSACTION;
CREATE INDEX "IX_Bills_HouseholdId" ON "Bills" ("HouseholdId");

CREATE INDEX "IX_Bills_UserId" ON "Bills" ("UserId");

CREATE UNIQUE INDEX "IX_Users_Email" ON "Users" ("Email");

CREATE INDEX "IX_Users_HouseholdId" ON "Users" ("HouseholdId");

CREATE UNIQUE INDEX "IX_Users_Username" ON "Users" ("Username");

COMMIT;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260214191651_AddHouseholdSupport', '10.0.1');


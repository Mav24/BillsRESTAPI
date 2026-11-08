using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BillsApi.Migrations
{
    /// <inheritdoc />
    public partial class AddHouseholdOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider.Contains("SqlServer"))
            {
                migrationBuilder.Sql(@"
                    IF COL_LENGTH('dbo.Households', 'OwnerUserId') IS NULL
                    BEGIN
                        ALTER TABLE [dbo].[Households]
                        ADD [OwnerUserId] nvarchar(450) NULL;
                    END");

                migrationBuilder.Sql(@"
                    EXEC(N'
                        UPDATE h
                        SET h.[OwnerUserId] = u.[Id]
                        FROM [dbo].[Households] h
                        CROSS APPLY (
                            SELECT TOP 1 [Id]
                            FROM [dbo].[Users]
                            WHERE [HouseholdId] = h.[Id]
                            ORDER BY [Id]
                        ) u
                        WHERE h.[OwnerUserId] IS NULL;
                    ');");

                migrationBuilder.Sql(@"
                    IF COL_LENGTH('dbo.HouseholdInvitations', 'ShareInviterBills') IS NULL
                    BEGIN
                        ALTER TABLE [dbo].[HouseholdInvitations]
                        ADD [ShareInviterBills] bit NOT NULL
                            CONSTRAINT [DF_HouseholdInvitations_ShareInviterBills] DEFAULT (0);
                    END");

                migrationBuilder.Sql(@"
                    IF NOT EXISTS (
                        SELECT 1
                        FROM sys.indexes
                        WHERE name = 'IX_Households_OwnerUserId'
                          AND object_id = OBJECT_ID('dbo.Households')
                    )
                    BEGIN
                        CREATE INDEX [IX_Households_OwnerUserId]
                        ON [dbo].[Households] ([OwnerUserId]);
                    END");
            }
            else
            {
                migrationBuilder.AddColumn<string>(
                    name: "OwnerUserId",
                    table: "Households",
                    type: "TEXT",
                    maxLength: 450,
                    nullable: true);

                migrationBuilder.Sql(@"
                    UPDATE Households
                    SET OwnerUserId = (
                        SELECT u.Id
                        FROM Users u
                        WHERE u.HouseholdId = Households.Id
                        ORDER BY u.Id
                        LIMIT 1
                    )
                    WHERE OwnerUserId IS NULL;");

                migrationBuilder.AddColumn<bool>(
                    name: "ShareInviterBills",
                    table: "HouseholdInvitations",
                    type: "INTEGER",
                    nullable: false,
                    defaultValue: false);

                migrationBuilder.CreateIndex(
                    name: "IX_Households_OwnerUserId",
                    table: "Households",
                    column: "OwnerUserId");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider.Contains("SqlServer"))
            {
                migrationBuilder.Sql(@"
                    IF EXISTS (
                        SELECT 1
                        FROM sys.indexes
                        WHERE name = 'IX_Households_OwnerUserId'
                          AND object_id = OBJECT_ID('dbo.Households')
                    )
                    BEGIN
                        DROP INDEX [IX_Households_OwnerUserId] ON [dbo].[Households];
                    END");

                migrationBuilder.Sql(@"
                    IF COL_LENGTH('dbo.Households', 'OwnerUserId') IS NOT NULL
                    BEGIN
                        ALTER TABLE [dbo].[Households] DROP COLUMN [OwnerUserId];
                    END");

                migrationBuilder.Sql(@"
                    IF EXISTS (
                        SELECT 1
                        FROM sys.default_constraints
                        WHERE name = 'DF_HouseholdInvitations_ShareInviterBills'
                    )
                    BEGIN
                        ALTER TABLE [dbo].[HouseholdInvitations]
                        DROP CONSTRAINT [DF_HouseholdInvitations_ShareInviterBills];
                    END");

                migrationBuilder.Sql(@"
                    IF COL_LENGTH('dbo.HouseholdInvitations', 'ShareInviterBills') IS NOT NULL
                    BEGIN
                        ALTER TABLE [dbo].[HouseholdInvitations] DROP COLUMN [ShareInviterBills];
                    END");
            }
            else
            {
                migrationBuilder.DropIndex(
                    name: "IX_Households_OwnerUserId",
                    table: "Households");

                migrationBuilder.DropColumn(
                    name: "OwnerUserId",
                    table: "Households");

                migrationBuilder.DropColumn(
                    name: "ShareInviterBills",
                    table: "HouseholdInvitations");
            }
        }
    }
}

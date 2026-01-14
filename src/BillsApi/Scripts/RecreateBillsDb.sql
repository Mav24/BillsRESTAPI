-- =============================================
-- Script to recreate BillsDb database
-- Run this on: DUCK-WEBSERVER\SQLEXPRESS02
-- WARNING: This will DELETE all existing data!
-- =============================================

USE master;
GO

-- Close all connections to the database
IF EXISTS (SELECT name FROM sys.databases WHERE name = N'BillsDb')
BEGIN
    ALTER DATABASE [BillsDb] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [BillsDb];
END
GO

-- Create fresh database
CREATE DATABASE [BillsDb];
GO

USE [BillsDb];
GO

-- Create Users table
CREATE TABLE [Users] (
    [Id] NVARCHAR(450) NOT NULL,
    [Username] NVARCHAR(100) NOT NULL,
    [PasswordHash] NVARCHAR(MAX) NOT NULL,
    CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
);
GO

-- Create unique index on Username
CREATE UNIQUE INDEX [IX_Users_Username] ON [Users] ([Username]);
GO

-- Create Bills table with UserId foreign key
CREATE TABLE [Bills] (
    [Id] INT IDENTITY(1,1) NOT NULL,
    [UserId] NVARCHAR(450) NOT NULL,
    [BillName] NVARCHAR(100) NOT NULL,
    [Amount] DECIMAL(18,2) NOT NULL,
    [Date] DATETIME2 NOT NULL,
    [AmountOverMinimum] DECIMAL(18,2) NOT NULL,
    CONSTRAINT [PK_Bills] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Bills_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users]([Id]) ON DELETE CASCADE
);
GO

-- Create index on UserId for faster queries
CREATE INDEX [IX_Bills_UserId] ON [Bills] ([UserId]);
GO

PRINT 'Database BillsDb created successfully!';
GO

-- Additive operational key store for Microsoft's EF Core Data Protection provider.
-- Does not change ApplicationDbContext or its migration history. Never delete old keys.
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF OBJECT_ID(N'dbo.DataProtectionKeys', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DataProtectionKeys
    (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_DataProtectionKeys PRIMARY KEY,
        FriendlyName nvarchar(max) NULL,
        Xml nvarchar(max) NULL
    );
END;
COMMIT TRANSACTION;

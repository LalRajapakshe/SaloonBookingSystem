/*
  Review script for migration 20261104023525_AddCatalogueVisibilityAndPlatformAdmin.

  Do not run this against saloonDB until the schema change has been approved.
  Do not rerun, reverse, or edit 20261015015246_AddUserAccessFoundation.
  This script was generated for review. It has not been executed.

  Data preservation:
  - New bit columns default to 0. Existing branches, services, and employees stay
    unpublished in the customer app and stay active for staff use.
  - New text columns are nullable. Existing rows keep their current values.
  - PreferredEmployeeId is nullable and is not a schedule. Existing appointments
    are unchanged.
  - Qualifications and EmployeeQualifications are new tables. No existing rows
    are copied or deleted.

  Permissions are not inserted here. The API adds catalogue, qualification, and
  platform permission rows the next time it starts, and it fills missing
  permissions onto the ADMIN and CUSTOMER roles. platform.manage is not granted
  by any role.

  After this script is applied, choose one platform account and run the commented
  UPDATE below. Leave every salon administrator at IsPlatformAdministrator = 0.
*/

BEGIN TRANSACTION;

ALTER TABLE [Users] ADD [IsPlatformAdministrator] bit NOT NULL CONSTRAINT [DF_Users_IsPlatformAdministrator] DEFAULT CAST(0 AS bit);

ALTER TABLE [Services] ADD [IsCustomerVisible] bit NOT NULL CONSTRAINT [DF_Services_IsCustomerVisible] DEFAULT CAST(0 AS bit);
ALTER TABLE [Services] ADD [IsPopular] bit NOT NULL CONSTRAINT [DF_Services_IsPopular] DEFAULT CAST(0 AS bit);
ALTER TABLE [Services] ADD [PhotoPath] nvarchar(80) NULL;

ALTER TABLE [Employees] ADD [IsCustomerVisible] bit NOT NULL CONSTRAINT [DF_Employees_IsCustomerVisible] DEFAULT CAST(0 AS bit);
ALTER TABLE [Employees] ADD [PhotoPath] nvarchar(80) NULL;
ALTER TABLE [Employees] ADD [ProfessionalDescription] nvarchar(1000) NULL;
ALTER TABLE [Employees] ADD [Specialisation] nvarchar(150) NULL;

ALTER TABLE [Branches] ADD [Highlight] nvarchar(150) NULL;
ALTER TABLE [Branches] ADD [IsCustomerVisible] bit NOT NULL CONSTRAINT [DF_Branches_IsCustomerVisible] DEFAULT CAST(0 AS bit);
ALTER TABLE [Branches] ADD [PhotoPath] nvarchar(80) NULL;

ALTER TABLE [AppointmentServices] ADD [PreferredEmployeeId] bigint NULL;

CREATE TABLE [Qualifications] (
    [QualificationId] bigint NOT NULL IDENTITY,
    [QualificationName] nvarchar(150) NOT NULL,
    [QualificationLevel] nvarchar(80) NULL,
    [Description] nvarchar(500) NULL,
    [CreatedDate] datetime2 NOT NULL,
    [CreatedBy] bigint NULL,
    [ModifiedDate] datetime2 NULL,
    [ModifiedBy] bigint NULL,
    [IsDeleted] bit NOT NULL,
    [IsActive] bit NOT NULL,
    [TenantId] bigint NOT NULL,
    CONSTRAINT [PK_Qualifications] PRIMARY KEY ([QualificationId])
);

CREATE TABLE [EmployeeQualifications] (
    [EmployeeQualificationId] bigint NOT NULL IDENTITY,
    [EmployeeId] bigint NOT NULL,
    [QualificationId] bigint NOT NULL,
    [AwardingInstitution] nvarchar(200) NULL,
    [CompletionYear] int NULL,
    [CertificatePath] nvarchar(80) NULL,
    [VerificationStatus] int NOT NULL,
    [IsCustomerVisible] bit NOT NULL,
    [CreatedDate] datetime2 NOT NULL,
    [CreatedBy] bigint NULL,
    [ModifiedDate] datetime2 NULL,
    [ModifiedBy] bigint NULL,
    [IsDeleted] bit NOT NULL,
    [IsActive] bit NOT NULL,
    [TenantId] bigint NOT NULL,
    CONSTRAINT [PK_EmployeeQualifications] PRIMARY KEY ([EmployeeQualificationId]),
    CONSTRAINT [FK_EmployeeQualifications_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([EmployeeId]),
    CONSTRAINT [FK_EmployeeQualifications_Qualifications_QualificationId] FOREIGN KEY ([QualificationId]) REFERENCES [Qualifications] ([QualificationId])
);

CREATE INDEX [IX_AppointmentServices_PreferredEmployeeId] ON [AppointmentServices] ([PreferredEmployeeId]);
CREATE UNIQUE INDEX [IX_EmployeeQualifications_EmployeeId_QualificationId] ON [EmployeeQualifications] ([EmployeeId], [QualificationId]) WHERE [IsDeleted] = 0;
CREATE INDEX [IX_EmployeeQualifications_QualificationId] ON [EmployeeQualifications] ([QualificationId]);
CREATE UNIQUE INDEX [IX_Qualifications_TenantId_QualificationName] ON [Qualifications] ([TenantId], [QualificationName]) WHERE [IsDeleted] = 0;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261104023525_AddCatalogueVisibilityAndPlatformAdmin', N'9.0.8');

COMMIT TRANSACTION;

/*
  MANUAL STEP. Run only for the chosen platform account, after the schema change.

  UPDATE [Users]
  SET [IsPlatformAdministrator] = 1
  WHERE [Username] = N'YOUR_PLATFORM_USERNAME'
    AND [IsDeleted] = 0;
*/

/*
  ROLLBACK. Run only if this change must be removed and no catalogue data must be kept.
  Dropping the new tables deletes qualification rows entered after the change.
  Dropping the new columns discards photos paths, visibility flags, and preferred stylists.

  BEGIN TRANSACTION;
  DELETE FROM [__EFMigrationsHistory]
  WHERE [MigrationId] = N'20261104023525_AddCatalogueVisibilityAndPlatformAdmin';
  DROP TABLE [EmployeeQualifications];
  DROP TABLE [Qualifications];
  DROP INDEX [IX_AppointmentServices_PreferredEmployeeId] ON [AppointmentServices];
  ALTER TABLE [AppointmentServices] DROP COLUMN [PreferredEmployeeId];
  ALTER TABLE [Branches] DROP CONSTRAINT [DF_Branches_IsCustomerVisible];
  ALTER TABLE [Branches] DROP COLUMN [Highlight], [IsCustomerVisible], [PhotoPath];
  ALTER TABLE [Employees] DROP CONSTRAINT [DF_Employees_IsCustomerVisible];
  ALTER TABLE [Employees] DROP COLUMN [IsCustomerVisible], [PhotoPath], [ProfessionalDescription], [Specialisation];
  ALTER TABLE [Services] DROP CONSTRAINT [DF_Services_IsCustomerVisible];
  ALTER TABLE [Services] DROP CONSTRAINT [DF_Services_IsPopular];
  ALTER TABLE [Services] DROP COLUMN [IsCustomerVisible], [IsPopular], [PhotoPath];
  ALTER TABLE [Users] DROP CONSTRAINT [DF_Users_IsPlatformAdministrator];
  ALTER TABLE [Users] DROP COLUMN [IsPlatformAdministrator];
  COMMIT TRANSACTION;
*/

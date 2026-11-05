BEGIN TRANSACTION;
CREATE TABLE [Docs] (
    [DocId] bigint NOT NULL IDENTITY,
    [CreatedBy] bigint NULL,
    [CreatedDate] datetime2 NOT NULL,
    [DocType] nvarchar(20) NOT NULL,
    [IsActive] bit NOT NULL,
    [IsDeleted] bit NOT NULL,
    [LastNo] bigint NOT NULL,
    [ModifiedBy] bigint NULL,
    [ModifiedDate] datetime2 NULL,
    [PadLength] int NOT NULL,
    [Prefix] nvarchar(10) NOT NULL,
    [TenantId] bigint NOT NULL,
    CONSTRAINT [PK_Docs] PRIMARY KEY ([DocId]),
    CONSTRAINT [FK_Docs_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([TenantId])
);

CREATE UNIQUE INDEX [IX_Docs_TenantId_DocType] ON [Docs] ([TenantId], [DocType]) WHERE [IsDeleted] = 0;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261025093100_AddDocsNumberSeries', N'9.0.8');

ALTER TABLE [Users] ADD [IsPlatformAdministrator] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [Services] ADD [IsCustomerVisible] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [Services] ADD [IsPopular] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [Services] ADD [PhotoPath] nvarchar(80) NULL;

ALTER TABLE [Employees] ADD [IsCustomerVisible] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [Employees] ADD [PhotoPath] nvarchar(80) NULL;

ALTER TABLE [Employees] ADD [ProfessionalDescription] nvarchar(1000) NULL;

ALTER TABLE [Employees] ADD [Specialisation] nvarchar(150) NULL;

ALTER TABLE [Branches] ADD [Highlight] nvarchar(150) NULL;

ALTER TABLE [Branches] ADD [IsCustomerVisible] bit NOT NULL DEFAULT CAST(0 AS bit);

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

COMMIT;
GO


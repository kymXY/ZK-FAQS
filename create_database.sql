/* =====================================================================
   FaqCms — database setup script for MSSQL
   Target: ZKPAYROLL-KYM\SQLEXPRESS (matches the connection string in
   appsettings.json). Safe to re-run — every step is guarded.

   NOTE: You normally do NOT need to run this by hand. Program.cs calls
   db.Database.Migrate() on startup, so the app creates the database and
   applies both migrations automatically the first time you run it,
   against whatever "ConnectionStrings:Default" points to.

   Run this instead if you want the schema to exist before the first run
   (e.g. to inspect it in SSMS first), or if the app's own migration run
   fails because the SQL login doesn't have CREATE DATABASE rights and
   a DBA needs to run this part separately. This script also stamps
   __EFMigrationsHistory so that a later `dotnet ef database update` or
   app startup won't try to re-create tables that already exist.
   ===================================================================== */

IF DB_ID(N'FaqCms') IS NULL
BEGIN
    CREATE DATABASE FaqCms;
END
GO

USE FaqCms;
GO

IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260910092554_InitialCreate')
BEGIN

CREATE TABLE [AdminUsers] (
    [Id] int NOT NULL IDENTITY(1,1),
    [Username] nvarchar(450) NOT NULL,
    [PasswordHash] nvarchar(max) NOT NULL,
    [Email] nvarchar(max) NULL,
    [FullName] nvarchar(max) NULL,
    [Role] nvarchar(max) NOT NULL,
    [IsPermanent] bit NOT NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [LastLoginAt] datetime2 NULL,
    [PasswordChangedAt] datetime2 NULL,
    CONSTRAINT [PK_AdminUsers] PRIMARY KEY ([Id])
);

CREATE TABLE [Categories] (
    [Id] int NOT NULL IDENTITY(1,1),
    [Name] nvarchar(max) NOT NULL,
    [Slug] nvarchar(450) NOT NULL,
    [Icon] nvarchar(max) NOT NULL,
    [Description] nvarchar(max) NULL,
    [Color] nvarchar(max) NULL,
    [SortOrder] int NOT NULL,
    [IsVisible] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Categories] PRIMARY KEY ([Id])
);

CREATE TABLE [Tags] (
    [Id] int NOT NULL IDENTITY(1,1),
    [Name] nvarchar(max) NOT NULL,
    [Slug] nvarchar(450) NOT NULL,
    [Color] nvarchar(max) NULL,
    CONSTRAINT [PK_Tags] PRIMARY KEY ([Id])
);

CREATE TABLE [AuditLog] (
    [Id] int NOT NULL IDENTITY(1,1),
    [Action] nvarchar(max) NOT NULL,
    [EntityType] nvarchar(450) NOT NULL,
    [EntityId] int NOT NULL,
    [EntityName] nvarchar(max) NOT NULL,
    [Username] nvarchar(max) NOT NULL,
    [ChangesJson] nvarchar(max) NULL,
    [IpAddress] nvarchar(max) NULL,
    [UserAgent] nvarchar(max) NULL,
    [Timestamp] datetime2 NOT NULL,
    [AdminUserId] int NULL,
    CONSTRAINT [PK_AuditLog] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AuditLog_AdminUsers_AdminUserId] FOREIGN KEY ([AdminUserId]) REFERENCES [AdminUsers] ([Id]) ON DELETE SET NULL
);

CREATE TABLE [Articles] (
    [Id] int NOT NULL IDENTITY(1,1),
    [CategoryId] int NOT NULL,
    [Title] nvarchar(max) NOT NULL,
    [Slug] nvarchar(450) NOT NULL,
    [Summary] nvarchar(max) NOT NULL,
    [VideoUrl] nvarchar(max) NULL,
    [SortOrder] int NOT NULL,
    [IsPublished] bit NOT NULL,
    [IsFeatured] bit NOT NULL,
    [ViewCount] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    [PublishedAt] datetime2 NULL,
    [ScheduledPublishAt] datetime2 NULL,
    [MetaTitle] nvarchar(max) NULL,
    [MetaDescription] nvarchar(max) NULL,
    [Tags] nvarchar(max) NULL,
    [ParentArticleId] int NULL,
    CONSTRAINT [PK_Articles] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Articles_Articles_ParentArticleId] FOREIGN KEY ([ParentArticleId]) REFERENCES [Articles] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Articles_Categories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [ArticleFeedbacks] (
    [Id] int NOT NULL IDENTITY(1,1),
    [ArticleId] int NOT NULL,
    [Rating] int NOT NULL,
    [Comment] nvarchar(max) NULL,
    [UserAgent] nvarchar(max) NULL,
    [IpAddress] nvarchar(max) NULL,
    [IsPublic] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_ArticleFeedbacks] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ArticleFeedbacks_Articles_ArticleId] FOREIGN KEY ([ArticleId]) REFERENCES [Articles] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [ArticleSteps] (
    [Id] int NOT NULL IDENTITY(1,1),
    [ArticleId] int NOT NULL,
    [SortOrder] int NOT NULL,
    [Title] nvarchar(max) NOT NULL,
    [Description] nvarchar(max) NOT NULL,
    [ImageUrl] nvarchar(max) NULL,
    [VideoUrl] nvarchar(max) NULL,
    [ImageAlt] nvarchar(max) NULL,
    [EstimatedMinutes] int NULL,
    [IsOptional] bit NOT NULL,
    CONSTRAINT [PK_ArticleSteps] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ArticleSteps_Articles_ArticleId] FOREIGN KEY ([ArticleId]) REFERENCES [Articles] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [ArticleTags] (
    [ArticleId] int NOT NULL,
    [TagId] int NOT NULL,
    CONSTRAINT [PK_ArticleTags] PRIMARY KEY ([ArticleId], [TagId]),
    CONSTRAINT [FK_ArticleTags_Articles_ArticleId] FOREIGN KEY ([ArticleId]) REFERENCES [Articles] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ArticleTags_Tags_TagId] FOREIGN KEY ([TagId]) REFERENCES [Tags] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [ArticleVersions] (
    [Id] int NOT NULL IDENTITY(1,1),
    [ArticleId] int NOT NULL,
    [Title] nvarchar(max) NOT NULL,
    [Summary] nvarchar(max) NOT NULL,
    [VideoUrl] nvarchar(max) NULL,
    [VersionNumber] int NOT NULL,
    [ChangeNotes] nvarchar(max) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_ArticleVersions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ArticleVersions_Articles_ArticleId] FOREIGN KEY ([ArticleId]) REFERENCES [Articles] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [ArticleStepVersions] (
    [Id] int NOT NULL IDENTITY(1,1),
    [ArticleVersionId] int NOT NULL,
    [SortOrder] int NOT NULL,
    [Title] nvarchar(max) NOT NULL,
    [Description] nvarchar(max) NOT NULL,
    [ImageUrl] nvarchar(max) NULL,
    [VideoUrl] nvarchar(max) NULL,
    CONSTRAINT [PK_ArticleStepVersions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ArticleStepVersions_ArticleVersions_ArticleVersionId] FOREIGN KEY ([ArticleVersionId]) REFERENCES [ArticleVersions] ([Id]) ON DELETE CASCADE
);

SET IDENTITY_INSERT [Categories] ON;
INSERT INTO [Categories] ([Id], [Color], [CreatedAt], [Description], [Icon], [IsVisible], [Name], [Slug], [SortOrder])
VALUES
(1, N'#1F7A4D', SYSUTCDATETIME(), N'Payroll processing and management guides', N'💰', 1, N'Payroll', N'payroll', 1),
(2, N'#38B476', SYSUTCDATETIME(), N'Employee onboarding, records, and HR processes', N'👥', 1, N'Employee Management', N'employee-management', 2),
(3, N'#5B6B60', SYSUTCDATETIME(), N'Financial transactions and reporting', N'📋', 1, N'Transactions', N'transactions', 3);
SET IDENTITY_INSERT [Categories] OFF;

SET IDENTITY_INSERT [Tags] ON;
INSERT INTO [Tags] ([Id], [Color], [Name], [Slug])
VALUES
(1, N'#1F7A4D', N'Getting Started', N'getting-started'),
(2, N'#38B476', N'Payroll Basics', N'payroll-basics'),
(3, N'#B8462E', N'Loans', N'loans'),
(4, N'#5B6B60', N'Corrections', N'corrections'),
(5, N'#0B120F', N'Advanced', N'advanced');
SET IDENTITY_INSERT [Tags] OFF;

SET IDENTITY_INSERT [Articles] ON;
INSERT INTO [Articles] ([Id], [CategoryId], [CreatedAt], [IsFeatured], [IsPublished], [MetaDescription], [MetaTitle], [ParentArticleId], [PublishedAt], [ScheduledPublishAt], [Slug], [SortOrder], [Summary], [Tags], [Title], [UpdatedAt], [VideoUrl], [ViewCount])
VALUES
(1, 1, '2026-09-02', 1, 1, NULL, NULL, NULL, '2026-09-02', NULL, N'how-to-create-payroll', 1, N'Run a new pay cycle for your team in a few steps — from setting the pay period to reviewing totals before it''s finalized.', N'payroll,basics,getting-started', N'How to create payroll', '2026-09-02', NULL, 0),
(2, 1, '2026-08-20', 0, 1, NULL, NULL, NULL, '2026-08-20', NULL, N'payroll-loans', 2, N'Add a staff loan to a pay run, set repayment terms, and track the remaining balance each cycle.', N'loans,deductions,staff', N'Payroll loans', '2026-08-20', NULL, 0),
(3, 1, '2026-08-15', 0, 1, NULL, NULL, NULL, '2026-08-15', NULL, N'payroll-adjustment', 3, N'Correct an already-confirmed pay run, reissue a payslip, and log the reason for the change.', N'corrections,adjustments,payslips', N'Payroll adjustment', '2026-08-15', NULL, 0);
SET IDENTITY_INSERT [Articles] OFF;

SET IDENTITY_INSERT [ArticleSteps] ON;
INSERT INTO [ArticleSteps] ([Id], [ArticleId], [Description], [EstimatedMinutes], [ImageAlt], [ImageUrl], [IsOptional], [SortOrder], [Title], [VideoUrl])
VALUES
(1, 1, N'Go to Payroll from the sidebar, then select New Pay Run in the top-right corner.', 2, NULL, NULL, 0, 1, N'Open the Payroll module', NULL),
(2, 1, N'Choose the start and end dates. Employees included in this range are added automatically.', 3, NULL, NULL, 0, 2, N'Set the pay period', NULL),
(3, 1, N'Check hours, deductions, and totals for each employee, then select Confirm Payroll to finalize.', 5, NULL, NULL, 0, 3, N'Review and confirm', NULL);
SET IDENTITY_INSERT [ArticleSteps] OFF;

INSERT INTO [ArticleTags] ([ArticleId], [TagId])
VALUES (1, 1), (1, 2), (2, 2), (2, 3), (3, 2), (3, 4);

CREATE UNIQUE INDEX [IX_AdminUsers_Username] ON [AdminUsers] ([Username]);
CREATE INDEX [IX_ArticleFeedbacks_ArticleId] ON [ArticleFeedbacks] ([ArticleId]);
CREATE INDEX [IX_Articles_CategoryId] ON [Articles] ([CategoryId]);
CREATE INDEX [IX_Articles_ParentArticleId] ON [Articles] ([ParentArticleId]);
CREATE UNIQUE INDEX [IX_Articles_Slug] ON [Articles] ([Slug]);
CREATE INDEX [IX_ArticleSteps_ArticleId] ON [ArticleSteps] ([ArticleId]);
CREATE INDEX [IX_ArticleStepVersions_ArticleVersionId] ON [ArticleStepVersions] ([ArticleVersionId]);
CREATE INDEX [IX_ArticleTags_TagId] ON [ArticleTags] ([TagId]);
CREATE INDEX [IX_ArticleVersions_ArticleId] ON [ArticleVersions] ([ArticleId]);
CREATE INDEX [IX_AuditLog_AdminUserId] ON [AuditLog] ([AdminUserId]);
CREATE INDEX [IX_AuditLog_EntityType_EntityId] ON [AuditLog] ([EntityType], [EntityId]);
CREATE UNIQUE INDEX [IX_Categories_Slug] ON [Categories] ([Slug]);
CREATE UNIQUE INDEX [IX_Tags_Slug] ON [Tags] ([Slug]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260910092554_InitialCreate', N'8.0.8');

END
GO

IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260911015948_AddCompanyAndNameToFeedback')
BEGIN

ALTER TABLE [ArticleFeedbacks] ADD [Company] nvarchar(max) NOT NULL DEFAULT N'';
ALTER TABLE [ArticleFeedbacks] ADD [Name] nvarchar(max) NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260911015948_AddCompanyAndNameToFeedback', N'8.0.8');

END
GO

/* The very first admin login is normally seeded by the app itself on
   startup (UserService.EnsureSeedAdminAsync, using AdminAuth:Username /
   AdminAuth:Password from appsettings.json) the first time it connects
   to an AdminUsers table with zero rows — you don't need to insert an
   admin row here by hand. */


/* Role/permission matrix behind /admin/roles. Not tied to an EF migration —
   the app itself creates and seeds this table on first startup too (see
   PermissionService.EnsureSchemaAndDefaultsAsync), so running the app once
   would do this step for you. It's included here for a pre-built schema.
   SuperAdmin is intentionally absent — it always has every permission,
   hardcoded in the app, so it can never be locked out from this table. */
IF OBJECT_ID(N'[RolePermissions]') IS NULL
BEGIN
    CREATE TABLE [RolePermissions] (
        [Id] int NOT NULL IDENTITY(1,1),
        [Role] nvarchar(450) NOT NULL,
        [PermissionKey] nvarchar(450) NOT NULL,
        [Allowed] bit NOT NULL,
        CONSTRAINT [PK_RolePermissions] PRIMARY KEY ([Id])
    );
    CREATE UNIQUE INDEX [IX_RolePermissions_Role_PermissionKey] ON [RolePermissions] ([Role], [PermissionKey]);
END
GO

/* Each row guarded individually (not "insert all if table is empty") so this
   is safe to re-run after adding new permission keys later — matches the
   backfill logic in PermissionService.EnsureSchemaAndDefaultsAsync. */
IF NOT EXISTS (SELECT 1 FROM [RolePermissions] WHERE [Role]=N'Admin' AND [PermissionKey]=N'Articles.Manage')
    INSERT INTO [RolePermissions] ([Role],[PermissionKey],[Allowed]) VALUES (N'Admin', N'Articles.Manage', 1);
IF NOT EXISTS (SELECT 1 FROM [RolePermissions] WHERE [Role]=N'Admin' AND [PermissionKey]=N'Categories.Manage')
    INSERT INTO [RolePermissions] ([Role],[PermissionKey],[Allowed]) VALUES (N'Admin', N'Categories.Manage', 1);
IF NOT EXISTS (SELECT 1 FROM [RolePermissions] WHERE [Role]=N'Admin' AND [PermissionKey]=N'Tags.Manage')
    INSERT INTO [RolePermissions] ([Role],[PermissionKey],[Allowed]) VALUES (N'Admin', N'Tags.Manage', 1);
IF NOT EXISTS (SELECT 1 FROM [RolePermissions] WHERE [Role]=N'Admin' AND [PermissionKey]=N'Feedback.Manage')
    INSERT INTO [RolePermissions] ([Role],[PermissionKey],[Allowed]) VALUES (N'Admin', N'Feedback.Manage', 1);
IF NOT EXISTS (SELECT 1 FROM [RolePermissions] WHERE [Role]=N'Admin' AND [PermissionKey]=N'Users.Manage')
    INSERT INTO [RolePermissions] ([Role],[PermissionKey],[Allowed]) VALUES (N'Admin', N'Users.Manage', 1);
IF NOT EXISTS (SELECT 1 FROM [RolePermissions] WHERE [Role]=N'Admin' AND [PermissionKey]=N'AuditLog.View')
    INSERT INTO [RolePermissions] ([Role],[PermissionKey],[Allowed]) VALUES (N'Admin', N'AuditLog.View', 1);
IF NOT EXISTS (SELECT 1 FROM [RolePermissions] WHERE [Role]=N'Admin' AND [PermissionKey]=N'Settings.Manage')
    INSERT INTO [RolePermissions] ([Role],[PermissionKey],[Allowed]) VALUES (N'Admin', N'Settings.Manage', 1);

IF NOT EXISTS (SELECT 1 FROM [RolePermissions] WHERE [Role]=N'Editor' AND [PermissionKey]=N'Articles.Manage')
    INSERT INTO [RolePermissions] ([Role],[PermissionKey],[Allowed]) VALUES (N'Editor', N'Articles.Manage', 1);
IF NOT EXISTS (SELECT 1 FROM [RolePermissions] WHERE [Role]=N'Editor' AND [PermissionKey]=N'Categories.Manage')
    INSERT INTO [RolePermissions] ([Role],[PermissionKey],[Allowed]) VALUES (N'Editor', N'Categories.Manage', 1);
IF NOT EXISTS (SELECT 1 FROM [RolePermissions] WHERE [Role]=N'Editor' AND [PermissionKey]=N'Tags.Manage')
    INSERT INTO [RolePermissions] ([Role],[PermissionKey],[Allowed]) VALUES (N'Editor', N'Tags.Manage', 1);
IF NOT EXISTS (SELECT 1 FROM [RolePermissions] WHERE [Role]=N'Editor' AND [PermissionKey]=N'Feedback.Manage')
    INSERT INTO [RolePermissions] ([Role],[PermissionKey],[Allowed]) VALUES (N'Editor', N'Feedback.Manage', 1);
IF NOT EXISTS (SELECT 1 FROM [RolePermissions] WHERE [Role]=N'Editor' AND [PermissionKey]=N'Users.Manage')
    INSERT INTO [RolePermissions] ([Role],[PermissionKey],[Allowed]) VALUES (N'Editor', N'Users.Manage', 0);
IF NOT EXISTS (SELECT 1 FROM [RolePermissions] WHERE [Role]=N'Editor' AND [PermissionKey]=N'AuditLog.View')
    INSERT INTO [RolePermissions] ([Role],[PermissionKey],[Allowed]) VALUES (N'Editor', N'AuditLog.View', 0);
IF NOT EXISTS (SELECT 1 FROM [RolePermissions] WHERE [Role]=N'Editor' AND [PermissionKey]=N'Settings.Manage')
    INSERT INTO [RolePermissions] ([Role],[PermissionKey],[Allowed]) VALUES (N'Editor', N'Settings.Manage', 1);

IF NOT EXISTS (SELECT 1 FROM [RolePermissions] WHERE [Role]=N'Viewer' AND [PermissionKey]=N'Articles.Manage')
    INSERT INTO [RolePermissions] ([Role],[PermissionKey],[Allowed]) VALUES (N'Viewer', N'Articles.Manage', 0);
IF NOT EXISTS (SELECT 1 FROM [RolePermissions] WHERE [Role]=N'Viewer' AND [PermissionKey]=N'Categories.Manage')
    INSERT INTO [RolePermissions] ([Role],[PermissionKey],[Allowed]) VALUES (N'Viewer', N'Categories.Manage', 0);
IF NOT EXISTS (SELECT 1 FROM [RolePermissions] WHERE [Role]=N'Viewer' AND [PermissionKey]=N'Tags.Manage')
    INSERT INTO [RolePermissions] ([Role],[PermissionKey],[Allowed]) VALUES (N'Viewer', N'Tags.Manage', 0);
IF NOT EXISTS (SELECT 1 FROM [RolePermissions] WHERE [Role]=N'Viewer' AND [PermissionKey]=N'Feedback.Manage')
    INSERT INTO [RolePermissions] ([Role],[PermissionKey],[Allowed]) VALUES (N'Viewer', N'Feedback.Manage', 0);
IF NOT EXISTS (SELECT 1 FROM [RolePermissions] WHERE [Role]=N'Viewer' AND [PermissionKey]=N'Users.Manage')
    INSERT INTO [RolePermissions] ([Role],[PermissionKey],[Allowed]) VALUES (N'Viewer', N'Users.Manage', 0);
IF NOT EXISTS (SELECT 1 FROM [RolePermissions] WHERE [Role]=N'Viewer' AND [PermissionKey]=N'AuditLog.View')
    INSERT INTO [RolePermissions] ([Role],[PermissionKey],[Allowed]) VALUES (N'Viewer', N'AuditLog.View', 0);
IF NOT EXISTS (SELECT 1 FROM [RolePermissions] WHERE [Role]=N'Viewer' AND [PermissionKey]=N'Settings.Manage')
    INSERT INTO [RolePermissions] ([Role],[PermissionKey],[Allowed]) VALUES (N'Viewer', N'Settings.Manage', 0);
GO

/* Helpdesk dashboard settings (demo video + payroll summary shown at
   /helpdesk). Same pattern as RolePermissions above — the app also
   creates and seeds this itself on first startup (SettingsService). */
IF OBJECT_ID(N'[HelpdeskSettings]') IS NULL
BEGIN
    CREATE TABLE [HelpdeskSettings] (
        [Id] int NOT NULL,
        [DemoVideoUrl] nvarchar(max) NULL,
        [DemoVideoTitle] nvarchar(max) NOT NULL,
        [PayrollSummaryTitle] nvarchar(max) NOT NULL,
        [PayrollSummaryBody] nvarchar(max) NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_HelpdeskSettings] PRIMARY KEY ([Id])
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM [HelpdeskSettings])
BEGIN
    INSERT INTO [HelpdeskSettings] ([Id], [DemoVideoUrl], [DemoVideoTitle], [PayrollSummaryTitle], [PayrollSummaryBody], [UpdatedAt])
    VALUES (1, NULL, N'Product Demo', N'Payroll at a glance',
            N'A quick look at what our payroll module handles — pay runs, loans, adjustments, and reporting — all from one place.',
            SYSUTCDATETIME());
END
GO

/* The very first admin login is normally seeded by the app itself on
   startup (UserService.EnsureSeedAdminAsync, using AdminAuth:Username /
   AdminAuth:Password from appsettings.json) the first time it connects
   to an AdminUsers table with zero rows — you don't need to insert an
   admin row here by hand. */

/* Recycle Bin (soft-delete) columns on Categories/Tags/Articles. Same
   pattern as everything above — the app also adds these itself on
   startup (SchemaMaintenanceService.EnsureRecycleBinSchemaAsync). */
IF COL_LENGTH('[Categories]', 'IsDeleted') IS NULL
BEGIN
    ALTER TABLE [Categories] ADD [IsDeleted] bit NOT NULL CONSTRAINT [DF_Categories_IsDeleted] DEFAULT 0;
    ALTER TABLE [Categories] ADD [DeletedAt] datetime2 NULL;
    ALTER TABLE [Categories] ADD [DeletedBy] nvarchar(max) NULL;
END
GO
IF COL_LENGTH('[Tags]', 'IsDeleted') IS NULL
BEGIN
    ALTER TABLE [Tags] ADD [IsDeleted] bit NOT NULL CONSTRAINT [DF_Tags_IsDeleted] DEFAULT 0;
    ALTER TABLE [Tags] ADD [DeletedAt] datetime2 NULL;
    ALTER TABLE [Tags] ADD [DeletedBy] nvarchar(max) NULL;
END
GO
IF COL_LENGTH('[Articles]', 'IsDeleted') IS NULL
BEGIN
    ALTER TABLE [Articles] ADD [IsDeleted] bit NOT NULL CONSTRAINT [DF_Articles_IsDeleted] DEFAULT 0;
    ALTER TABLE [Articles] ADD [DeletedAt] datetime2 NULL;
    ALTER TABLE [Articles] ADD [DeletedBy] nvarchar(max) NULL;
END
GO

/* Subcategories: Categories can now point at a parent Category (one
   level deep). Same pattern again — the app also adds this itself on
   startup (SchemaMaintenanceService.EnsureSubCategorySchemaAsync). */
IF COL_LENGTH('[Categories]', 'ParentCategoryId') IS NULL
BEGIN
    ALTER TABLE [Categories] ADD [ParentCategoryId] int NULL;
    ALTER TABLE [Categories] ADD CONSTRAINT [FK_Categories_Categories_ParentCategoryId]
        FOREIGN KEY ([ParentCategoryId]) REFERENCES [Categories] ([Id]) ON DELETE NO ACTION;
    CREATE INDEX [IX_Categories_ParentCategoryId] ON [Categories] ([ParentCategoryId]);
END
GO

PRINT 'FaqCms database is ready.';
GO

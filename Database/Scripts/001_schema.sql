/*
    RAAMS schema for SQL Server 2022.

    Run in the target database. Timestamps are UTC unless supplied explicitly.
    This bootstrap script creates missing objects; it is not a migration and
    does not reconcile existing table definitions or remove existing grants.
    Applications must update ModifiedDate when modifying a row. Unique keys
    remain reserved by soft-deleted records.

    Primary-key and unique-constraint indexes enforce identity, natural-key
    uniqueness, and the specified role/transition/field combinations.
    Connections modifying Actions must use SQL Server's required SET options
    for filtered indexes, including QUOTED_IDENTIFIER ON and ANSI_NULLS ON.

    The seed matrix is a proposed baseline, not an authorization policy
    approved for production. Review it before enabling access. Permissions
    are additive: reruns do not revoke locally managed role assignments.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.Users (
            Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Users_Id DEFAULT NEWSEQUENTIALID(),
            EntraObjectId UNIQUEIDENTIFIER NOT NULL,
            DisplayName NVARCHAR(255) NOT NULL,
            Email NVARCHAR(255) NOT NULL,
            [Function] NVARCHAR(100) NOT NULL,
            Department NVARCHAR(100) NOT NULL,
            IsActive BIT NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT (1),
            CreatedDate DATETIME2(3) NOT NULL CONSTRAINT DF_Users_CreatedDate DEFAULT SYSUTCDATETIME(),
            ModifiedDate DATETIME2(3) NOT NULL CONSTRAINT DF_Users_ModifiedDate DEFAULT SYSUTCDATETIME(),
            IsDeleted BIT NOT NULL CONSTRAINT DF_Users_IsDeleted DEFAULT (0),
            RowVersion ROWVERSION NOT NULL,
            CONSTRAINT PK_Users PRIMARY KEY (Id),
            CONSTRAINT UQ_Users_EntraObjectId UNIQUE (EntraObjectId),
            CONSTRAINT UQ_Users_Email UNIQUE (Email)
        );
    END;

    IF OBJECT_ID(N'dbo.Roles', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.Roles (
            Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Roles_Id DEFAULT NEWSEQUENTIALID(),
            Name NVARCHAR(100) NOT NULL,
            Description NVARCHAR(500) NULL,
            CONSTRAINT PK_Roles PRIMARY KEY (Id),
            CONSTRAINT UQ_Roles_Name UNIQUE (Name)
        );
    END;

    IF OBJECT_ID(N'dbo.Permissions', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.Permissions (
            Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Permissions_Id DEFAULT NEWSEQUENTIALID(),
            Name NVARCHAR(150) NOT NULL,
            Module NVARCHAR(100) NOT NULL,
            Description NVARCHAR(500) NULL,
            CONSTRAINT PK_Permissions PRIMARY KEY (Id),
            CONSTRAINT UQ_Permissions_Name UNIQUE (Name)
        );
    END;

    IF OBJECT_ID(N'dbo.RolePermissions', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.RolePermissions (
            RoleId UNIQUEIDENTIFIER NOT NULL,
            PermissionId UNIQUEIDENTIFIER NOT NULL,
            CONSTRAINT PK_RolePermissions PRIMARY KEY (RoleId, PermissionId),
            CONSTRAINT FK_RolePermissions_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles (Id) ON DELETE CASCADE,
            CONSTRAINT FK_RolePermissions_Permissions FOREIGN KEY (PermissionId) REFERENCES dbo.Permissions (Id) ON DELETE CASCADE
        );
    END;

    IF OBJECT_ID(N'dbo.UserRoles', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.UserRoles (
            UserId UNIQUEIDENTIFIER NOT NULL,
            RoleId UNIQUEIDENTIFIER NOT NULL,
            EffectiveFrom DATETIME2(3) NOT NULL CONSTRAINT DF_UserRoles_EffectiveFrom DEFAULT SYSUTCDATETIME(),
            EffectiveTo DATETIME2(3) NULL,
            CONSTRAINT PK_UserRoles PRIMARY KEY (UserId, RoleId),
            CONSTRAINT FK_UserRoles_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (Id) ON DELETE CASCADE,
            CONSTRAINT FK_UserRoles_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles (Id) ON DELETE CASCADE,
            CONSTRAINT CK_UserRoles_EffectiveDates CHECK (EffectiveTo IS NULL OR EffectiveTo > EffectiveFrom)
        );
    END;

    IF OBJECT_ID(N'dbo.WorkflowPermissions', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.WorkflowPermissions (
            Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_WorkflowPermissions_Id DEFAULT NEWSEQUENTIALID(),
            RoleId UNIQUEIDENTIFIER NOT NULL,
            EntityType NVARCHAR(100) NOT NULL,
            CurrentStatus NVARCHAR(100) NOT NULL,
            TargetStatus NVARCHAR(100) NOT NULL,
            CanView BIT NOT NULL CONSTRAINT DF_WorkflowPermissions_CanView DEFAULT (0),
            CanEdit BIT NOT NULL CONSTRAINT DF_WorkflowPermissions_CanEdit DEFAULT (0),
            CanApprove BIT NOT NULL CONSTRAINT DF_WorkflowPermissions_CanApprove DEFAULT (0),
            CanReopen BIT NOT NULL CONSTRAINT DF_WorkflowPermissions_CanReopen DEFAULT (0),
            CanDelete BIT NOT NULL CONSTRAINT DF_WorkflowPermissions_CanDelete DEFAULT (0),
            CONSTRAINT PK_WorkflowPermissions PRIMARY KEY (Id),
            CONSTRAINT UQ_WorkflowPermissions_Transition UNIQUE (RoleId, EntityType, CurrentStatus, TargetStatus),
            CONSTRAINT FK_WorkflowPermissions_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles (Id) ON DELETE CASCADE
        );
    END;

    IF OBJECT_ID(N'dbo.FieldPermissions', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FieldPermissions (
            Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_FieldPermissions_Id DEFAULT NEWSEQUENTIALID(),
            RoleId UNIQUEIDENTIFIER NOT NULL,
            EntityType NVARCHAR(100) NOT NULL,
            FieldName NVARCHAR(128) NOT NULL,
            CanView BIT NOT NULL CONSTRAINT DF_FieldPermissions_CanView DEFAULT (0),
            CanEdit BIT NOT NULL CONSTRAINT DF_FieldPermissions_CanEdit DEFAULT (0),
            CONSTRAINT PK_FieldPermissions PRIMARY KEY (Id),
            CONSTRAINT UQ_FieldPermissions_Field UNIQUE (RoleId, EntityType, FieldName),
            CONSTRAINT FK_FieldPermissions_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles (Id) ON DELETE CASCADE
        );
    END;

    IF OBJECT_ID(N'dbo.Delegations', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.Delegations (
            Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Delegations_Id DEFAULT NEWSEQUENTIALID(),
            DelegatorUserId UNIQUEIDENTIFIER NOT NULL,
            DelegateUserId UNIQUEIDENTIFIER NOT NULL,
            EffectiveFrom DATETIME2(3) NOT NULL,
            EffectiveTo DATETIME2(3) NOT NULL,
            Reason NVARCHAR(500) NOT NULL,
            CreatedDate DATETIME2(3) NOT NULL CONSTRAINT DF_Delegations_CreatedDate DEFAULT SYSUTCDATETIME(),
            IsDeleted BIT NOT NULL CONSTRAINT DF_Delegations_IsDeleted DEFAULT (0),
            CONSTRAINT PK_Delegations PRIMARY KEY (Id),
            CONSTRAINT FK_Delegations_Delegator FOREIGN KEY (DelegatorUserId) REFERENCES dbo.Users (Id) ON DELETE NO ACTION,
            CONSTRAINT FK_Delegations_Delegate FOREIGN KEY (DelegateUserId) REFERENCES dbo.Users (Id) ON DELETE NO ACTION,
            CONSTRAINT CK_Delegations_EffectiveDates CHECK (EffectiveTo > EffectiveFrom),
            CONSTRAINT CK_Delegations_DistinctUsers CHECK (DelegatorUserId <> DelegateUserId)
        );
    END;

    IF OBJECT_ID(N'dbo.Assessments', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.Assessments (
            Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Assessments_Id DEFAULT NEWSEQUENTIALID(),
            ReferenceNumber NVARCHAR(100) NOT NULL,
            Name NVARCHAR(255) NOT NULL,
            Scope NVARCHAR(MAX) NOT NULL,
            [Function] NVARCHAR(100) NOT NULL,
            Quarter CHAR(2) NOT NULL,
            [Year] INT NOT NULL,
            LeadAuditorId UNIQUEIDENTIFIER NOT NULL,
            CoordinatorId UNIQUEIDENTIFIER NOT NULL,
            Status NVARCHAR(100) NOT NULL,
            CompletionDate DATETIME2(3) NULL,
            CancellationReason NVARCHAR(500) NULL,
            CreatedDate DATETIME2(3) NOT NULL CONSTRAINT DF_Assessments_CreatedDate DEFAULT SYSUTCDATETIME(),
            ModifiedDate DATETIME2(3) NOT NULL CONSTRAINT DF_Assessments_ModifiedDate DEFAULT SYSUTCDATETIME(),
            IsDeleted BIT NOT NULL CONSTRAINT DF_Assessments_IsDeleted DEFAULT (0),
            RowVersion ROWVERSION NOT NULL,
            CONSTRAINT PK_Assessments PRIMARY KEY (Id),
            CONSTRAINT UQ_Assessments_ReferenceNumber UNIQUE (ReferenceNumber),
            CONSTRAINT FK_Assessments_LeadAuditor FOREIGN KEY (LeadAuditorId) REFERENCES dbo.Users (Id) ON DELETE NO ACTION,
            CONSTRAINT FK_Assessments_Coordinator FOREIGN KEY (CoordinatorId) REFERENCES dbo.Users (Id) ON DELETE NO ACTION,
            CONSTRAINT CK_Assessments_Quarter CHECK (Quarter IN ('Q1', 'Q2', 'Q3', 'Q4'))
        );
    END;

    IF OBJECT_ID(N'dbo.AssessmentStatusHistory', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.AssessmentStatusHistory (
            Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_AssessmentStatusHistory_Id DEFAULT NEWSEQUENTIALID(),
            AssessmentId UNIQUEIDENTIFIER NOT NULL,
            FromStatus NVARCHAR(100) NULL,
            ToStatus NVARCHAR(100) NOT NULL,
            ActorId UNIQUEIDENTIFIER NOT NULL,
            Reason NVARCHAR(500) NULL,
            ChangedAt DATETIME2(3) NOT NULL CONSTRAINT DF_AssessmentStatusHistory_ChangedAt DEFAULT SYSUTCDATETIME(),
            CONSTRAINT PK_AssessmentStatusHistory PRIMARY KEY (Id),
            CONSTRAINT FK_AssessmentStatusHistory_Assessments FOREIGN KEY (AssessmentId) REFERENCES dbo.Assessments (Id) ON DELETE NO ACTION,
            CONSTRAINT FK_AssessmentStatusHistory_Actor FOREIGN KEY (ActorId) REFERENCES dbo.Users (Id) ON DELETE NO ACTION
        );
    END;

    IF OBJECT_ID(N'dbo.Findings', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.Findings (
            Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Findings_Id DEFAULT NEWSEQUENTIALID(),
            AssessmentId UNIQUEIDENTIFIER NOT NULL,
            ReferenceNumber NVARCHAR(100) NOT NULL,
            Description NVARCHAR(MAX) NOT NULL,
            RootCause NVARCHAR(MAX) NOT NULL,
            Recommendation NVARCHAR(MAX) NOT NULL,
            Severity NVARCHAR(100) NOT NULL,
            Status NVARCHAR(100) NOT NULL,
            CreatedDate DATETIME2(3) NOT NULL CONSTRAINT DF_Findings_CreatedDate DEFAULT SYSUTCDATETIME(),
            ModifiedDate DATETIME2(3) NOT NULL CONSTRAINT DF_Findings_ModifiedDate DEFAULT SYSUTCDATETIME(),
            IsDeleted BIT NOT NULL CONSTRAINT DF_Findings_IsDeleted DEFAULT (0),
            RowVersion ROWVERSION NOT NULL,
            CONSTRAINT PK_Findings PRIMARY KEY (Id),
            CONSTRAINT UQ_Findings_ReferenceNumber UNIQUE (ReferenceNumber),
            CONSTRAINT FK_Findings_Assessments FOREIGN KEY (AssessmentId) REFERENCES dbo.Assessments (Id) ON DELETE NO ACTION
        );
    END;

    IF OBJECT_ID(N'dbo.Actions', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.Actions (
            Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Actions_Id DEFAULT NEWSEQUENTIALID(),
            FindingId UNIQUEIDENTIFIER NOT NULL,
            ReferenceNumber NVARCHAR(100) NOT NULL,
            ActionDescription NVARCHAR(MAX) NOT NULL,
            ActionRequired NVARCHAR(MAX) NOT NULL,
            ActionPartyId UNIQUEIDENTIFIER NOT NULL,
            AuditeeId UNIQUEIDENTIFIER NOT NULL,
            TargetDate DATE NOT NULL,
            ActionTaken NVARCHAR(MAX) NULL,
            Status NVARCHAR(100) NOT NULL,
            SubmittedDate DATETIME2(3) NULL,
            ApprovedDate DATETIME2(3) NULL,
            ClosedDate DATETIME2(3) NULL,
            CreatedDate DATETIME2(3) NOT NULL CONSTRAINT DF_Actions_CreatedDate DEFAULT SYSUTCDATETIME(),
            ModifiedDate DATETIME2(3) NOT NULL CONSTRAINT DF_Actions_ModifiedDate DEFAULT SYSUTCDATETIME(),
            IsDeleted BIT NOT NULL CONSTRAINT DF_Actions_IsDeleted DEFAULT (0),
            RowVersion ROWVERSION NOT NULL,
            CONSTRAINT PK_Actions PRIMARY KEY (Id),
            CONSTRAINT UQ_Actions_ReferenceNumber UNIQUE (ReferenceNumber),
            CONSTRAINT FK_Actions_Findings FOREIGN KEY (FindingId) REFERENCES dbo.Findings (Id) ON DELETE NO ACTION,
            CONSTRAINT FK_Actions_ActionParty FOREIGN KEY (ActionPartyId) REFERENCES dbo.Users (Id) ON DELETE NO ACTION,
            CONSTRAINT FK_Actions_Auditee FOREIGN KEY (AuditeeId) REFERENCES dbo.Users (Id) ON DELETE NO ACTION
        );
    END;

    IF OBJECT_ID(N'dbo.ActionStatusHistory', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.ActionStatusHistory (
            Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_ActionStatusHistory_Id DEFAULT NEWSEQUENTIALID(),
            ActionId UNIQUEIDENTIFIER NOT NULL,
            FromStatus NVARCHAR(100) NULL,
            ToStatus NVARCHAR(100) NOT NULL,
            ActorId UNIQUEIDENTIFIER NOT NULL,
            Reason NVARCHAR(500) NULL,
            ChangedAt DATETIME2(3) NOT NULL CONSTRAINT DF_ActionStatusHistory_ChangedAt DEFAULT SYSUTCDATETIME(),
            CONSTRAINT PK_ActionStatusHistory PRIMARY KEY (Id),
            CONSTRAINT FK_ActionStatusHistory_Actions FOREIGN KEY (ActionId) REFERENCES dbo.Actions (Id) ON DELETE NO ACTION,
            CONSTRAINT FK_ActionStatusHistory_Actor FOREIGN KEY (ActorId) REFERENCES dbo.Users (Id) ON DELETE NO ACTION
        );
    END;

    IF OBJECT_ID(N'dbo.Evidence', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.Evidence (
            Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Evidence_Id DEFAULT NEWSEQUENTIALID(),
            ActionId UNIQUEIDENTIFIER NOT NULL,
            FileName NVARCHAR(255) NOT NULL,
            ContentType NVARCHAR(100) NOT NULL,
            SizeBytes BIGINT NOT NULL,
            SharePointItemId NVARCHAR(255) NOT NULL,
            SharePointUrl NVARCHAR(500) NOT NULL,
            [Version] INT NOT NULL,
            FileHash CHAR(64) NOT NULL,
            UploadedById UNIQUEIDENTIFIER NOT NULL,
            UploadedAt DATETIME2(3) NOT NULL CONSTRAINT DF_Evidence_UploadedAt DEFAULT SYSUTCDATETIME(),
            IsDeleted BIT NOT NULL CONSTRAINT DF_Evidence_IsDeleted DEFAULT (0),
            CONSTRAINT PK_Evidence PRIMARY KEY (Id),
            CONSTRAINT FK_Evidence_Actions FOREIGN KEY (ActionId) REFERENCES dbo.Actions (Id) ON DELETE NO ACTION,
            CONSTRAINT FK_Evidence_Uploader FOREIGN KEY (UploadedById) REFERENCES dbo.Users (Id) ON DELETE NO ACTION,
            CONSTRAINT CK_Evidence_SizeBytes CHECK (SizeBytes >= 0),
            CONSTRAINT CK_Evidence_Version CHECK ([Version] > 0)
        );
    END;

    IF OBJECT_ID(N'dbo.Reviews', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.Reviews (
            Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Reviews_Id DEFAULT NEWSEQUENTIALID(),
            ActionId UNIQUEIDENTIFIER NOT NULL,
            ReviewerId UNIQUEIDENTIFIER NOT NULL,
            Outcome NVARCHAR(100) NOT NULL,
            ReasonCode NVARCHAR(100) NULL,
            Comments NVARCHAR(MAX) NOT NULL,
            ReviewedAt DATETIME2(3) NOT NULL CONSTRAINT DF_Reviews_ReviewedAt DEFAULT SYSUTCDATETIME(),
            CONSTRAINT PK_Reviews PRIMARY KEY (Id),
            CONSTRAINT FK_Reviews_Actions FOREIGN KEY (ActionId) REFERENCES dbo.Actions (Id) ON DELETE NO ACTION,
            CONSTRAINT FK_Reviews_Reviewer FOREIGN KEY (ReviewerId) REFERENCES dbo.Users (Id) ON DELETE NO ACTION
        );
    END;

    IF OBJECT_ID(N'dbo.Notifications', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.Notifications (
            Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Notifications_Id DEFAULT NEWSEQUENTIALID(),
            RecipientEmail NVARCHAR(255) NOT NULL,
            RecipientUserId UNIQUEIDENTIFIER NULL,
            Subject NVARCHAR(500) NOT NULL,
            Body NVARCHAR(MAX) NOT NULL,
            Channel NVARCHAR(100) NOT NULL,
            EventType NVARCHAR(100) NOT NULL,
            RelatedEntityType NVARCHAR(100) NULL,
            RelatedEntityId UNIQUEIDENTIFIER NULL,
            SentAt DATETIME2(3) NULL,
            DeliveryStatus NVARCHAR(100) NOT NULL,
            ErrorMessage NVARCHAR(MAX) NULL,
            CONSTRAINT PK_Notifications PRIMARY KEY (Id),
            CONSTRAINT FK_Notifications_Recipient FOREIGN KEY (RecipientUserId) REFERENCES dbo.Users (Id) ON DELETE NO ACTION
        );
    END;

    IF OBJECT_ID(N'dbo.AuditTrails', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.AuditTrails (
            Id BIGINT IDENTITY(1,1) NOT NULL,
            EntityType NVARCHAR(100) NOT NULL,
            EntityId UNIQUEIDENTIFIER NOT NULL,
            [Action] NVARCHAR(100) NOT NULL,
            OldValue NVARCHAR(MAX) NULL,
            NewValue NVARCHAR(MAX) NULL,
            ChangedFields NVARCHAR(MAX) NULL,
            UserId UNIQUEIDENTIFIER NULL,
            UserEmail NVARCHAR(255) NULL,
            [Timestamp] DATETIME2(3) NOT NULL CONSTRAINT DF_AuditTrails_Timestamp DEFAULT SYSUTCDATETIME(),
            CorrelationId UNIQUEIDENTIFIER NULL,
            Reason NVARCHAR(500) NULL,
            CONSTRAINT PK_AuditTrails PRIMARY KEY (Id),
            CONSTRAINT FK_AuditTrails_User FOREIGN KEY (UserId) REFERENCES dbo.Users (Id) ON DELETE NO ACTION
        );
    END;

    -- Supports assessment lists filtered by status and function.
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Assessments') AND name = N'IX_Assessments_Status_Function')
        CREATE NONCLUSTERED INDEX IX_Assessments_Status_Function ON dbo.Assessments (Status, [Function]);

    -- Supports action-party work queues filtered by status.
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Actions') AND name = N'IX_Actions_ActionPartyId_Status')
        CREATE NONCLUSTERED INDEX IX_Actions_ActionPartyId_Status ON dbo.Actions (ActionPartyId, Status);

    -- Supports action deadline scans and status filtering.
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Actions') AND name = N'IX_Actions_TargetDate_Status')
        CREATE NONCLUSTERED INDEX IX_Actions_TargetDate_Status ON dbo.Actions (TargetDate, Status);

    -- Supports deadline scans for open, non-deleted actions.
    -- SQL Server filtered-index predicates do not support IN or OR. These three
    -- indexes cover the same active status subsets as the requested IN filter.
    -- An overdue query must additionally compare TargetDate with today's date.
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Actions') AND name = N'IX_Actions_Overdue_Open')
        CREATE NONCLUSTERED INDEX IX_Actions_Overdue_Open ON dbo.Actions (TargetDate) INCLUDE (Status, IsDeleted) WHERE Status = N'Open' AND IsDeleted = 0;
    -- Supports deadline scans for submitted, non-deleted actions.
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Actions') AND name = N'IX_Actions_Overdue_Submitted')
        CREATE NONCLUSTERED INDEX IX_Actions_Overdue_Submitted ON dbo.Actions (TargetDate) INCLUDE (Status, IsDeleted) WHERE Status = N'Submitted' AND IsDeleted = 0;
    -- Supports deadline scans for reopened, non-deleted actions.
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Actions') AND name = N'IX_Actions_Overdue_Reopened')
        CREATE NONCLUSTERED INDEX IX_Actions_Overdue_Reopened ON dbo.Actions (TargetDate) INCLUDE (Status, IsDeleted) WHERE Status = N'Reopened' AND IsDeleted = 0;

    -- Supports finding lists for an assessment and status.
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Findings') AND name = N'IX_Findings_AssessmentId_Status')
        CREATE NONCLUSTERED INDEX IX_Findings_AssessmentId_Status ON dbo.Findings (AssessmentId, Status);

    -- History has ToStatus rather than Status; index the destination status.
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ActionStatusHistory') AND name = N'IX_ActionStatusHistory_ActionId_ToStatus')
        CREATE NONCLUSTERED INDEX IX_ActionStatusHistory_ActionId_ToStatus ON dbo.ActionStatusHistory (ActionId, ToStatus);

    -- Supports notification delivery history by recipient and sent time.
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Notifications') AND name = N'IX_Notifications_RecipientEmail_SentAt')
        CREATE NONCLUSTERED INDEX IX_Notifications_RecipientEmail_SentAt ON dbo.Notifications (RecipientEmail, SentAt);

    -- Supports chronological audit retrieval for a specific entity.
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.AuditTrails') AND name = N'IX_AuditTrails_EntityType_EntityId_Timestamp')
        CREATE NONCLUSTERED INDEX IX_AuditTrails_EntityType_EntityId_Timestamp ON dbo.AuditTrails (EntityType, EntityId, [Timestamp]);

    MERGE dbo.Roles WITH (HOLDLOCK) AS target
    USING (VALUES
        (N'GRA', N'Global Risk and Assurance'),
        (N'LeadAuditor', N'Leads assessments and findings'),
        (N'AssuranceCoordinator', N'Coordinates assurance work'),
        (N'Auditee', N'Participates in assessments and actions'),
        (N'ActionParty', N'Owns and submits assigned actions'),
        (N'Reviewer', N'Reviews submitted actions'),
        (N'Sponsor', N'Views assurance information and reports'),
        (N'SuperAdmin', N'Administers the system')
    ) AS source (Name, Description)
    ON target.Name = source.Name
    WHEN NOT MATCHED BY TARGET THEN
        INSERT (Name, Description) VALUES (source.Name, source.Description);

    MERGE dbo.Permissions WITH (HOLDLOCK) AS target
    USING (VALUES
        (N'Assessment.View', N'Assessments', N'View assessments'),
        (N'Assessment.Create', N'Assessments', N'Create assessments'),
        (N'Assessment.Edit', N'Assessments', N'Edit assessments'),
        (N'Assessment.Submit', N'Assessments', N'Submit assessments'),
        (N'Assessment.Approve', N'Assessments', N'Approve assessments'),
        (N'Assessment.Cancel', N'Assessments', N'Cancel assessments'),
        (N'Assessment.Reopen', N'Assessments', N'Reopen assessments'),
        (N'Finding.View', N'Findings', N'View findings'),
        (N'Finding.Create', N'Findings', N'Create findings'),
        (N'Finding.Edit', N'Findings', N'Edit findings'),
        (N'Finding.Delete', N'Findings', N'Delete findings'),
        (N'Action.View', N'Actions', N'View actions'),
        (N'Action.Create', N'Actions', N'Create actions'),
        (N'Action.Edit', N'Actions', N'Edit actions'),
        (N'Action.Submit', N'Actions', N'Submit actions'),
        (N'Action.Approve', N'Actions', N'Approve actions'),
        (N'Action.Reopen', N'Actions', N'Reopen actions'),
        (N'Action.Close', N'Actions', N'Close actions'),
        (N'Evidence.View', N'Evidence', N'View evidence'),
        (N'Evidence.Upload', N'Evidence', N'Upload evidence'),
        (N'Evidence.Delete', N'Evidence', N'Delete evidence'),
        (N'Review.Create', N'Reviews', N'Create reviews'),
        (N'Reporting.View', N'Reporting', N'View reports'),
        (N'Administration.Users', N'Administration', N'Manage users'),
        (N'Administration.Security', N'Administration', N'Manage roles and permissions')
    ) AS source (Name, Module, Description)
    ON target.Name = source.Name
    WHEN NOT MATCHED BY TARGET THEN
        INSERT (Name, Module, Description) VALUES (source.Name, source.Module, source.Description);

    -- These permissions are coarse capabilities, not record-level access.
    -- Applications must also enforce assignment, delegation, workflow and
    -- field-level authorization; no workflow/field grants are seeded here.
    ;WITH GrantNames AS (
        SELECT v.RoleName, v.PermissionName
        FROM (VALUES
            (N'LeadAuditor', N'Assessment.View'), (N'LeadAuditor', N'Assessment.Create'),
            (N'LeadAuditor', N'Assessment.Edit'), (N'LeadAuditor', N'Assessment.Submit'),
            (N'LeadAuditor', N'Finding.View'), (N'LeadAuditor', N'Finding.Create'),
            (N'LeadAuditor', N'Finding.Edit'), (N'LeadAuditor', N'Action.View'),
            (N'LeadAuditor', N'Action.Create'), (N'LeadAuditor', N'Evidence.View'),
            (N'LeadAuditor', N'Reporting.View'),
            (N'AssuranceCoordinator', N'Assessment.View'), (N'AssuranceCoordinator', N'Assessment.Create'),
            (N'AssuranceCoordinator', N'Assessment.Edit'), (N'AssuranceCoordinator', N'Finding.View'),
            (N'AssuranceCoordinator', N'Finding.Create'), (N'AssuranceCoordinator', N'Finding.Edit'),
            (N'AssuranceCoordinator', N'Action.View'), (N'AssuranceCoordinator', N'Action.Create'),
            (N'AssuranceCoordinator', N'Evidence.View'), (N'AssuranceCoordinator', N'Reporting.View'),
            (N'Auditee', N'Assessment.View'), (N'Auditee', N'Finding.View'),
            (N'Auditee', N'Action.View'), (N'Auditee', N'Action.Approve'),
            (N'Auditee', N'Evidence.View'), (N'Auditee', N'Evidence.Upload'),
            (N'ActionParty', N'Finding.View'), (N'ActionParty', N'Action.View'),
            (N'ActionParty', N'Action.Edit'), (N'ActionParty', N'Action.Submit'),
            (N'ActionParty', N'Evidence.View'), (N'ActionParty', N'Evidence.Upload'),
            (N'Reviewer', N'Assessment.View'), (N'Reviewer', N'Finding.View'),
            (N'Reviewer', N'Action.View'), (N'Reviewer', N'Action.Reopen'),
            (N'Reviewer', N'Evidence.View'), (N'Reviewer', N'Review.Create'),
            (N'Sponsor', N'Assessment.View'), (N'Sponsor', N'Finding.View'),
            (N'Sponsor', N'Action.View'), (N'Sponsor', N'Reporting.View')
        ) AS v (RoleName, PermissionName)
        UNION ALL
        SELECT N'GRA', p.Name FROM dbo.Permissions AS p
        WHERE p.Name IN (
            N'Assessment.View', N'Assessment.Create', N'Assessment.Edit', N'Assessment.Submit',
            N'Assessment.Approve', N'Assessment.Cancel', N'Assessment.Reopen',
            N'Finding.View', N'Finding.Create', N'Finding.Edit', N'Finding.Delete',
            N'Action.View', N'Action.Create', N'Action.Edit', N'Action.Submit',
            N'Action.Approve', N'Action.Reopen', N'Action.Close',
            N'Evidence.View', N'Evidence.Upload', N'Evidence.Delete',
            N'Review.Create', N'Reporting.View'
        )
        UNION ALL
        SELECT N'SuperAdmin', p.Name FROM dbo.Permissions AS p
        WHERE p.Name IN (
            N'Assessment.View', N'Assessment.Create', N'Assessment.Edit', N'Assessment.Submit',
            N'Assessment.Approve', N'Assessment.Cancel', N'Assessment.Reopen',
            N'Finding.View', N'Finding.Create', N'Finding.Edit', N'Finding.Delete',
            N'Action.View', N'Action.Create', N'Action.Edit', N'Action.Submit',
            N'Action.Approve', N'Action.Reopen', N'Action.Close',
            N'Evidence.View', N'Evidence.Upload', N'Evidence.Delete',
            N'Review.Create', N'Reporting.View', N'Administration.Users', N'Administration.Security'
        )
    )
    MERGE dbo.RolePermissions WITH (HOLDLOCK) AS target
    USING (
        SELECT r.Id AS RoleId, p.Id AS PermissionId
        FROM GrantNames AS g
        INNER JOIN dbo.Roles AS r ON r.Name = g.RoleName
        INNER JOIN dbo.Permissions AS p ON p.Name = g.PermissionName
    ) AS source
    ON target.RoleId = source.RoleId AND target.PermissionId = source.PermissionId
    WHEN NOT MATCHED BY TARGET THEN
        INSERT (RoleId, PermissionId) VALUES (source.RoleId, source.PermissionId);

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

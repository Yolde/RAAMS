/*
Optional reset for an intentional full reseed; this removes all existing rules:
DELETE FROM dbo.WorkflowPermissions;
DELETE FROM dbo.FieldPermissions;

This seed inserts missing rules only. Existing rules are not changed or revoked.
Action.Rejected and Assessment.Reopened are not yet members of the corresponding
domain enums; the application must support them before those transitions can run.
EffectivenessRating and User.Role are policy field names, not columns in 001_schema.
EffectivenessRating defaults to no view or edit for roles other than GRA and Reviewer.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @Roles TABLE (Name NVARCHAR(100) NOT NULL PRIMARY KEY);
    INSERT INTO @Roles (Name)
    VALUES (N'GRA'), (N'LeadAuditor'), (N'AssuranceCoordinator'), (N'Auditee'),
           (N'ActionParty'), (N'Reviewer'), (N'Sponsor'), (N'SuperAdmin');

    IF EXISTS (
        SELECT 1
        FROM @Roles AS expected
        WHERE NOT EXISTS (SELECT 1 FROM dbo.Roles AS actual WHERE actual.Name = expected.Name)
    )
        THROW 51000, 'RBAC seed requires all eight roles from 001_schema.sql.', 1;

    DECLARE @WorkflowRules TABLE (
        RoleName NVARCHAR(100) NOT NULL,
        EntityType NVARCHAR(100) NOT NULL,
        CurrentStatus NVARCHAR(100) NOT NULL,
        TargetStatus NVARCHAR(100) NOT NULL,
        CanView BIT NOT NULL,
        CanEdit BIT NOT NULL,
        CanApprove BIT NOT NULL,
        CanReopen BIT NOT NULL,
        CanDelete BIT NOT NULL,
        PRIMARY KEY (RoleName, EntityType, CurrentStatus, TargetStatus)
    );

    INSERT INTO @WorkflowRules
        (RoleName, EntityType, CurrentStatus, TargetStatus, CanView, CanEdit, CanApprove, CanReopen, CanDelete)
    VALUES
        (N'ActionParty', N'Action', N'Open', N'Submitted', 1, 1, 0, 0, 0),
        (N'Auditee', N'Action', N'Open', N'Submitted', 1, 0, 0, 0, 0),
        (N'Auditee', N'Action', N'Submitted', N'Approved', 1, 0, 1, 0, 0),
        (N'Auditee', N'Action', N'Submitted', N'Rejected', 1, 0, 1, 0, 0),
        (N'GRA', N'Action', N'Approved', N'Closed', 1, 1, 0, 0, 0),
        (N'Reviewer', N'Action', N'Closed', N'Reopened', 1, 0, 0, 1, 0),
        (N'GRA', N'Action', N'Closed', N'Reopened', 1, 0, 0, 1, 0),
        (N'ActionParty', N'Action', N'Reopened', N'Submitted', 1, 1, 0, 0, 0),
        (N'LeadAuditor', N'Assessment', N'Planned', N'Started', 1, 1, 0, 0, 0),
        (N'GRA', N'Assessment', N'Planned', N'Started', 1, 1, 0, 0, 0),
        (N'LeadAuditor', N'Assessment', N'Started', N'DraftReport', 1, 1, 0, 0, 0),
        (N'GRA', N'Assessment', N'Started', N'DraftReport', 1, 1, 0, 0, 0),
        (N'LeadAuditor', N'Assessment', N'DraftReport', N'Completed', 1, 1, 0, 0, 0),
        (N'GRA', N'Assessment', N'DraftReport', N'Completed', 1, 1, 0, 0, 0),
        (N'GRA', N'Assessment', N'Planned', N'Deferred', 1, 1, 0, 0, 0),
        (N'GRA', N'Assessment', N'Planned', N'Cancelled', 1, 1, 0, 0, 0),
        (N'LeadAuditor', N'Assessment', N'Planned', N'Cancelled', 1, 1, 0, 0, 0),
        (N'GRA', N'Assessment', N'Completed', N'Reopened', 1, 0, 0, 1, 0);

    MERGE dbo.WorkflowPermissions WITH (HOLDLOCK) AS target
    USING (
        SELECT role.Id AS RoleId, wr.EntityType, wr.CurrentStatus, wr.TargetStatus,
               wr.CanView, wr.CanEdit, wr.CanApprove, wr.CanReopen, wr.CanDelete
        FROM @WorkflowRules AS wr
        INNER JOIN dbo.Roles AS role ON role.Name = wr.RoleName
    ) AS source
    ON target.RoleId = source.RoleId
       AND target.EntityType = source.EntityType
       AND target.CurrentStatus = source.CurrentStatus
       AND target.TargetStatus = source.TargetStatus
    WHEN NOT MATCHED BY TARGET THEN
        INSERT (RoleId, EntityType, CurrentStatus, TargetStatus, CanView, CanEdit, CanApprove, CanReopen, CanDelete)
        VALUES (source.RoleId, source.EntityType, source.CurrentStatus, source.TargetStatus,
                source.CanView, source.CanEdit, source.CanApprove, source.CanReopen, source.CanDelete);

    DECLARE @FieldRules TABLE (
        RoleName NVARCHAR(100) NOT NULL,
        EntityType NVARCHAR(100) NOT NULL,
        FieldName NVARCHAR(128) NOT NULL,
        CanView BIT NOT NULL,
        CanEdit BIT NOT NULL,
        PRIMARY KEY (RoleName, EntityType, FieldName)
    );

    INSERT INTO @FieldRules (RoleName, EntityType, FieldName, CanView, CanEdit)
    VALUES
        (N'GRA', N'Action', N'TargetDate', 1, 1),
        (N'LeadAuditor', N'Action', N'TargetDate', 1, 0),
        (N'AssuranceCoordinator', N'Action', N'TargetDate', 1, 0),
        (N'ActionParty', N'Action', N'TargetDate', 0, 0),
        (N'Auditee', N'Action', N'TargetDate', 1, 0),
        (N'Reviewer', N'Action', N'TargetDate', 1, 0),
        (N'GRA', N'Action', N'ActionTaken', 1, 1),
        (N'ActionParty', N'Action', N'ActionTaken', 1, 1),
        (N'Auditee', N'Action', N'ActionTaken', 1, 1),
        (N'AssuranceCoordinator', N'Action', N'ActionTaken', 1, 0),
        (N'Reviewer', N'Action', N'ActionTaken', 1, 0),
        (N'Sponsor', N'Action', N'ActionTaken', 0, 0),
        (N'GRA', N'Action', N'Status', 1, 1),
        (N'ActionParty', N'Action', N'Status', 1, 0),
        (N'Auditee', N'Action', N'Status', 1, 0),
        (N'Reviewer', N'Action', N'Status', 1, 0),
        (N'GRA', N'User', N'Role', 1, 1),
        (N'SuperAdmin', N'User', N'Role', 1, 1);

    -- The eight named seed roles are the scope of "everyone else" and "others".
    INSERT INTO @FieldRules (RoleName, EntityType, FieldName, CanView, CanEdit)
    SELECT role.Name, N'Action', field.FieldName, 1,
           CASE WHEN role.Name = N'GRA' THEN 1 ELSE 0 END
    FROM @Roles AS role
    CROSS JOIN (VALUES (N'ActionPartyId'), (N'AuditeeId'), (N'ReferenceNumber')) AS field (FieldName);

    INSERT INTO @FieldRules (RoleName, EntityType, FieldName, CanView, CanEdit)
    SELECT Name, N'Action', N'EffectivenessRating',
           CASE WHEN Name IN (N'GRA', N'Reviewer') THEN 1 ELSE 0 END,
           CASE WHEN Name IN (N'GRA', N'Reviewer') THEN 1 ELSE 0 END
    FROM @Roles;

    INSERT INTO @FieldRules (RoleName, EntityType, FieldName, CanView, CanEdit)
    SELECT role.Name, N'User', field.FieldName, 1,
           CASE WHEN role.Name = N'GRA' THEN 1 ELSE 0 END
    FROM @Roles AS role
    CROSS JOIN (VALUES (N'Function'), (N'Department')) AS field (FieldName);

    MERGE dbo.FieldPermissions WITH (HOLDLOCK) AS target
    USING (
        SELECT role.Id AS RoleId, wr.EntityType, wr.FieldName, wr.CanView, wr.CanEdit
        FROM @FieldRules AS wr
        INNER JOIN dbo.Roles AS role ON role.Name = wr.RoleName
    ) AS source
    ON target.RoleId = source.RoleId
       AND target.EntityType = source.EntityType
       AND target.FieldName = source.FieldName
    WHEN NOT MATCHED BY TARGET THEN
        INSERT (RoleId, EntityType, FieldName, CanView, CanEdit)
        VALUES (source.RoleId, source.EntityType, source.FieldName, source.CanView, source.CanEdit);

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

-- SELECT N'WorkflowPermissions' AS TableName, COUNT_BIG(*) AS RowCount FROM dbo.WorkflowPermissions
-- UNION ALL
-- SELECT N'FieldPermissions', COUNT_BIG(*) FROM dbo.FieldPermissions;

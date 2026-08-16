-- Phase 5 — Security and Tenancy Tables
-- Migration 005: Create 14 security metadata tables for multi-client/organization/role access control

-- ------------------------------------------------------------------
-- 1. SysClient — tenant/client organisation
-- ------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "SysClient" (
    "SysClient_ID"    SERIAL              PRIMARY KEY,
    "Code"            VARCHAR(30)         NOT NULL UNIQUE,
    "Name"            VARCHAR(120)        NOT NULL,
    "Description"     TEXT,
    "IsActive"        BOOLEAN             NOT NULL DEFAULT TRUE,
    "CreatedAt"       TIMESTAMPTZ         NOT NULL DEFAULT NOW(),
    "UpdatedAt"       TIMESTAMPTZ
);

COMMENT ON TABLE "SysClient" IS 'Top-level tenant/client definitions.';
CREATE UNIQUE INDEX IF NOT EXISTS uq_sys_client_code ON "SysClient" ("Code");

-- ------------------------------------------------------------------
-- 2. SysOrg — sub-tenant within a client
-- ------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "SysOrg" (
    "SysOrg_ID"       SERIAL              PRIMARY KEY,
    "SysClient_ID"    INT                 NOT NULL REFERENCES "SysClient" ("SysClient_ID"),
    "Code"            VARCHAR(30)         NOT NULL,
    "Name"            VARCHAR(120)        NOT NULL,
    "Description"     TEXT,
    "IsActive"        BOOLEAN             NOT NULL DEFAULT TRUE,
    "CreatedAt"       TIMESTAMPTZ         NOT NULL DEFAULT NOW(),
    "UpdatedAt"       TIMESTAMPTZ,
    UNIQUE ("SysClient_ID", "Code")
);

COMMENT ON TABLE "SysOrg" IS 'Organisations within a client (sub-tenant).'';
CREATE INDEX IF NOT EXISTS ix_sys_org_client ON "SysOrg" ("SysClient_ID");
CREATE INDEX IF NOT EXISTS ix_sys_org_is_active ON "SysOrg" ("IsActive") WHERE "IsActive" = TRUE;

-- ------------------------------------------------------------------
-- 3. SysUser — application user with password hash
-- ------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "SysUser" (
    "SysUser_ID"      SERIAL              PRIMARY KEY,
    "Username"        VARCHAR(60)         NOT NULL UNIQUE,
    "PasswordHash"    TEXT                NOT NULL,
    "Name"            VARCHAR(120)        NOT NULL,
    "Email"           VARCHAR(120),
    "SysClient_ID"    INT                 NOT NULL REFERENCES "SysClient" ("SysClient_ID"),
    "SysOrg_ID"       INT                 REFERENCES "SysOrg" ("SysOrg_ID"),
    "IsActive"        BOOLEAN             NOT NULL DEFAULT TRUE,
    "FailedLoginAttempts" INT             NOT NULL DEFAULT 0,
    "LockedUntil"       TIMESTAMPTZ,
    "CreatedAt"       TIMESTAMPTZ         NOT NULL DEFAULT NOW(),
    "UpdatedAt"       TIMESTAMPTZ
);

COMMENT ON TABLE "SysUser" IS 'Application user accounts with password hashes.';
CREATE UNIQUE INDEX IF NOT EXISTS uq_sys_user_username ON "SysUser" ("Username");
CREATE INDEX IF NOT EXISTS ix_sys_user_client ON "SysUser" ("SysClient_ID");
CREATE INDEX IF NOT EXISTS ix_sys_org ON "SysUser" ("SysOrg_ID");

-- ------------------------------------------------------------------
-- 4. SysRole — role definitions
-- ------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "SysRole" (
    "SysRole_ID"      SERIAL              PRIMARY KEY,
    "SysClient_ID"    INT                 REFERENCES "SysClient" ("SysClient_ID"),
    "Name"            VARCHAR(60)         NOT NULL UNIQUE,
    "Description"     TEXT,
    "IsActive"        BOOLEAN             NOT NULL DEFAULT TRUE,
    "CreatedAt"       TIMESTAMPTZ         NOT NULL DEFAULT NOW(),
    "UpdatedAt"       TIMESTAMPTZ
);

COMMENT ON TABLE "SysRole" IS 'Role definitions (scoped to client or global).'';
CREATE INDEX IF NOT EXISTS ix_sys_role_client ON "SysRole" ("SysClient_ID");

-- ------------------------------------------------------------------
-- 5. SysUserRoles — many-to-many user ↔ role
-- ------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "SysUserRoles" (
    "SysUser_ID"      INT                 NOT NULL REFERENCES "SysUser" ("SysUser_ID") ON DELETE CASCADE,
    "SysRole_ID"      INT                 NOT NULL REFERENCES "SysRole" ("SysRole_ID") ON DELETE CASCADE,
    PRIMARY KEY ("SysUser_ID", "SysRole_ID")
);

COMMENT ON TABLE "SysUserRoles" IS 'Many-to-many mapping between users and roles.';

-- ------------------------------------------------------------------
-- 6. SysRoleOrgAccess — org-level role grants
-- ------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "SysRoleOrgAccess" (
    "SysRoleOrgAccess_ID"  SERIAL          PRIMARY KEY,
    "SysRole_ID"           INT             NOT NULL REFERENCES "SysRole" ("SysRole_ID") ON DELETE CASCADE,
    "SysClient_ID"         INT             NOT NULL REFERENCES "SysClient" ("SysClient_ID"),
    "SysOrg_ID"            INT             REFERENCES "SysOrg" ("SysOrg_ID"),
    "Permission"           SMALLINT        NOT NULL DEFAULT 1,
    "CreatedAt"            TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE "SysRoleOrgAccess" IS 'Role grants at the organisation level.';
CREATE INDEX IF NOT EXISTS ix_sys_role_org_role ON "SysRoleOrgAccess" ("SysRole_ID");

-- ------------------------------------------------------------------
-- 7. SysUserOrgAccess — direct user org grants (bypass role)
-- ------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "SysUserOrgAccess" (
    "SysUserOrgAccess_ID" SERIAL         PRIMARY KEY,
    "SysUser_ID"          INT           NOT NULL REFERENCES "SysUser" ("SysUser_ID") ON DELETE CASCADE,
    "SysClient_ID"        INT           NOT NULL REFERENCES "SysClient" ("SysClient_ID"),
    "SysOrg_ID"           INT           REFERENCES "SysOrg" ("SysOrg_ID"),
    "Permission"          SMALLINT      NOT NULL DEFAULT 1,
    "CreatedAt"           TIMESTAMPTZ   NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE "SysUserOrgAccess" IS 'Direct user-to-org access grants.';
CREATE INDEX IF NOT EXISTS ix_sys_user_org_user ON "SysUserOrgAccess" ("SysUser_ID");

-- ------------------------------------------------------------------
-- 8. SysRoleWindowAccess — window/screen access control
-- ------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "SysRoleWindowAccess" (
    "SysRoleWindowAccess_ID"  SERIAL      PRIMARY KEY,
    "SysRole_ID"              INT         NOT NULL REFERENCES "SysRole" ("SysRole_ID") ON DELETE CASCADE,
    "SysClient_ID"            INT         NOT NULL REFERENCES "SysClient" ("SysClient_ID"),
    "SysWindow_ID"            INT         NOT NULL REFERENCES "SysWindow" ("SysWindow_ID"),
    "Permission"              SMALLINT    NOT NULL DEFAULT 0,
    "CreatedAt"               TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE "SysRoleWindowAccess" IS 'Role access to UI windows/screens.';
CREATE INDEX IF NOT EXISTS ix_sys_role_window_role ON "SysRoleWindowAccess" ("SysRole_ID");
CREATE INDEX IF NOT EXISTS ix_sys_role_window_window ON "SysRoleWindowAccess" ("SysWindow_ID");

-- ------------------------------------------------------------------
-- 9. SysRoleProcessAccess — process access control
-- ------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "SysRoleProcessAccess" (
    "SysRoleProcessAccess_ID" SERIAL     PRIMARY KEY,
    "SysRole_ID"              INT         NOT NULL REFERENCES "SysRole" ("SysRole_ID") ON DELETE CASCADE,
    "SysClient_ID"            INT         NOT NULL REFERENCES "SysClient" ("SysClient_ID"),
    "SysProcess_ID"           INT,
    "Permission"              SMALLINT    NOT NULL DEFAULT 0,
    "CreatedAt"               TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE "SysRoleProcessAccess" IS 'Role access to processes. SysProcess_ID is nullable until SysProcess table is created in a later migration.';
CREATE INDEX IF NOT EXISTS ix_sys_role_process_role ON "SysRoleProcessAccess" ("SysRole_ID");

-- ------------------------------------------------------------------
-- 10. SysRoleTableAccess — table/data access control
-- ------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "SysRoleTableAccess" (
    "SysRoleTableAccess_ID"   SERIAL      PRIMARY KEY,
    "SysRole_ID"              INT         NOT NULL REFERENCES "SysRole" ("SysRole_ID") ON DELETE CASCADE,
    "SysClient_ID"            INT         NOT NULL REFERENCES "SysClient" ("SysClient_ID"),
    "SysTable_ID"             INT         NOT NULL REFERENCES "SysTable" ("SysTable_ID"),
    "Permission"              SMALLINT    NOT NULL DEFAULT 0,
    "CreatedAt"               TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE "SysRoleTableAccess" IS 'Role access to data tables.';
CREATE INDEX IF NOT EXISTS ix_sys_role_table_role ON "SysRoleTableAccess" ("SysRole_ID");
CREATE INDEX IF NOT EXISTS ix_sys_role_table_table ON "SysRoleTableAccess" ("SysTable_ID");

-- ------------------------------------------------------------------
-- 11. SysRoleColumnAccess — field-level access control
-- ------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "SysRoleColumnAccess" (
    "SysRoleColumnAccess_ID"  SERIAL      PRIMARY KEY,
    "SysRole_ID"              INT         NOT NULL REFERENCES "SysRole" ("SysRole_ID") ON DELETE CASCADE,
    "SysClient_ID"            INT         NOT NULL REFERENCES "SysClient" ("SysClient_ID"),
    "SysTable_ID"             INT         NOT NULL REFERENCES "SysTable" ("SysTable_ID"),
    "SysColumn_ID"            INT         NOT NULL REFERENCES "SysColumn" ("SysColumn_ID"),
    "Permission"              SMALLINT    NOT NULL DEFAULT 0,
    "CreatedAt"               TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE "SysRoleColumnAccess" IS 'Role access to individual columns/fields.';
CREATE INDEX IF NOT EXISTS ix_sys_role_column_role ON "SysRoleColumnAccess" ("SysRole_ID");
CREATE INDEX IF NOT EXISTS ix_sys_role_column_table ON "SysRoleColumnAccess" ("SysTable_ID");
CREATE INDEX IF NOT EXISTS ix_sys_role_column_column ON "SysRoleColumnAccess" ("SysColumn_ID");

-- ------------------------------------------------------------------
-- 12. SysRecordAccess — row-level (record) access control
-- ------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "SysRecordAccess" (
    "SysRecordAccess_ID"  SERIAL      PRIMARY KEY,
    "SysRole_ID"          INT         NOT NULL REFERENCES "SysRole" ("SysRole_ID") ON DELETE CASCADE,
    "SysClient_ID"        INT         NOT NULL REFERENCES "SysClient" ("SysClient_ID"),
    "SysTable_ID"         INT         NOT NULL REFERENCES "SysTable" ("SysTable_ID"),
    "FilterExpression"    TEXT,
    "Permission"          SMALLINT    NOT NULL DEFAULT 0,
    "CreatedAt"           TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE "SysRecordAccess" IS 'Row-level access filters for roles.';
CREATE INDEX IF NOT EXISTS ix_sys_record_access_role ON "SysRecordAccess" ("SysRole_ID");

-- ------------------------------------------------------------------
-- 13. SysPrivateAccess — per-user private record ownership
-- ------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "SysPrivateAccess" (
    "SysPrivateAccess_ID"   SERIAL      PRIMARY KEY,
    "SysUser_ID"            INT         NOT NULL REFERENCES "SysUser" ("SysUser_ID") ON DELETE CASCADE,
    "SysClient_ID"          INT         NOT NULL REFERENCES "SysClient" ("SysClient_ID"),
    "SysTable_ID"           INT         NOT NULL REFERENCES "SysTable" ("SysTable_ID"),
    "RecordId"              INT,
    "CreatedAt"             TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE "SysPrivateAccess" IS 'Per-user private record ownership.';
CREATE INDEX IF NOT EXISTS ix_sys_private_user ON "SysPrivateAccess" ("SysUser_ID");
CREATE INDEX IF NOT EXISTS ix_sys_private_table ON "SysPrivateAccess" ("SysTable_ID");

-- ------------------------------------------------------------------
-- 14. SysSession — session tracking for logout/revocation
-- ------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "SysSession" (
    "SysSession_ID"     BIGSERIAL       PRIMARY KEY,
    "SysUser_ID"        INT             NOT NULL REFERENCES "SysUser" ("SysUser_ID"),
    "RefreshTokenHash"  TEXT            NOT NULL,
    "AccessTokenJti"    VARCHAR(64)     NOT NULL,
    "IpAddress"         VARCHAR(45),
    "UserAgent"         VARCHAR(255),
    "CreatedAt"         TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    "ExpiresAt"         TIMESTAMPTZ     NOT NULL,
    "RevokedAt"         TIMESTAMPTZ,
    "IsRevoked"         BOOLEAN         NOT NULL DEFAULT FALSE
);

COMMENT ON TABLE "SysSession" IS 'Tracks active sessions for logout, token revocation, and session limits.';
CREATE INDEX IF NOT EXISTS ix_sys_session_user ON "SysSession" ("SysUser_ID");
CREATE INDEX IF NOT EXISTS ix_sys_session_token_hash ON "SysSession" ("RefreshTokenHash");
CREATE INDEX IF NOT EXISTS ix_sys_session_expires ON "SysSession" ("ExpiresAt") WHERE "IsRevoked" = FALSE;

-- ------------------------------------------------------------------
-- Permission enum values (for Permission SMALLINT columns)
-- 0 = None, 1 = ReadOnly, 2 = ReadWrite, 3 = Create, 4 = FullControl
-- ------------------------------------------------------------------

-- ------------------------------------------------------------------
-- Seed data (idempotent)
-- ------------------------------------------------------------------

-- Default client
INSERT INTO "SysClient" ("Code", "Name", "Description")
VALUES ('DEFAULT', 'Default Client', 'Default platform client')
ON CONFLICT ("Code") DO UPDATE SET
    "Name" = EXCLUDED."Name",
    "Description" = EXCLUDED."Description";

-- Default organisation
INSERT INTO "SysOrg" ("SysClient_ID", "Code", "Name", "Description")
SELECT (SELECT "SysClient_ID" FROM "SysClient" WHERE "Code" = 'DEFAULT'), 'DEFAULT', 'Default Org', 'Default organisation'
WHERE NOT EXISTS (SELECT 1 FROM "SysOrg" WHERE "Code" = 'DEFAULT')
LIMIT 1;

-- Default admin role
INSERT INTO "SysRole" ("SysClient_ID", "Name", "Description")
SELECT (SELECT "SysClient_ID" FROM "SysClient" WHERE "Code" = 'DEFAULT'), 'Admin', 'System administrator'
WHERE NOT EXISTS (SELECT 1 FROM "SysRole" WHERE "Name" = 'Admin')
LIMIT 1;

-- Default admin user (password: Admin@123 — hashed with BCrypt, update on first login)
INSERT INTO "SysUser" ("Username", "PasswordHash", "Name", "SysClient_ID", "SysOrg_ID", "IsActive")
SELECT 'admin', '$2b$12$LZqXwQvGNhL2R4kJPPKISuPej7MmHMqB0Qm8L5gJ3VXf7kE2JGq5i', 'System Administrator',
       (SELECT "SysClient_ID" FROM "SysClient" WHERE "Code" = 'DEFAULT'),
       (SELECT "SysOrg_ID" FROM "SysOrg" WHERE "Code" = 'DEFAULT'),
       TRUE
WHERE NOT EXISTS (SELECT 1 FROM "SysUser" WHERE "Username" = 'admin')
LIMIT 1;

-- Assign admin role to admin user
INSERT INTO "SysUserRoles" ("SysUser_ID", "SysRole_ID")
SELECT u."SysUser_ID", r."SysRole_ID"
FROM "SysUser" u, "SysRole" r
WHERE u."Username" = 'admin' AND r."Name" = 'Admin'
AND NOT EXISTS (
    SELECT 1 FROM "SysUserRoles" ur
    JOIN "SysUser" u2 ON ur."SysUser_ID" = u2."SysUser_ID"
    JOIN "SysRole" r2 ON ur."SysRole_ID" = r2."SysRole_ID"
    WHERE u2."Username" = 'admin' AND r2."Name" = 'Admin'
);

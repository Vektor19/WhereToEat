-- 0005_create_users.sql
-- Step 5: the minimal, PII-free user model (invariant #11).
--
-- We store only our internal user Guid, the opaque external-IdP identity (issuer + subject — the
-- OIDC iss/sub claims, NOT email/name/phone), and the user's roles. Identity/PII lives at the IdP;
-- nothing here is ever venue-exposed. (Issuer, Subject) is unique — one internal user per external
-- identity.

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'users')
    EXEC (N'CREATE SCHEMA users');
GO

CREATE TABLE users.[User]
(
    Id      UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_User PRIMARY KEY,
    -- External OIDC identity. Opaque strings, treated as identifiers, never parsed. No PII columns.
    Issuer  NVARCHAR(500)    NOT NULL,
    Subject NVARCHAR(500)    NOT NULL,
    CONSTRAINT UQ_User_Issuer_Subject UNIQUE (Issuer, Subject)
);
GO

-- Roles mapped from the IdP claims (0 = User baseline, 1 = Admin). A set per user; the baseline
-- User role is always present (enforced by the domain), Admin is what the admin host authorizes on.
CREATE TABLE users.UserRole
(
    UserId UNIQUEIDENTIFIER NOT NULL,
    Role   INT              NOT NULL,
    CONSTRAINT PK_UserRole PRIMARY KEY (UserId, Role),
    CONSTRAINT FK_UserRole_User FOREIGN KEY (UserId) REFERENCES users.[User] (Id) ON DELETE CASCADE
);
GO

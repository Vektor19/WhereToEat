-- 0008_create_monetization.sql
-- Step 5: the monetization domain shape (future-facing; behaviour in Step 14).
--
-- VerifiedStatus (per venue, with a SubscriptionTier), Promotion and AdPlacement. CRITICAL
-- (invariant #10): AdPlacement/Promotion are ALWAYS-labeled, separate slots and carry NO column that
-- could feed organic ranking — there is intentionally NO rank/boost/weight/priority/score column on
-- either table. They hold only identity, the venue, the targeting/offer, and the active window. The
-- always-labeled truth is implicit (every row IS a labeled ad); the recommendation pipeline never
-- joins these tables. Lives in its own `monetization` schema, keyed by the opaque venue Guid.

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'monetization')
    EXEC (N'CREATE SCHEMA monetization');
GO

CREATE TABLE monetization.VerifiedStatus
(
    VenueId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_VerifiedStatus PRIMARY KEY,
    -- SubscriptionTier enum: 0 = None (freemium baseline), 1 = Basic, 2 = Pro.
    Tier    INT              NOT NULL CONSTRAINT DF_VerifiedStatus_Tier DEFAULT (0)
);
GO

CREATE TABLE monetization.AdPlacement
(
    Id          UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AdPlacement PRIMARY KEY,
    VenueId     UNIQUEIDENTIFIER NOT NULL,
    -- Targeting selects WHERE the labeled slot shows (audience), NOT how organic results rank.
    TargetingKey NVARCHAR(400)   NOT NULL,
    StartsAt    DATETIMEOFFSET   NOT NULL,
    EndsAt      DATETIMEOFFSET   NOT NULL,
    CONSTRAINT CK_AdPlacement_Window CHECK (EndsAt > StartsAt)
);
GO

CREATE INDEX IX_AdPlacement_VenueId ON monetization.AdPlacement (VenueId);
GO

CREATE TABLE monetization.Promotion
(
    Id       UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Promotion PRIMARY KEY,
    VenueId  UNIQUEIDENTIFIER NOT NULL,
    Offer    NVARCHAR(1000)   NOT NULL,
    StartsAt DATETIMEOFFSET   NOT NULL,
    EndsAt   DATETIMEOFFSET   NOT NULL,
    CONSTRAINT CK_Promotion_Window CHECK (EndsAt > StartsAt)
);
GO

CREATE INDEX IX_Promotion_VenueId ON monetization.Promotion (VenueId);
GO

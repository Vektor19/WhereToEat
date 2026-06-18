-- 0006_create_photos.sql
-- Step 5: dish photos with the invariant-#8 gate.
--
-- A photo is either our own GENERIC category illustration (IsGeneric = 1, the DEFAULT on every dish,
-- always displayable) or a venue's REAL photo (IsGeneric = 0), which is displayable ONLY when the
-- venue granted permission (PermissionGranted = 1). The domain Photo aggregate enforces the gate; the
-- CHECK here pins it in the schema: a generic photo never needs permission, and a real photo's
-- displayability hinges on PermissionGranted. Lives in the `catalog` schema next to its Dish.

CREATE TABLE catalog.Photo
(
    Id                UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Photo PRIMARY KEY,
    DishId            UNIQUEIDENTIFIER NOT NULL,
    IsGeneric         BIT              NOT NULL,
    -- Only meaningful for a real photo; kept 0 for generics (our own content needs no permission).
    PermissionGranted BIT              NOT NULL CONSTRAINT DF_Photo_PermissionGranted DEFAULT (0),
    Url               NVARCHAR(1000)   NOT NULL,
    CONSTRAINT FK_Photo_Dish FOREIGN KEY (DishId) REFERENCES catalog.Dish (Id) ON DELETE CASCADE,
    -- A generic photo must not carry a granted permission (it is our own content, not a venue's).
    CONSTRAINT CK_Photo_GenericHasNoPermission CHECK (IsGeneric = 0 OR PermissionGranted = 0)
);
GO

CREATE INDEX IX_Photo_DishId ON catalog.Photo (DishId);
GO

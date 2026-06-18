namespace WhereToEat.Admin.Application.Photos;

/// <summary>
/// Toggles a venue's real (non-generic) photo behind the Step 5 permission gate (invariant #8): a
/// real photo becomes displayable only when <see cref="PermissionGranted"/> is true. Our own generic
/// category photos are the default on every dish and are never affected by this command.
/// </summary>
public sealed record ManageRealPhotoCommand(Guid PhotoId, bool PermissionGranted);

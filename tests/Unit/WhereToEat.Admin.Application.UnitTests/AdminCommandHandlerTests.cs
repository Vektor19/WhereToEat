using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WhereToEat.Admin.Application.Abstractions;
using WhereToEat.Admin.Application.Addresses;
using WhereToEat.Admin.Application.MenuItems;
using WhereToEat.Admin.Application.Photos;
using WhereToEat.Admin.Application.Protection;
using WhereToEat.SharedKernel.Results;
using Xunit;

namespace WhereToEat.Admin.Application.UnitTests;

/// <summary>
/// Unit tests for the admin CRUD command handlers with a mocked <see cref="IAdminCatalogStore"/> (and,
/// for the address edit, a mocked <see cref="IAddressChangedDispatcher"/>). They prove each handler
/// delegates to the store and surfaces its <see cref="Result"/>, and — the load-bearing one — that
/// <see cref="EditAddressCommandHandler"/> raises the re-geocode flow ONLY after a successful persist.
/// </summary>
public sealed class AdminCommandHandlerTests
{
    private readonly IAdminCatalogStore _store = Substitute.For<IAdminCatalogStore>();

    [Fact]
    public async Task EditMenuItem_DelegatesToStore_AndReturnsItsResult()
    {
        var id = Guid.NewGuid();
        var dishId = Guid.NewGuid();
        _store.EditMenuItemAsync(id, dishId, 180m, "UAH", "450 г", Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        var handler = new EditMenuItemCommandHandler(_store, NullLogger<EditMenuItemCommandHandler>.Instance);

        var result = await handler.HandleAsync(new EditMenuItemCommand(id, dishId, 180m, "UAH", "450 г"));

        result.IsSuccess.Should().BeTrue();
        await _store.Received(1).EditMenuItemAsync(id, dishId, 180m, "UAH", "450 г", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EditMenuItem_SurfacesStoreFailure()
    {
        var id = Guid.NewGuid();
        _store.EditMenuItemAsync(id, Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(Error.NotFound("Admin.MenuItem.NotFound", "nope")));
        var handler = new EditMenuItemCommandHandler(_store, NullLogger<EditMenuItemCommandHandler>.Instance);

        var result = await handler.HandleAsync(new EditMenuItemCommand(id, Guid.NewGuid(), 1m, "UAH", null));

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task SetMenuItemDoNotParse_DelegatesToStore()
    {
        var id = Guid.NewGuid();
        _store.SetMenuItemDoNotParseAsync(id, true, Arg.Any<CancellationToken>()).Returns(Result.Success());
        var handler = new SetMenuItemDoNotParseCommandHandler(_store, NullLogger<SetMenuItemDoNotParseCommandHandler>.Instance);

        var result = await handler.HandleAsync(new SetMenuItemDoNotParseCommand(id, true));

        result.IsSuccess.Should().BeTrue();
        await _store.Received(1).SetMenuItemDoNotParseAsync(id, true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetRestaurantDoNotUpdate_DelegatesToStore()
    {
        var id = Guid.NewGuid();
        _store.SetRestaurantDoNotUpdateAsync(id, true, Arg.Any<CancellationToken>()).Returns(Result.Success());
        var handler = new SetRestaurantDoNotUpdateCommandHandler(_store, NullLogger<SetRestaurantDoNotUpdateCommandHandler>.Instance);

        var result = await handler.HandleAsync(new SetRestaurantDoNotUpdateCommand(id, true));

        result.IsSuccess.Should().BeTrue();
        await _store.Received(1).SetRestaurantDoNotUpdateAsync(id, true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ManageRealPhoto_DelegatesToStore()
    {
        var id = Guid.NewGuid();
        _store.SetRealPhotoPermissionAsync(id, true, Arg.Any<CancellationToken>()).Returns(Result.Success());
        var handler = new ManageRealPhotoCommandHandler(_store, NullLogger<ManageRealPhotoCommandHandler>.Instance);

        var result = await handler.HandleAsync(new ManageRealPhotoCommand(id, true));

        result.IsSuccess.Should().BeTrue();
        await _store.Received(1).SetRealPhotoPermissionAsync(id, true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EditAddress_OnSuccess_RaisesReGeocodeDispatch()
    {
        var id = Guid.NewGuid();
        var dispatcher = Substitute.For<IAddressChangedDispatcher>();
        _store.EditAddressAsync(id, "вул. Хрещатик, 1", "Київ", Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        var handler = new EditAddressCommandHandler(_store, dispatcher, NullLogger<EditAddressCommandHandler>.Instance);

        var result = await handler.HandleAsync(new EditAddressCommand(id, "вул. Хрещатик, 1", "Київ"));

        result.IsSuccess.Should().BeTrue();
        // The AddressChanged → re-geocode flow (Step 8 seam) is raised exactly once, AFTER persist.
        await dispatcher.Received(1).DispatchAsync(id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EditAddress_OnStoreFailure_DoesNotRaiseReGeocode()
    {
        var id = Guid.NewGuid();
        var dispatcher = Substitute.For<IAddressChangedDispatcher>();
        _store.EditAddressAsync(id, Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(Error.NotFound("Admin.Restaurant.NotFound", "nope")));
        var handler = new EditAddressCommandHandler(_store, dispatcher, NullLogger<EditAddressCommandHandler>.Instance);

        var result = await handler.HandleAsync(new EditAddressCommand(id, "x", null));

        result.IsFailure.Should().BeTrue();
        // A rejected edit never re-geocodes (nothing changed).
        await dispatcher.DidNotReceive().DispatchAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WhereToEat.Monetization.Application.Ads;
using WhereToEat.Monetization.Application.Payments;
using WhereToEat.Monetization.Application.Verified;
using WhereToEat.Monetization.Domain;
using WhereToEat.Monetization.Domain.Abstractions;
using WhereToEat.Monetization.Domain.Identifiers;
using WhereToEat.Monetization.Infrastructure.Payments;
using WhereToEat.SharedKernel.Results;
using Xunit;

namespace WhereToEat.Monetization.Application.UnitTests;

/// <summary>
/// The <see cref="IPaymentGateway"/> is the SINGLE payment touch-point (Step 14). These tests prove the
/// money-handling use-cases run through it and nowhere else: a rejecting gateway blocks the grant /
/// ad-creation (nothing is persisted), and the only shipped implementation — the dev
/// <see cref="NoOpPaymentGateway"/> — always authorizes without contacting a provider. They also pin
/// that granting Verified flips the photo gate + tier with NO ranking/contact-link effect, and that a
/// created ad placement is always labeled.
/// </summary>
public sealed class PaymentSeamTests
{
    private static readonly Guid Venue = Guid.NewGuid();
    private static readonly DateTimeOffset Start = DateTimeOffset.UtcNow;
    private static readonly DateTimeOffset End = DateTimeOffset.UtcNow.AddDays(30);

    [Fact]
    public async Task NoOpPaymentGateway_AlwaysAuthorizes_WithoutAProvider()
    {
        var gateway = new NoOpPaymentGateway(NullLogger<NoOpPaymentGateway>.Instance);

        var result = await gateway.AuthorizeAsync(VenueRef.From(Venue), "Verified:Pro");

        result.IsSuccess.Should().BeTrue();
        result.Value.AuthorizationId.Should().StartWith("noop-");
    }

    [Fact]
    public async Task GrantVerified_RunsThroughThePaymentSeam_AndFlipsThePhotoGateAndTier()
    {
        var repository = Substitute.For<IVerifiedStatusRepository>();
        repository.GetByVenueAsync(Arg.Any<VenueRef>(), Arg.Any<CancellationToken>())
            .Returns((VerifiedStatus?)null);
        var gateway = new NoOpPaymentGateway(NullLogger<NoOpPaymentGateway>.Instance);
        var handler = new GrantVerifiedCommandHandler(repository, gateway, NullLogger<GrantVerifiedCommandHandler>.Instance);

        VerifiedStatus? persisted = null;
        await repository.UpsertAsync(
            Arg.Do<VerifiedStatus>(s => persisted = s), Arg.Any<CancellationToken>());

        var result = await handler.HandleAsync(new GrantVerifiedCommand(Venue, SubscriptionTier.Pro));

        result.IsSuccess.Should().BeTrue();
        persisted.Should().NotBeNull();
        persisted!.Tier.Should().Be(SubscriptionTier.Pro);
        persisted.CanManageRealPhotos.Should().BeTrue("granting Verified opens the Step 5 real-photo gate");
        await repository.Received(1).UpsertAsync(Arg.Any<VerifiedStatus>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GrantVerified_WhenPaymentRejected_DoesNotPersist()
    {
        var repository = Substitute.For<IVerifiedStatusRepository>();
        var rejectingGateway = Substitute.For<IPaymentGateway>();
        rejectingGateway.AuthorizeAsync(Arg.Any<VenueRef>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<PaymentAuthorization>(
                Error.Validation("Payment.Declined", "card declined")));
        var handler = new GrantVerifiedCommandHandler(repository, rejectingGateway, NullLogger<GrantVerifiedCommandHandler>.Instance);

        var result = await handler.HandleAsync(new GrantVerifiedCommand(Venue, SubscriptionTier.Pro));

        result.IsFailure.Should().BeTrue();
        await repository.DidNotReceive().UpsertAsync(Arg.Any<VerifiedStatus>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RevokeVerified_ClosesThePhotoGate_NoPaymentInvolved()
    {
        var repository = Substitute.For<IVerifiedStatusRepository>();
        repository.GetByVenueAsync(Arg.Any<VenueRef>(), Arg.Any<CancellationToken>())
            .Returns(VerifiedStatus.Create(VenueRef.From(Venue), SubscriptionTier.Pro).Value);
        var handler = new RevokeVerifiedCommandHandler(repository, NullLogger<RevokeVerifiedCommandHandler>.Instance);

        VerifiedStatus? persisted = null;
        await repository.UpsertAsync(
            Arg.Do<VerifiedStatus>(s => persisted = s), Arg.Any<CancellationToken>());

        var result = await handler.HandleAsync(new RevokeVerifiedCommand(Venue));

        result.IsSuccess.Should().BeTrue();
        persisted.Should().NotBeNull();
        persisted!.IsVerified.Should().BeFalse();
        persisted.CanManageRealPhotos.Should().BeFalse("revoking Verified closes the real-photo gate");
    }

    [Fact]
    public async Task CreateAdPlacement_RunsThroughThePaymentSeam_AndIsAlwaysLabeled()
    {
        var repository = Substitute.For<IAdPlacementRepository>();
        var gateway = new NoOpPaymentGateway(NullLogger<NoOpPaymentGateway>.Instance);
        var handler = new CreateAdPlacementCommandHandler(repository, gateway, NullLogger<CreateAdPlacementCommandHandler>.Instance);

        AdPlacement? persisted = null;
        await repository.AddAsync(
            Arg.Do<AdPlacement>(p => persisted = p), Arg.Any<CancellationToken>());

        var result = await handler.HandleAsync(new CreateAdPlacementCommand(Venue, "pizza:kyiv", Start, End));

        result.IsSuccess.Should().BeTrue();
        persisted.Should().NotBeNull();
        persisted!.IsLabeledAd.Should().BeTrue("every ad placement is a labeled, separate slot (invariant #10)");
    }

    [Fact]
    public async Task CreateAdPlacement_WhenPaymentRejected_DoesNotPersist()
    {
        var repository = Substitute.For<IAdPlacementRepository>();
        var rejectingGateway = Substitute.For<IPaymentGateway>();
        rejectingGateway.AuthorizeAsync(Arg.Any<VenueRef>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<PaymentAuthorization>(
                Error.Validation("Payment.Declined", "card declined")));
        var handler = new CreateAdPlacementCommandHandler(repository, rejectingGateway, NullLogger<CreateAdPlacementCommandHandler>.Instance);

        var result = await handler.HandleAsync(new CreateAdPlacementCommand(Venue, "pizza:kyiv", Start, End));

        result.IsFailure.Should().BeTrue();
        await repository.DidNotReceive().AddAsync(Arg.Any<AdPlacement>(), Arg.Any<CancellationToken>());
    }
}

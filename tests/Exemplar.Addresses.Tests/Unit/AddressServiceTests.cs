using Exemplar.Addresses.Application;
using Exemplar.Addresses.Domain;
using Exemplar.Addresses.Domain.Errors;
using Exemplar.Addresses.Infrastructure;
using Exemplar.Core.Errors;
using Exemplar.Core.Functional;
using Exemplar.Customers.Contracts;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Exemplar.Addresses.Tests.Unit;

public class AddressServiceTests
{
    private readonly ICustomerLookup _customerLookup = Substitute.For<ICustomerLookup>();
    private readonly IAddressRepository _repo = Substitute.For<IAddressRepository>();
    private readonly AddressService _sut;

    public AddressServiceTests() => _sut = new AddressService(_customerLookup, _repo);

    // ── GetByCustomerIdAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task GetByCustomerIdAsync_WhenAddressesExist_ReturnsDtos()
    {
        var customerId = Guid.NewGuid();
        var addresses = new List<Address> { MakeAddress(customerId), MakeAddress(customerId) };
        _repo.GetByCustomerIdAsync(customerId, default)
             .Returns(Result<BaseError, IReadOnlyList<Address>>.Success(addresses));

        var result = await _sut.GetByCustomerIdAsync(customerId, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.All(a => a.CustomerId == customerId).Should().BeTrue();
    }

    [Fact]
    public async Task GetByCustomerIdAsync_WhenNoAddresses_ReturnsEmptyList()
    {
        var customerId = Guid.NewGuid();
        _repo.GetByCustomerIdAsync(customerId, default)
             .Returns(Result<BaseError, IReadOnlyList<Address>>.Success(new List<Address>()));

        var result = await _sut.GetByCustomerIdAsync(customerId, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    // ── AddAddressAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task AddAddressAsync_WhenCustomerExistsAndNoPrimaryConflict_InsertsAndReturnsDto()
    {
        var customerId = Guid.NewGuid();
        var request = MakeRequest(isPrimary: false);
        SetupCustomerFound(customerId);
        _repo.InsertAsync(Arg.Any<Address>(), default)
             .Returns(ci => Result<BaseError, Address>.Success(ci.Arg<Address>()));

        var result = await _sut.AddAddressAsync(customerId, request, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.CustomerId.Should().Be(customerId);
        result.Value.Line1.Should().Be(request.Line1);
        result.Value.IsPrimary.Should().BeFalse();
        await _repo.DidNotReceive().HasPrimaryAddressAsync(Arg.Any<Guid>(), default);
    }

    [Fact]
    public async Task AddAddressAsync_WhenCustomerNotFound_ReturnsCustomerNotFoundError()
    {
        var customerId = Guid.NewGuid();
        _customerLookup.FindByIdAsync(customerId, default)
            .Returns(Result<NotFoundError, CustomerSummaryDto>.Failure(
                new NotFoundError($"Customer {customerId} not found")));

        var result = await _sut.AddAddressAsync(customerId, MakeRequest(), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<CustomerNotFoundError>();
        result.Error.StatusCode.Should().Be(404);
        result.Error.ErrorCode.Should().Be("ADDRESS_CUSTOMER_NOT_FOUND");
        await _repo.DidNotReceive().InsertAsync(Arg.Any<Address>(), default);
    }

    [Fact]
    public async Task AddAddressAsync_WhenAddingPrimary_AndPrimaryAlreadyExists_ReturnsDuplicatePrimaryError()
    {
        var customerId = Guid.NewGuid();
        SetupCustomerFound(customerId);
        _repo.HasPrimaryAddressAsync(customerId, default)
             .Returns(Result<BaseError, bool>.Success(true));

        var result = await _sut.AddAddressAsync(customerId, MakeRequest(isPrimary: true), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<DuplicatePrimaryAddressError>();
        result.Error.StatusCode.Should().Be(409);
        result.Error.ErrorCode.Should().Be("ADDRESS_DUPLICATE_PRIMARY");
        await _repo.DidNotReceive().InsertAsync(Arg.Any<Address>(), default);
    }

    [Fact]
    public async Task AddAddressAsync_WhenAddingPrimary_AndNoPrimaryExists_Inserts()
    {
        var customerId = Guid.NewGuid();
        SetupCustomerFound(customerId);
        _repo.HasPrimaryAddressAsync(customerId, default)
             .Returns(Result<BaseError, bool>.Success(false));
        _repo.InsertAsync(Arg.Any<Address>(), default)
             .Returns(ci => Result<BaseError, Address>.Success(ci.Arg<Address>()));

        var result = await _sut.AddAddressAsync(customerId, MakeRequest(isPrimary: true), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsPrimary.Should().BeTrue();
    }

    [Fact]
    public async Task AddAddressAsync_WhenAddingNonPrimary_SkipsPrimaryCheck()
    {
        var customerId = Guid.NewGuid();
        SetupCustomerFound(customerId);
        _repo.InsertAsync(Arg.Any<Address>(), default)
             .Returns(ci => Result<BaseError, Address>.Success(ci.Arg<Address>()));

        await _sut.AddAddressAsync(customerId, MakeRequest(isPrimary: false), default);

        await _repo.DidNotReceive().HasPrimaryAddressAsync(Arg.Any<Guid>(), default);
    }

    [Fact]
    public async Task AddAddressAsync_WhenRepoUnavailableDuringInsert_ReturnsError()
    {
        var customerId = Guid.NewGuid();
        SetupCustomerFound(customerId);
        _repo.InsertAsync(Arg.Any<Address>(), default)
             .Returns(new InternalError("db down"));

        var result = await _sut.AddAddressAsync(customerId, MakeRequest(isPrimary: false), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<InternalError>();
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private void SetupCustomerFound(Guid customerId) =>
        _customerLookup.FindByIdAsync(customerId, default)
            .Returns(Result<NotFoundError, CustomerSummaryDto>.Success(
                new CustomerSummaryDto(customerId, "Test Customer", "test@example.com")));

    private static Address MakeAddress(Guid customerId) => Address.Create(
        customerId, "123 Main St", null, "Testville", "12345", "GB", false);

    private static AddAddressRequest MakeRequest(bool isPrimary = false) =>
        new("10 High Street", null, "London", "SW1A 1AA", "GB", isPrimary);
}

using FluentAssertions;
using WhereToEat.SharedKernel.Primitives;
using Xunit;

namespace WhereToEat.SharedKernel.UnitTests.Primitives;

public sealed class EntityTests
{
    // Two concrete entity types over the same Guid id type. Identity is by id + concrete
    // type, never by attribute values, so Name is deliberately ignored by equality.
    private sealed class Customer : Entity<Guid>
    {
        public Customer(Guid id, string name)
            : base(id) => Name = name;

        public string Name { get; }
    }

    private sealed class Product : Entity<Guid>
    {
        public Product(Guid id)
            : base(id)
        {
        }
    }

    [Fact]
    public void Equals_SameIdAndType_IsTrue_EvenWhenOtherStateDiffers()
    {
        var id = Guid.NewGuid();
        var a = new Customer(id, "Alice");
        var b = new Customer(id, "DIFFERENT NAME");

        a.Equals(b).Should().BeTrue();
        a.Equals((object)b).Should().BeTrue();
        (a == b).Should().BeTrue();
        (a != b).Should().BeFalse();
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void Equals_DifferentId_IsFalse()
    {
        var a = new Customer(Guid.NewGuid(), "Alice");
        var b = new Customer(Guid.NewGuid(), "Alice");

        a.Equals(b).Should().BeFalse();
        (a == b).Should().BeFalse();
        (a != b).Should().BeTrue();
    }

    [Fact]
    public void Equals_DifferentConcreteTypeSameIdValue_IsFalse()
    {
        var id = Guid.NewGuid();
        var customer = new Customer(id, "Alice");
        var product = new Product(id);

        // Same Guid value, but a Customer is not a Product.
        customer.Equals(product as Entity<Guid>).Should().BeFalse();
        customer.Equals((object)product).Should().BeFalse();
    }

    [Fact]
    public void Equals_Null_IsFalse()
    {
        var a = new Customer(Guid.NewGuid(), "Alice");
        Customer? nullCustomer = null;

        a.Equals(null).Should().BeFalse();
        (a == nullCustomer).Should().BeFalse();
        (a != nullCustomer).Should().BeTrue();
    }

    [Fact]
    public void GetHashCode_IncludesConcreteType_SoSameIdDifferentTypesDiffer()
    {
        var id = Guid.NewGuid();
        var customer = new Customer(id, "Alice");
        var product = new Product(id);

        customer.GetHashCode().Should().NotBe(product.GetHashCode());
    }
}

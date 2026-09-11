using Equine.Domain.Common;
using Shouldly;
using Xunit;

namespace Equine.UnitTests.Domain;

public class EntityTests
{
    [Fact]
    public void CreateEntity_GeneratesUuidV7Id()
    {
        // Act
        var entity = new TestEntity();

        // Assert
        entity.Id.ShouldNotBe(Guid.Empty);
        // UUID v7 format: xxxxxxxx-xxxx-7xxx-...  — version nibble at position 14
        var uuidStr = entity.Id.ToString();
        uuidStr[14].ShouldBe('7');
    }

    [Fact]
    public void CreateEntity_WithExplicitId_UsesGivenId()
    {
        // Arrange
        var givenId = Guid.CreateVersion7();

        // Act
        var entity = new TestEntity(givenId);

        // Assert
        entity.Id.ShouldBe(givenId);
    }
}

public class SoftDeleteTests
{
    [Fact]
    public void SoftDelete_SetsDeletedAt()
    {
        // Act
        var entity = new TestSoftEntity();

        entity.SoftDelete();

        // Assert
        entity.DeletedAt.ShouldNotBeNull();
        entity.IsDeleted.ShouldBeTrue();
    }

    [Fact]
    public void Restore_ClearsDeletedAt()
    {
        // Arrange
        var entity = new TestSoftEntity();
        entity.SoftDelete();

        // Act
        entity.Restore();

        // Assert
        entity.DeletedAt.ShouldBeNull();
        entity.IsDeleted.ShouldBeFalse();
    }
}

// Test fixtures
internal class TestEntity : Entity
{
    public TestEntity() : base() { }
    public TestEntity(Guid id) : base(id) { }
}

internal class TestSoftEntity : SoftDeletableEntity { }

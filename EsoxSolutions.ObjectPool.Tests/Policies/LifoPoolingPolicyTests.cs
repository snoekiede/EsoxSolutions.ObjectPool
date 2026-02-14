using EsoxSolutions.ObjectPool.Policies;

namespace EsoxSolutions.ObjectPool.Tests.Policies
{
    public class LifoPoolingPolicyTests
    {
        [Fact]
        public void Add_ShouldStoreItem()
        {
            // Arrange
            var policy = new LifoPoolingPolicy<string>();
            
            // Act
            policy.Add("test");
            
            // Assert
            Assert.Equal(1, policy.Count);
        }

        [Fact]
        public void TryTake_LastInFirstOut_ShouldRetrieveInReverseOrder()
        {
            // Arrange
            var policy = new LifoPoolingPolicy<string>();
            policy.Add("first");
            policy.Add("second");
            policy.Add("third");
            
            // Act
            var success1 = policy.TryTake(out var result1);
            var success2 = policy.TryTake(out var result2);
            var success3 = policy.TryTake(out var result3);
            
            // Assert
            Assert.True(success1);
            Assert.True(success2);
            Assert.True(success3);
            Assert.Equal("third", result1);
            Assert.Equal("second", result2);
            Assert.Equal("first", result3);
        }

        [Fact]
        public void TryTake_EmptyPolicy_ShouldReturnFalse()
        {
            // Arrange
            var policy = new LifoPoolingPolicy<string>();
            
            // Act
            var result = policy.TryTake(out var item);
            
            // Assert
            Assert.False(result);
            Assert.Null(item);
        }

        [Fact]
        public void Clear_ShouldRemoveAllItems()
        {
            // Arrange
            var policy = new LifoPoolingPolicy<string>();
            policy.Add("first");
            policy.Add("second");
            
            // Act
            policy.Clear();
            
            // Assert
            Assert.Equal(0, policy.Count);
        }

        [Fact]
        public void GetAll_ShouldReturnAllItems()
        {
            // Arrange
            var policy = new LifoPoolingPolicy<string>();
            policy.Add("first");
            policy.Add("second");
            policy.Add("third");
            
            // Act
            var items = policy.GetAll().ToList();
            
            // Assert
            Assert.Equal(3, items.Count);
            Assert.Contains("first", items);
            Assert.Contains("second", items);
            Assert.Contains("third", items);
        }

        [Fact]
        public void PolicyName_ShouldReturnLifo()
        {
            // Arrange
            var policy = new LifoPoolingPolicy<string>();
            
            // Act & Assert
            Assert.Equal("LIFO", policy.PolicyName);
        }

        [Fact]
        public void Add_NullItem_ShouldThrowException()
        {
            // Arrange
            var policy = new LifoPoolingPolicy<string>();
            
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => policy.Add(null!));
        }
    }
}

using EsoxSolutions.ObjectPool.Policies;

namespace EsoxSolutions.ObjectPool.Tests.Policies
{
    public class PriorityPoolingPolicyTests
    {
        private class PriorityItem
        {
            public string Name { get; set; } = string.Empty;
            public int Priority { get; set; }
        }

        [Fact]
        public void TryTake_ShouldRetrieveHighestPriorityFirst()
        {
            // Arrange
            var policy = new PriorityPoolingPolicy<PriorityItem>(item => item.Priority);
            
            policy.Add(new PriorityItem { Name = "Low", Priority = 1 });
            policy.Add(new PriorityItem { Name = "High", Priority = 10 });
            policy.Add(new PriorityItem { Name = "Medium", Priority = 5 });
            
            // Act
            var success1 = policy.TryTake(out var result1);
            var success2 = policy.TryTake(out var result2);
            var success3 = policy.TryTake(out var result3);
            
            // Assert
            Assert.True(success1);
            Assert.True(success2);
            Assert.True(success3);
            Assert.Equal("High", result1!.Name);
            Assert.Equal("Medium", result2!.Name);
            Assert.Equal("Low", result3!.Name);
        }

        [Fact]
        public void Constructor_WithNullSelector_ShouldThrowException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new PriorityPoolingPolicy<string>(null!));
        }

        [Fact]
        public void Count_ShouldBeThreadSafe()
        {
            // Arrange
            var policy = new PriorityPoolingPolicy<int>(x => x);
            
            // Act
            policy.Add(1);
            policy.Add(2);
            var countBefore = policy.Count;
            policy.TryTake(out _);
            var countAfter = policy.Count;
            
            // Assert
            Assert.Equal(2, countBefore);
            Assert.Equal(1, countAfter);
        }

        [Fact]
        public void PolicyName_ShouldReturnPriority()
        {
            // Arrange
            var policy = new PriorityPoolingPolicy<int>(x => x);
            
            // Act & Assert
            Assert.Equal("Priority", policy.PolicyName);
        }

        [Fact]
        public void Clear_ShouldRemoveAllItems()
        {
            // Arrange
            var policy = new PriorityPoolingPolicy<int>(x => x);
            policy.Add(1);
            policy.Add(2);
            policy.Add(3);
            
            // Act
            policy.Clear();
            
            // Assert
            Assert.Equal(0, policy.Count);
            Assert.False(policy.TryTake(out _));
        }
    }
}

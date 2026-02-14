using EsoxSolutions.ObjectPool.Policies;

namespace EsoxSolutions.ObjectPool.Tests.Policies
{
    public class FifoPoolingPolicyTests
    {
        [Fact]
        public void TryTake_FirstInFirstOut_ShouldRetrieveInOrder()
        {
            // Arrange
            var policy = new FifoPoolingPolicy<string>();
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
            Assert.Equal("first", result1);
            Assert.Equal("second", result2);
            Assert.Equal("third", result3);
        }

        [Fact]
        public void TryTake_EmptyPolicy_ShouldReturnFalse()
        {
            // Arrange
            var policy = new FifoPoolingPolicy<string>();
            
            // Act
            var result = policy.TryTake(out var item);
            
            // Assert
            Assert.False(result);
            Assert.Null(item);
        }

        [Fact]
        public void Count_ShouldReflectCurrentSize()
        {
            // Arrange
            var policy = new FifoPoolingPolicy<string>();
            
            // Act & Assert
            Assert.Equal(0, policy.Count);
            policy.Add("test1");
            Assert.Equal(1, policy.Count);
            policy.Add("test2");
            Assert.Equal(2, policy.Count);
            policy.TryTake(out _);
            Assert.Equal(1, policy.Count);
        }

        [Fact]
        public void PolicyName_ShouldReturnFifo()
        {
            // Arrange
            var policy = new FifoPoolingPolicy<string>();
            
            // Act & Assert
            Assert.Equal("FIFO", policy.PolicyName);
        }

        [Fact]
        public void Add_NullItem_ShouldThrowException()
        {
            // Arrange
            var policy = new FifoPoolingPolicy<string>();
            
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => policy.Add(null!));
        }
    }
}

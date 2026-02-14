using EsoxSolutions.ObjectPool.Policies;

namespace EsoxSolutions.ObjectPool.Tests.Policies
{
    public class PoolingPolicyFactoryTests
    {
        [Fact]
        public void Create_Lifo_ShouldReturnLifoPolicy()
        {
            // Act
            var policy = PoolingPolicyFactory.Create<string>(PoolingPolicyType.Lifo);
            
            // Assert
            Assert.IsType<LifoPoolingPolicy<string>>(policy);
            Assert.Equal("LIFO", policy.PolicyName);
        }

        [Fact]
        public void Create_Fifo_ShouldReturnFifoPolicy()
        {
            // Act
            var policy = PoolingPolicyFactory.Create<string>(PoolingPolicyType.Fifo);
            
            // Assert
            Assert.IsType<FifoPoolingPolicy<string>>(policy);
            Assert.Equal("FIFO", policy.PolicyName);
        }

        [Fact]
        public void Create_LeastRecentlyUsed_ShouldReturnLruPolicy()
        {
            // Act
            var policy = PoolingPolicyFactory.Create<string>(PoolingPolicyType.LeastRecentlyUsed);
            
            // Assert
            Assert.IsType<LeastRecentlyUsedPolicy<string>>(policy);
            Assert.Equal("LRU", policy.PolicyName);
        }

        [Fact]
        public void Create_RoundRobin_ShouldReturnRoundRobinPolicy()
        {
            // Act
            var policy = PoolingPolicyFactory.Create<string>(PoolingPolicyType.RoundRobin);
            
            // Assert
            Assert.IsType<RoundRobinPoolingPolicy<string>>(policy);
            Assert.Equal("RoundRobin", policy.PolicyName);
        }

        [Fact]
        public void Create_Priority_WithSelector_ShouldReturnPriorityPolicy()
        {
            // Act
            var policy = PoolingPolicyFactory.Create<int>(
                PoolingPolicyType.Priority, 
                prioritySelector: x => x);
            
            // Assert
            Assert.IsType<PriorityPoolingPolicy<int>>(policy);
            Assert.Equal("Priority", policy.PolicyName);
        }

        [Fact]
        public void Create_Priority_WithoutSelector_ShouldThrowException()
        {
            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => 
                PoolingPolicyFactory.Create<int>(PoolingPolicyType.Priority));
            
            Assert.Contains("Priority selector is required", ex.Message);
        }

        [Fact]
        public void CreateLifo_ShouldReturnLifoPolicy()
        {
            // Act
            var policy = PoolingPolicyFactory.CreateLifo<string>();
            
            // Assert
            Assert.IsType<LifoPoolingPolicy<string>>(policy);
        }

        [Fact]
        public void CreateFifo_ShouldReturnFifoPolicy()
        {
            // Act
            var policy = PoolingPolicyFactory.CreateFifo<string>();
            
            // Assert
            Assert.IsType<FifoPoolingPolicy<string>>(policy);
        }

        [Fact]
        public void CreatePriority_ShouldReturnPriorityPolicy()
        {
            // Act
            var policy = PoolingPolicyFactory.CreatePriority<int>(x => x);
            
            // Assert
            Assert.IsType<PriorityPoolingPolicy<int>>(policy);
        }

        [Fact]
        public void CreateLeastRecentlyUsed_ShouldReturnLruPolicy()
        {
            // Act
            var policy = PoolingPolicyFactory.CreateLeastRecentlyUsed<string>();
            
            // Assert
            Assert.IsType<LeastRecentlyUsedPolicy<string>>(policy);
        }

        [Fact]
        public void CreateRoundRobin_ShouldReturnRoundRobinPolicy()
        {
            // Act
            var policy = PoolingPolicyFactory.CreateRoundRobin<string>();
            
            // Assert
            Assert.IsType<RoundRobinPoolingPolicy<string>>(policy);
        }
    }
}

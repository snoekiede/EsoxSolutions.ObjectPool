namespace EsoxSolutions.ObjectPool.Interfaces
{
    /// <summary>
    /// Interface for pool health monitoring
    /// </summary>
    public interface IPoolHealth
    {
        /// <summary>
        /// Checks if the pool is healthy
        /// </summary>
        bool IsHealthy { get; }

        /// <summary>
        /// Gets health status with details
        /// </summary>
        PoolHealthStatus GetHealthStatus();

        /// <summary>
        /// Gets the utilization percentage of the pool
        /// </summary>
        double UtilizationPercentage { get; }
    }

    /// <summary>
    /// Pool health status information
    /// </summary>
    public class PoolHealthStatus
    {
        /// <summary>
        /// Whether the pool is considered healthy
        /// </summary>
        public bool IsHealthy { get; set; }

        /// <summary>
        /// Human-readable health message
        /// </summary>
        public string? HealthMessage { get; set; }

        /// <summary>
        /// Pool utilization as a percentage (0.0 to 100.0)
        /// </summary>
        public double UtilizationPercentage { get; set; }

        /// <summary>
        /// When this health check was performed
        /// </summary>
        public DateTime LastChecked { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Number of warning conditions detected
        /// </summary>
        public int WarningCount { get; set; }

        /// <summary>
        /// List of warning messages
        /// </summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>
        /// Additional diagnostic information
        /// </summary>
        public Dictionary<string, object> Diagnostics { get; set; } = new();
    }
}

using EsoxSolutions.ObjectPool.Metrics;

namespace EsoxSolutions.ObjectPool.Interfaces
{
    /// <summary>
    /// Interface for exporting pool metrics to monitoring systems
    /// </summary>
    public interface IPoolMetrics
    {

        /// <summary>
        /// Export metrics with tags/labels for dimensional monitoring
        /// </summary>
        Dictionary<string, object> ExportMetrics(Dictionary<string, string>? tags = null);

        /// <summary>
        /// Reset metrics counters (useful for testing or periodic resets)
        /// </summary>
        void ResetMetrics();

    }
}

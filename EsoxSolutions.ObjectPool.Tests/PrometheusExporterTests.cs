using EsoxSolutions.ObjectPool.Pools;
using EsoxSolutions.ObjectPool.Interfaces;
using EsoxSolutions.ObjectPool.Metrics;

namespace EsoxSolutions.ObjectPool.Tests
{
    public class PrometheusExporterTests
    {
        [Fact]
        public void ExportMetricsPrometheus_IncludesHelpAndTypeAndValues()
        {
            // Arrange
            var initial = new List<int> { 1, 2, 3 };
            var pool = new ObjectPool<int>(initial);

            // Act
            var txt = ((IPoolMetrics)pool).ExportMetricsPrometheus();

            // Assert HELP/TYPE blocks for known metrics
            Assert.Contains("# HELP pool_objects_retrieved_total Total number of objects retrieved from the pool", txt);
            Assert.Contains("# TYPE pool_objects_retrieved_total counter", txt);

            // Assert a numeric metric line exists for available current
            Assert.Matches(@"pool_objects_available_current\{?[^}]*\}?\s+\d+\.?\d*", txt);
        }

        [Fact]
        public void ExportMetricsPrometheus_IncludesTagsAsLabels()
        {
            // Arrange
            var initial = new List<int> { 1, 2 };
            var pool = new ObjectPool<int>(initial);
            var tags = new Dictionary<string, string> { ["service"] = "testsvc", ["region"] = "euw" };

            // Act
            var txt = ((IPoolMetrics)pool).ExportMetricsPrometheus(tags);

            // Assert labels present
            Assert.Contains("service=\"testsvc\"", txt);
            Assert.Contains("region=\"euw\"", txt);

            // Ensure label block precedes numeric value for a metric
            Assert.Matches(@"pool_objects_retrieved_total\{[^}]*service=""testsvc""[^}]*\}\s+\d+", txt);
        }

        [Fact]
        public void ExportMetricsPrometheus_ExportsStringMetricAsInfo()
        {
            // Arrange
            var initialCars = new List<Models.Car> { new("a", "b") };
            var pool = new QueryableObjectPool<Models.Car>(initialCars);

            // Act
            var txt = ((IPoolMetrics)pool).ExportMetricsPrometheus();

            // pool_type should be exported as an _info metric and include value label
            Assert.Contains("# TYPE pool_type_info gauge", txt);
            Assert.Matches(@"pool_type_info\{[^}]*value=""queryable""[^}]*\}\s+1", txt);
        }
    }
}

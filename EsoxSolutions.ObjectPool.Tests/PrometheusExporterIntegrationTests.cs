using EsoxSolutions.ObjectPool.Pools;
using EsoxSolutions.ObjectPool.Interfaces;
using EsoxSolutions.ObjectPool.Metrics;
using Xunit.Abstractions;

namespace EsoxSolutions.ObjectPool.Tests
{
    public class PrometheusExporterIntegrationTests(ITestOutputHelper testOutputHelper)
    {
        private bool TryParseMetric(string prometheusText, string metricName, out double value, out Dictionary<string, string> labels)
        {
            value = double.NaN;
            labels = new Dictionary<string, string>();

            if (string.IsNullOrEmpty(prometheusText)) return false;

            var lines = prometheusText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            string? found = null;
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("#")) continue; // skip comments

                if (trimmed.StartsWith(metricName + " ") || trimmed.StartsWith(metricName + "{"))
                {
                    found = trimmed;
                    break;
                }
            }

            if (found == null) return false;

            // found line looks like: metricName{label1="v",label2="v"} 123.45
            var idxSpace = found.LastIndexOf(' ');
            if (idxSpace <= 0) return false;
            var valuePart = found.Substring(idxSpace + 1).Trim();
            if (!double.TryParse(valuePart, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out value))
                return false;

            var beforeValue = found.Substring(0, idxSpace).Trim();
            var idxBraceStart = beforeValue.IndexOf('{');
            if (idxBraceStart >= 0)
            {
                var idxBraceEnd = beforeValue.IndexOf('}', idxBraceStart);
                if (idxBraceEnd > idxBraceStart)
                {
                    var labelsPart = beforeValue.Substring(idxBraceStart + 1, idxBraceEnd - idxBraceStart - 1);
                    // split on commas not inside quotes - simple approach: split on ","
                    var parts = labelsPart.Split([','], StringSplitOptions.RemoveEmptyEntries);
                    foreach (var part in parts)
                    {
                        var kv = part.Split(['='], 2);
                        if (kv.Length != 2) continue;
                        var k = kv[0].Trim();
                        var v = kv[1].Trim();
                        if (v.StartsWith("\"") && v.EndsWith("\""))
                        {
                            v = v.Substring(1, v.Length - 2);
                        }
                        // unescape common sequences
                        v = v.Replace("\\\"", "\"").Replace("\\\\", "\\");
                        labels[k] = v;
                    }
                }
            }

            return true;
        }

        [Fact]
        public void Integration_ExportMetricsPrometheus_ValuesMatchPoolState()
        {
            try
            {
                // Arrange
                var initial = new List<int> { 1, 2, 3 };
                var pool = new ObjectPool<int>(initial);

                // Act - perform operations
                var m1 = pool.GetObject();
                var m2 = pool.GetObject();
                m1.Dispose(); // return one

                // Export prometheus text
                var text = ((IPoolMetrics)pool).ExportMetricsPrometheus();

                // Assert metrics parsed correctly
                Assert.True(TryParseMetric(text, "pool_objects_retrieved_total", out var retrieved, out var _));
                Assert.True(TryParseMetric(text, "pool_objects_returned_total", out var returned, out var _));
                Assert.True(TryParseMetric(text, "pool_objects_active_current", out var active, out var _));
                Assert.True(TryParseMetric(text, "pool_objects_available_current", out var available, out var _));

                Assert.Equal(2d, retrieved);
                Assert.Equal(1d, returned);
                Assert.Equal(1d, active);
                Assert.Equal(2d, available);

                // cleanup
                m2.Dispose();
            }
            catch (Exception ex)
            {
                testOutputHelper.WriteLine("Exception in Integration_ExportMetricsPrometheus_ValuesMatchPoolState: " + ex);
                throw;
            }
        }

        [Fact]
        public void Integration_ExportMetricsPrometheus_IncludesProvidedTagsAsLabels()
        {
            try
            {
                // Arrange
                var initial = new List<int> { 1, 2 };
                var pool = new ObjectPool<int>(initial);
                var tags = new Dictionary<string, string> { ["service"] = "integ-svc", ["zone"] = "eu-1" };

                // Act
                var text = pool.ExportMetricsPrometheus(tags);

                // Assert the retrieved_total metric has labels
                Assert.True(TryParseMetric(text, "pool_objects_retrieved_total", out _, out var labels));
                Assert.Contains("service", labels.Keys);
                Assert.Contains("zone", labels.Keys);
                Assert.Equal("integ-svc", labels["service"]);
                Assert.Equal("eu-1", labels["zone"]);
            }
            catch (Exception ex)
            {
                testOutputHelper.WriteLine("Exception in Integration_ExportMetricsPrometheus_IncludesProvidedTagsAsLabels: " + ex);
                throw;
            }
        }

        [Fact]
        public void Integration_ExportMetricsPrometheus_StringMetricExportedAsInfoWithValueLabel()
        {
            try
            {
                // Arrange
                var cars = new List<Models.Car> { new("Ford", "F") };
                var pool = new QueryableObjectPool<Models.Car>(cars);

                // Act
                var text = pool.ExportMetricsPrometheus();

                // pool_type_info should exist and contain value="queryable"
                Assert.True(TryParseMetric(text, "pool_type_info", out var infoVal, out var labels));
                // info metric should be 1
                Assert.Equal(1d, infoVal);
                Assert.Contains("value", labels.Keys);
                Assert.Equal("queryable", labels["value"]);
            }
            catch (Exception ex)
            {
                testOutputHelper.WriteLine("Exception in Integration_ExportMetricsPrometheus_StringMetricExportedAsInfoWithValueLabel: " + ex);
                throw;
            }
        }
    }
}

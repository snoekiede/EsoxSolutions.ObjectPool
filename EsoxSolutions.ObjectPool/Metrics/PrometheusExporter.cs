using System.Text;
using EsoxSolutions.ObjectPool.Constants;
using EsoxSolutions.ObjectPool.Interfaces;

namespace EsoxSolutions.ObjectPool.Metrics
{
    /// <summary>
    /// Provides helpers to convert pool metrics to Prometheus exposition format.
    /// </summary>
    public static class PrometheusExporter
    {
        /// <summary>
        /// Export metrics from an IPoolMetrics implementation into Prometheus exposition text.
        /// Tags passed are forwarded to the pool ExportMetrics call and also used as labels.
        /// </summary>
        /// <param name="pool">The pool implementing IPoolMetrics</param>
        /// <param name="tags">Optional tags to include as labels</param>
        /// <returns>Prometheus exposition formatted string</returns>
        public static string ExportMetricsPrometheus(this IPoolMetrics pool, Dictionary<string, string>? tags = null)
        {
            var metrics = pool.ExportMetrics(tags);
            return ConvertToPrometheus(metrics);
        }

        private static string ConvertToPrometheus(Dictionary<string, object> metrics)
        {
            var sb = new StringBuilder();

            // Extract global tags from metrics (tag_ prefix)
            var globalLabels = new Dictionary<string, string>();
            foreach (var kv in metrics.Where(k => k.Key.StartsWith(PoolConstants.Metrics.TagPrefix)))
            {
                var labelName = kv.Key.Substring(PoolConstants.Metrics.TagPrefix.Length);
                globalLabels[labelName] = kv.Value.ToString() ?? string.Empty;
            }

            // Iterate over metrics and render them
            foreach (var kv in metrics)
            {
                var key = kv.Key;

                // skip tag entries (already consumed)
                if (key.StartsWith(PoolConstants.Metrics.TagPrefix))
                    continue;

                var sanitized = SanitizeMetricName(key);
                var metricType = GetMetricType(key);
                var description = GetMetricDescription(key);

                // HELP and TYPE
                sb.AppendLine($"# HELP {sanitized} {description}");
                sb.AppendLine($"# TYPE {sanitized} {metricType}");

                // Build labels from globalLabels
                var labels = new List<string>();
                foreach (var gl in globalLabels)
                {
                    labels.Add($"{gl.Key}=\"{EscapeLabelValue(gl.Value)}\"");
                }

                // Handle value types
                if (kv.Value is sbyte || kv.Value is byte || kv.Value is short || kv.Value is ushort || kv.Value is int || kv.Value is uint || kv.Value is long || kv.Value is ulong || kv.Value is float || kv.Value is double || kv.Value is decimal)
                {
                    var numeric = Convert.ToDouble(kv.Value);
                    var labelStr = labels.Count > 0 ? "{" + string.Join(",", labels) + "}" : string.Empty;
                    sb.AppendLine($"{sanitized}{labelStr} {numeric.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
                }
                else if (kv.Value is DateTime dt)
                {
                    // Export as unix seconds
                    var unix = new DateTimeOffset(dt).ToUnixTimeSeconds();
                    var labelStr = labels.Count > 0 ? "{" + string.Join(",", labels) + "}" : string.Empty;
                    sb.AppendLine($"{sanitized}{labelStr} {unix}");
                }
                else if (kv.Value is TimeSpan ts)
                {
                    var seconds = ts.TotalSeconds;
                    var labelStr = labels.Count > 0 ? "{" + string.Join(",", labels) + "}" : string.Empty;
                    sb.AppendLine($"{sanitized}{labelStr} {seconds.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
                }
                else if (kv.Value is string strVal)
                {
                    // For string metrics, emit a *_info metric with a "value" label.
                    var infoName = SanitizeMetricName(key + "_info");
                    sb.AppendLine($"# HELP {infoName} Information label for {sanitized}");
                    sb.AppendLine($"# TYPE {infoName} gauge");

                    var labelPairs = new List<string>(labels) { $"value=\"{EscapeLabelValue(strVal)}\"" };
                    var labelStr = labelPairs.Count > 0 ? "{" + string.Join(",", labelPairs) + "}" : string.Empty;
                    sb.AppendLine($"{infoName}{labelStr} 1");
                }
                else
                {
                    // Fallback: attempt to convert to double, otherwise emit as info label
                    if (double.TryParse(kv.Value.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
                    {
                        var labelStr = labels.Count > 0 ? "{" + string.Join(",", labels) + "}" : string.Empty;
                        sb.AppendLine($"{sanitized}{labelStr} {parsed.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
                    }
                    else
                    {
                        var infoName = SanitizeMetricName(key + "_info");
                        sb.AppendLine($"# HELP {infoName} Information label for {sanitized}");
                        sb.AppendLine($"# TYPE {infoName} gauge");

                        var labelPairs = new List<string>(labels) { $"value=\"{EscapeLabelValue(kv.Value.ToString() ?? string.Empty)}\"" };
                        var labelStr = labelPairs.Count > 0 ? "{" + string.Join(",", labelPairs) + "}" : string.Empty;
                        sb.AppendLine($"{infoName}{labelStr} 1");
                    }
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }

        private static string EscapeLabelValue(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
        }

        private static string SanitizeMetricName(string name)
        {
            // Replace invalid characters with underscores and ensure doesn't start with digit
            var sb = new StringBuilder();
            foreach (var ch in name)
            {
                if (char.IsLetterOrDigit(ch) || ch == '_') sb.Append(ch);
                else sb.Append('_');
            }

            var result = sb.ToString();
            if (char.IsDigit(result[0])) result = "m_" + result; // prefix if starts with digit
            return result;
        }

        private static string GetMetricType(string metricKey)
        {
            return metricKey switch
            {
                PoolConstants.Metrics.RetrievedTotal => PoolConstants.MetricTypes.Counter,
                PoolConstants.Metrics.ReturnedTotal => PoolConstants.MetricTypes.Counter,
                PoolConstants.Metrics.EmptyEventsTotal => PoolConstants.MetricTypes.Counter,
                PoolConstants.Metrics.UptimeSeconds => PoolConstants.MetricTypes.Counter,
                _ => PoolConstants.MetricTypes.Gauge
            };
        }

        private static string GetMetricDescription(string metricKey)
        {
            return metricKey switch
            {
                PoolConstants.Metrics.RetrievedTotal => "Total number of objects retrieved from the pool",
                PoolConstants.Metrics.ReturnedTotal => "Total number of objects returned to the pool",
                PoolConstants.Metrics.ActiveCurrent => "Current number of active objects",
                PoolConstants.Metrics.AvailableCurrent => "Current number of available objects in the pool",
                PoolConstants.Metrics.ActivePeak => "Peak number of active objects",
                PoolConstants.Metrics.EmptyEventsTotal => "Total number of times the pool was empty when requested",
                PoolConstants.Metrics.UtilizationPercentage => "Pool utilization as a percentage",
                PoolConstants.Metrics.HealthStatus => "Pool health status (1=healthy, 0=unhealthy)",
                PoolConstants.Metrics.UptimeSeconds => "Pool uptime in seconds",
                PoolConstants.Metrics.MaxSize => "Maximum pool size",
                PoolConstants.Metrics.MaxActive => "Maximum active objects",
                PoolConstants.Metrics.StartTime => "Pool metrics start time (unix seconds)",
                "pool_type" => "Type of pool (queryable)",
                _ => "Pool metric"
            };
        }
    }
}

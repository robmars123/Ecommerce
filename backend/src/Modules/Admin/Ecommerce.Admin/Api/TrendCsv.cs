using System.Globalization;
using System.Text;
using Ecommerce.Admin.Application;

namespace Ecommerce.Admin.Api;

/// <summary>The trend as CSV (P2-tier4 D5): numbers and timestamps only, invariant culture, CRLF line ends.</summary>
internal static class TrendCsv
{
    public static string Write(RevenueTrendView view)
    {
        StringBuilder csv = new("bucket_start_utc,revenue,orders,currency\r\n");
        foreach (TrendBucket bucket in view.Buckets)
        {
            csv.Append(bucket.Start.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)).Append(',')
                .Append(bucket.Revenue.ToString("0.00##", CultureInfo.InvariantCulture)).Append(',')
                .Append(bucket.Orders.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(view.Currency).Append("\r\n");
        }
        return csv.ToString();
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AwsJob
{
    public static class DateFormat
    {
        private const string DATE_FORMAT1 = "yyyy-MM-dd'T'HH:mm:ss'Z'";
        private const string DATE_FORMAT2 = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

        public static DateTime? parseDateTime(object value)
        {
            DateTime val;

            if (value == null || value.ToString().Length == 0)
            {
                return null;
            }
            else if (DateTime.TryParseExact(value == null ? null : value.ToString(), DATE_FORMAT2, null, DateTimeStyles.None, out val)
                || DateTime.TryParseExact(value == null ? null : value.ToString(), DATE_FORMAT1, null, DateTimeStyles.None, out val))
            {
                return val;
            }
            else
            {
                throw new InvalidDataException("Invalid date format - '" + value + "'");
            }
        }
        public static string formatDateTime(DateTime? value, bool includeMillis)
        {
            return value == null || !value.HasValue ? null : value.Value.ToString(includeMillis ? DATE_FORMAT2 : DATE_FORMAT1);
        }
    }
}

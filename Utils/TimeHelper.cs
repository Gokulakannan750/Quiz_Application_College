using System;
using TimeZoneConverter;

namespace Quiz_Application_College.Utils
{
    public static class TimeHelper
    {
        // Normalize both "Asia/Kolkata" and "India Standard Time" to a Windows ID
        public static string NormalizeTz(string? tz)
        {
            if (string.IsNullOrWhiteSpace(tz)) return "India Standard Time";
            try
            {
                // If it's IANA (e.g., "Asia/Kolkata"), convert to Windows
                var tzi = TZConvert.GetTimeZoneInfo(tz);
                return tzi.Id;
            }
            catch
            {
                // Fall back to Windows ID
                return tz.Equals("Asia/Kolkata", StringComparison.OrdinalIgnoreCase)
                    ? "India Standard Time"
                    : tz;
            }
        }

        // Convert a local DateTime (picked in UI) + tz to UTC DateTimeOffset
        public static DateTimeOffset LocalToUtc(DateTime local, string tz)
        {
            var winTz = NormalizeTz(tz);
            var tzi = TimeZoneInfo.FindSystemTimeZoneById(winTz);
            var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
            var utc = TimeZoneInfo.ConvertTimeToUtc(unspecified, tzi);
            return new DateTimeOffset(utc, TimeSpan.Zero);
        }

        // Convert a UTC DTO to display in a timezone
        public static DateTime Localize(DateTimeOffset utc, string tz)
        {
            var winTz = NormalizeTz(tz);
            var tzi = TimeZoneInfo.FindSystemTimeZoneById(winTz);
            return TimeZoneInfo.ConvertTime(utc.UtcDateTime, tzi);
        }
    }
}

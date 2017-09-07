using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Ical.Net.DataTypes;
using Ical.Net.Interfaces.DataTypes;
using NodaTime;
using NodaTime.TimeZones;

namespace Ical.Net.Utility
{
    internal class DateUtil
    {
        /// <summary>
        ///     Regex for filtering timeZone wich are not supposed to be a <see cref="TimeZoneInfo.DisplayName"/>.
        /// </summary>
        private static readonly Regex TimeZoneDisplayNameRegex = new Regex(@"^\(UTC[+-]\d{2}:\d{2}\)");

        public static IDateTime StartOfDay(IDateTime dt) => dt.AddHours(-dt.Hour).AddMinutes(-dt.Minute).AddSeconds(-dt.Second);

        public static IDateTime EndOfDay(IDateTime dt) => StartOfDay(dt).AddDays(1).AddTicks(-1);

        public static DateTime GetSimpleDateTimeData(IDateTime dt) => DateTime.SpecifyKind(dt.Value, dt.IsUniversalTime ? DateTimeKind.Utc : DateTimeKind.Local);

        public static DateTime SimpleDateTimeToMatch(IDateTime dt, IDateTime toMatch)
        {
            if (toMatch.IsUniversalTime && dt.IsUniversalTime)
            {
                return dt.Value;
            }
            if (toMatch.IsUniversalTime)
            {
                return dt.Value.ToUniversalTime();
            }
            if (dt.IsUniversalTime)
            {
                return dt.Value.ToLocalTime();
            }
            return dt.Value;
        }

        public static IDateTime MatchTimeZone(IDateTime dt1, IDateTime dt2)
        {
            // Associate the date/time with the first.
            var copy = dt2;
            copy.AssociateWith(dt1);

            // If the dt1 time does not occur in the same time zone as the
            // dt2 time, then let's convert it so they can be used in the
            // same context (i.e. evaluation).
            if (dt1.TzId != null)
            {
                if (!string.Equals(dt1.TzId, copy.TzId))
                {
                    return copy.ToTimeZone(dt1.TzId);
                }
                return copy;
            }
            if (dt1.IsUniversalTime)
            {
                // The first date/time is in UTC time, convert!
                return new CalDateTime(copy.AsUtc);
            }
            // The first date/time is in local time, convert!
            return new CalDateTime(copy.AsSystemLocal);
        }

        public static DateTime AddWeeks(DateTime dt, int interval, DayOfWeek firstDayOfWeek)
        {
            // NOTE: fixes WeeklyUntilWkst2() eval.
            // NOTE: simplified the execution of this - fixes bug #3119920 - missing weekly occurences also
            dt = dt.AddDays(interval * 7);
            while (dt.DayOfWeek != firstDayOfWeek)
            {
                dt = dt.AddDays(-1);
            }

            return dt;
        }

        public static DateTime FirstDayOfWeek(DateTime dt, DayOfWeek firstDayOfWeek, out int offset)
        {
            offset = 0;
            while (dt.DayOfWeek != firstDayOfWeek)
            {
                dt = dt.AddDays(-1);
                offset++;
            }
            return dt;
        }

        private static readonly Dictionary<string, string> _windowsMapping =
            TzdbDateTimeZoneSource.Default.WindowsMapping.PrimaryMapping.ToDictionary(k => k.Key, v => v.Value, StringComparer.OrdinalIgnoreCase);
        public static readonly DateTimeZone LocalDateTimeZone = DateTimeZoneProviders.Tzdb.GetSystemDefault();
        public static DateTimeZone GetZone(string tzId)
        {
            if (string.IsNullOrWhiteSpace(tzId))
            {
                return LocalDateTimeZone;
            }

            if (tzId.StartsWith("/"))
            {
                tzId = tzId.Substring(1, tzId.Length - 1);
            }

            var zone = DateTimeZoneProviders.Tzdb.GetZoneOrNull(tzId);
            if (zone != null)
            {
                return zone;
            }

            string ianaZone;
            if (_windowsMapping.TryGetValue(tzId, out ianaZone))
            {
                return DateTimeZoneProviders.Tzdb.GetZoneOrNull(ianaZone);
            }

            zone = DateTimeZoneProviders.Serialization.GetZoneOrNull(tzId);
            if (zone != null)
            {
                return zone;
            }

            //US/Eastern is commonly represented as US-Eastern
            var newTzId = tzId.Replace("-", "/");
            zone = DateTimeZoneProviders.Serialization.GetZoneOrNull(newTzId);
            if (zone != null)
            {
                return zone;
            }

            foreach (var providerId in DateTimeZoneProviders.Tzdb.Ids.Where(tzId.Contains))
            {
                return DateTimeZoneProviders.Tzdb.GetZoneOrNull(providerId);
            }

            if (_windowsMapping.Keys
                    .Where(tzId.Contains)
                    .Any(providerId => _windowsMapping.TryGetValue(providerId, out ianaZone))
               )
            {
                return DateTimeZoneProviders.Tzdb.GetZoneOrNull(ianaZone);
            }

            var serializationZones = DateTimeZoneProviders.Serialization.Ids.Where(tzId.Contains).ToArray();
            if (serializationZones.Any())
            {
                return DateTimeZoneProviders.Serialization.GetZoneOrNull(serializationZones.First());
            }

            if (TimeZoneDisplayNameRegex.IsMatch(tzId))
            {
                var timeZone = GetSystemTimeZone(tzId);
                if (timeZone == null)
                {
                    throw new ArgumentException($"The given timeZone display name is not found: {tzId}");
                }
            }

            return LocalDateTimeZone;
        }

        public static ZonedDateTime AddYears(ZonedDateTime zonedDateTime, int years)
        {
            var futureDate = zonedDateTime.Date.PlusYears(years);
            var futureLocalDateTime = new LocalDateTime(futureDate.Year, futureDate.Month, futureDate.Day, zonedDateTime.Hour, zonedDateTime.Minute,
                zonedDateTime.Second);
            var zonedFutureDate = new ZonedDateTime(futureLocalDateTime, zonedDateTime.Zone, zonedDateTime.Offset);
            return zonedFutureDate;
        }

        public static ZonedDateTime AddMonths(ZonedDateTime zonedDateTime, int months)
        {
            var futureDate = zonedDateTime.Date.PlusMonths(months);
            var futureLocalDateTime = new LocalDateTime(futureDate.Year, futureDate.Month, futureDate.Day, zonedDateTime.Hour, zonedDateTime.Minute,
                zonedDateTime.Second);
            var zonedFutureDate = new ZonedDateTime(futureLocalDateTime, zonedDateTime.Zone, zonedDateTime.Offset);
            return zonedFutureDate;
        }

        public static ZonedDateTime ToZonedDateTimeLeniently(DateTime dateTime, string tzId)
        {
            var zone = GetZone(tzId);
            var localDt = LocalDateTime.FromDateTime(dateTime); //19:00 UTC
            var lenientZonedDateTime = localDt.InZoneLeniently(zone).WithZone(zone); //15:00 Eastern
            return lenientZonedDateTime;
        }

        public static ZonedDateTime FromTimeZoneToTimeZone(DateTime dateTime, string fromZoneId, string toZoneId)
            => FromTimeZoneToTimeZone(dateTime, GetZone(fromZoneId), GetZone(toZoneId));

        public static ZonedDateTime FromTimeZoneToTimeZone(DateTime dateTime, DateTimeZone fromZone, DateTimeZone toZone)
        {
            var oldZone = LocalDateTime.FromDateTime(dateTime).InZoneLeniently(fromZone);
            var newZone = oldZone.WithZone(toZone);
            return newZone;
        }

        public static bool IsSerializationTimeZone(DateTimeZone zone) => DateTimeZoneProviders.Serialization.GetZoneOrNull(zone.Id) != null;

        /// <summary>
        ///     Returns the <see cref="TimeZoneInfo"/> according to its display name. Bad way but Office365 provides only
        ///     the timezone display name instead of the real the timeZoneId in their ics file.
        /// </summary>
        /// <param name="timeZoneDisplayName">
        ///     The .NET timeZone display name which matches with the <see cref="TimeZoneInfo.DisplayName"/> property.
        /// </param>
        /// <returns>
        ///     The timeZone matches with the given <paramref name="timeZoneDisplayName"/> or null if the it is not found.
        /// </returns>
        private static TimeZoneInfo GetSystemTimeZone(string timeZoneDisplayName)
        {
            return TimeZoneInfo.GetSystemTimeZones().SingleOrDefault(x => x.DisplayName == timeZoneDisplayName);
        }
    }
}
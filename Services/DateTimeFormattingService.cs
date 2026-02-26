using Ticketing.Api.Domain;

namespace Ticketing.Api.Services;

public interface IDateTimeFormattingService
{
    string FormatDateTime(DateTimeOffset dateTime, UserPreferences? preferences);
}

public class DateTimeFormattingService : IDateTimeFormattingService
{
    private const string DefaultTimezone = "Asia/Jerusalem";
    private const string DefaultDateFormat = "dd-MM-yyyy";
    private const string DefaultTimeFormat = "24h";

    public string FormatDateTime(DateTimeOffset dateTime, UserPreferences? preferences)
    {
        var timezone = preferences?.Timezone ?? DefaultTimezone;
        var dateFormat = preferences?.DateFormat ?? DefaultDateFormat;
        var timeFormat = preferences?.TimeFormat ?? DefaultTimeFormat;

        TimeZoneInfo timeZoneInfo;
        try
        {
            timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(timezone);
        }
        catch
        {
            timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(DefaultTimezone);
        }

        var localTime = TimeZoneInfo.ConvertTime(dateTime, timeZoneInfo);

        var dateFormatString = ConvertDateFormat(dateFormat);
        var timeFormatString = timeFormat == "12h" ? "hh:mm tt" : "HH:mm";

        return $"{localTime.ToString(dateFormatString)}, {localTime.ToString(timeFormatString)}";
    }

    private static string ConvertDateFormat(string format)
    {
        return format switch
        {
            "dd-MM-yyyy" => "dd-MM-yyyy",
            "MM-dd-yyyy" => "MM-dd-yyyy",
            "yyyy-MM-dd" => "yyyy-MM-dd",
            "dd/MM/yyyy" => "dd/MM/yyyy",
            "MM/dd/yyyy" => "MM/dd/yyyy",
            "yyyy/MM/dd" => "yyyy/MM/dd",
            "MMMM dd, yyyy" => "MMMM dd, yyyy",
            _ => "dd-MM-yyyy"
        };
    }
}

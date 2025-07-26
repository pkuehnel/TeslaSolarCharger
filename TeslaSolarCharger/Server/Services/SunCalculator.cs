using TeslaSolarCharger.Server.Services.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class SunCalculator : ISunCalculator
{
    // Official zenith angle for sunrise/sunset
    private const double ZenithOfficial = 90.83333333333333;
    // Coefficients for the Sun’s mean anomaly and time correction
    private const double MeanAnomalyCoefficient = 0.9856;     // degrees per day
    private const double MeanAnomalyOffset = 3.289;          // degrees
    private const double TrueLongitudeOffset = 282.634;      // degrees
    private const double SunTimeFactor = 0.06571;            // hours per day
    private const double SunTimeOffset = 6.622;              // hours
    private const double EarthTiltFactor = 0.91764;          // unitless

    public DateTimeOffset? CalculateSunset(double latitude, double longitude, DateTimeOffset date)
    {
        var dateOnly = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
        var dayOfYear = dateOnly.DayOfYear;
        var longitudeHour = longitude / 15.0;

        var approximateTimeSunset = dayOfYear + ((18.0 - longitudeHour) / 24.0);
        var sunMeanAnomalySunset = (MeanAnomalyCoefficient * approximateTimeSunset) - MeanAnomalyOffset;
        var sunTrueLongitudeSunset = NormalizeAngle(
            sunMeanAnomalySunset
            + (1.916 * Math.Sin(DegToRad(sunMeanAnomalySunset)))
            + (0.020 * Math.Sin(2.0 * DegToRad(sunMeanAnomalySunset)))
            + TrueLongitudeOffset
        );

        var rawRightAscensionSunset = RadToDeg(Math.Atan(EarthTiltFactor * Math.Tan(DegToRad(sunTrueLongitudeSunset))));
        var sunRightAscensionSunset = NormalizeRightAscension(sunTrueLongitudeSunset, rawRightAscensionSunset) / 15.0;

        var sinDeclinationSunset = 0.39782 * Math.Sin(DegToRad(sunTrueLongitudeSunset));
        var cosDeclinationSunset = Math.Cos(Math.Asin(sinDeclinationSunset));

        var cosHourAngleSunset =
            (Math.Cos(DegToRad(ZenithOfficial)) - (sinDeclinationSunset * Math.Sin(DegToRad(latitude))))
            / (cosDeclinationSunset * Math.Cos(DegToRad(latitude)));

        if (cosHourAngleSunset > 1.0)
        {
            return null;
        }
        else if (cosHourAngleSunset < -1.0)
        {
            return null;
        }
        else
        {
            var hourAngleSunset = RadToDeg(Math.Acos(cosHourAngleSunset)) / 15.0;
            var localMeanTimeSunset = hourAngleSunset
                                      + sunRightAscensionSunset
                                      - (SunTimeFactor * approximateTimeSunset)
                                      - SunTimeOffset;
            var utcSunsetHour = localMeanTimeSunset - longitudeHour;
            if (utcSunsetHour < 0)
            {
                utcSunsetHour += 24.0;
            }
            return new DateTimeOffset(dateOnly.AddHours(utcSunsetHour));
        }
    }

    public DateTimeOffset? CalculateSunrise(double latitude, double longitude, DateTimeOffset date)
    {
        var dateOnly = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
        var dayOfYear = dateOnly.DayOfYear;
        var longitudeHour = longitude / 15.0;

        //
        // 1) Sunrise
        //
        // approximate time in days (6am local)
        var approximateTimeSunrise = dayOfYear + ((6.0 - longitudeHour) / 24.0);
        var sunMeanAnomalySunrise = (MeanAnomalyCoefficient * approximateTimeSunrise) - MeanAnomalyOffset;
        var sunTrueLongitudeSunrise = NormalizeAngle(
            sunMeanAnomalySunrise
            + (1.916 * Math.Sin(DegToRad(sunMeanAnomalySunrise)))
            + (0.020 * Math.Sin(2.0 * DegToRad(sunMeanAnomalySunrise)))
            + TrueLongitudeOffset
        );

        // right ascension
        var rawRightAscensionSunrise = RadToDeg(Math.Atan(EarthTiltFactor * Math.Tan(DegToRad(sunTrueLongitudeSunrise))));
        var sunRightAscensionSunrise = NormalizeRightAscension(sunTrueLongitudeSunrise, rawRightAscensionSunrise) / 15.0;

        // sun declination
        var sinDeclinationSunrise = 0.39782 * Math.Sin(DegToRad(sunTrueLongitudeSunrise));
        var cosDeclinationSunrise = Math.Cos(Math.Asin(sinDeclinationSunrise));

        // hour angle
        var cosHourAngleSunrise =
            (Math.Cos(DegToRad(ZenithOfficial)) - (sinDeclinationSunrise * Math.Sin(DegToRad(latitude))))
            / (cosDeclinationSunrise * Math.Cos(DegToRad(latitude)));

        if (cosHourAngleSunrise > 1.0)
        {
            // Sun never rises
            return null;
        }

        if (cosHourAngleSunrise < -1.0)
        {
            // Sun never sets
            return null;
        }

        var hourAngleSunrise = (360.0 - RadToDeg(Math.Acos(cosHourAngleSunrise))) / 15.0;
        var localMeanTimeSunrise = hourAngleSunrise
                                   + sunRightAscensionSunrise
                                   - (SunTimeFactor * approximateTimeSunrise)
                                   - SunTimeOffset;
        var utcSunriseHour = localMeanTimeSunrise - longitudeHour;
        return new DateTimeOffset(dateOnly.AddHours(utcSunriseHour), TimeSpan.Zero);
    }

    // Normalize an angle to [0,360)
    private double NormalizeAngle(double angle)
    {
        var result = angle % 360.0;
        if (result < 0.0)
        {
            result += 360.0;
        }

        return result;
    }

    // Adjust right ascension into the same quadrant as the true longitude
    private double NormalizeRightAscension(double trueLongitude, double rightAscension)
    {
        var trueLongitudeQuadrant = Math.Floor(trueLongitude / 90.0) * 90.0;
        var rightAscensionQuadrant = Math.Floor(rightAscension / 90.0) * 90.0;
        return rightAscension + (trueLongitudeQuadrant - rightAscensionQuadrant);
    }

    private double DegToRad(double degrees)
    {
        return (Math.PI / 180.0) * degrees;
    }

    private double RadToDeg(double radians)
    {
        return (180.0 / Math.PI) * radians;
    }
}

using System.Globalization;

namespace MellowHouse.Web.Features;

public static class FulfillmentPolicy
{
    private static readonly TimeZoneInfo Moscow = TimeZoneInfo.FindSystemTimeZoneById("Europe/Moscow");
    public static DateTime LocalNow(TimeProvider clock) => TimeZoneInfo.ConvertTime(clock.GetUtcNow(), Moscow).DateTime;
    public static void ValidateSettings(CafeSettings settings)
    {
        if(!TimeOnly.TryParseExact(settings.OpeningTime,"HH:mm",CultureInfo.InvariantCulture,DateTimeStyles.None,out var open)||
           !TimeOnly.TryParseExact(settings.ClosingTime,"HH:mm",CultureInfo.InvariantCulture,DateTimeStyles.None,out var close)||open>=close)
            throw new ShopException("Укажите время открытия и закрытия в пределах одного дня, например 08:00–22:00.");
        if(settings.DeliveryCities is null||settings.DeliveryCities.Length is <1 or >20||settings.DeliveryCities.Any(x=>string.IsNullOrWhiteSpace(x)||x.Length>80))
            throw new ShopException("Укажите от 1 до 20 городов доставки.");
        if(settings.ClosedWeekdays is null||settings.ClosedWeekdays.Any(x=>x is <0 or >6)||settings.ClosedWeekdays.Distinct().Count()>6)
            throw new ShopException("У кафе должен быть хотя бы один рабочий день в неделю.");
        if(settings.ClosedDates is null||settings.ClosedDates.Length>100||settings.ClosedDates.Any(x=>!DateOnly.TryParseExact(x,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out _)))
            throw new ShopException("Нерабочие даты указываются в формате ГГГГ-ММ-ДД, не более 100 дат.");
    }
    public static string Hours(CafeSettings settings) => $"{(settings.ClosedWeekdays.Length==0?"Ежедневно":"В рабочие дни")} {settings.OpeningTime}–{settings.ClosingTime} (МСК)";
    public static bool IsOpen(CafeSettings settings,DateTime local)
    {
        var time=TimeOnly.FromDateTime(local);
        return !settings.ClosedWeekdays.Contains((int)local.DayOfWeek)&&!settings.ClosedDates.Contains(local.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture))&&
            time>=TimeOnly.ParseExact(settings.OpeningTime,"HH:mm",CultureInfo.InvariantCulture)&&time<TimeOnly.ParseExact(settings.ClosingTime,"HH:mm",CultureInfo.InvariantCulture);
    }
    public static void Validate(CheckoutRequest request,CafeSettings settings,DateTime localNow)
    {
        if(request.Method=="delivery")
        {
            var city=(request.City??"").Trim();
            if(city.StartsWith("г.",StringComparison.OrdinalIgnoreCase))city=city[2..].Trim();
            if(!settings.DeliveryCities.Any(x=>string.Equals(x.Trim(),city,StringComparison.OrdinalIgnoreCase)))
                throw new ShopException($"Доставка доступна в городах: {string.Join(", ",settings.DeliveryCities)}. Для другого адреса выберите самовывоз.",field:"city");
        }
        if(request.Timing=="scheduled")
        {
            if(!DateTime.TryParseExact(request.ScheduledLocal,"yyyy-MM-dd'T'HH:mm",CultureInfo.InvariantCulture,DateTimeStyles.None,out var date)||date<localNow.AddMinutes(30)||date>localNow.AddDays(7))
                throw new ShopException("Выберите время от 30 минут до 7 дней вперёд (МСК).",field:"scheduledLocal");
            if(!IsOpen(settings,date))throw new ShopException($"В это время кафе закрыто. Выберите рабочий день с {settings.OpeningTime} до {settings.ClosingTime} (МСК).",field:"scheduledLocal");
        }
        else if(request.Timing=="asap"&&(!IsOpen(settings,localNow)||!IsOpen(settings,localNow.AddMinutes(30))))
            throw new ShopException($"Приём ближайших заказов закрыт. Кафе работает с {settings.OpeningTime} до {settings.ClosingTime} (МСК); на приготовление нужно 30 минут. Выберите «Ко времени» на рабочий день.",field:"timing");
    }
}

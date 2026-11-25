using System.Text.Json;

try
{
    if (args.Length != 1) throw new ArgumentException("Pass one JSON input file");
    using var data = JsonDocument.Parse(File.ReadAllText(args[0]));
    Console.WriteLine(JsonSerializer.Serialize(Analyze(data.RootElement)));
    return 0;
}
catch (Exception error) when (error is IOException or JsonException or FormatException
                               or ArgumentException or InvalidOperationException
                               or KeyNotFoundException or ArithmeticException or IndexOutOfRangeException)
{
    Console.Error.WriteLine(error.Message);
    return 2;
}

static object Analyze(JsonElement input)
{
    static DateOnly ReadDate(JsonElement row, string key)
    {
        if (DateOnly.TryParseExact(row.GetProperty(key).GetString(), "yyyy-MM-dd",
            System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var date))
            return date;
        throw new ArgumentException(key + " must be a date in YYYY-MM-DD format.");
    }

    var review = ReadDate(input, "as_of");
    var inventory = new List<(string Item, int Units, DateOnly UseBy)>();
    foreach (var row in input.GetProperty("lots").EnumerateArray())
    {
        string? item = row.GetProperty("item").GetString()?.Trim();
        int units = row.GetProperty("units").GetInt32();
        if (string.IsNullOrEmpty(item) || units <= 0)
            throw new ArgumentException("Each lot needs an item name and a positive whole number of units.");
        inventory.Add((item, units, ReadDate(row, "use_by")));
    }

    long past = 0, today = 0, upcoming = 0;
    var ordered = new List<object>();
    foreach (var lot in inventory.OrderBy(lot => lot.UseBy).ThenBy(lot => lot.Item, StringComparer.Ordinal))
    {
        int days = lot.UseBy.DayNumber - review.DayNumber;
        if (days < 0) past += lot.Units;
        else if (days == 0) today += lot.Units;
        else upcoming += lot.Units;
        ordered.Add(new
        {
            item = lot.Item, units = lot.Units,
            use_by = lot.UseBy.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            days_from_review = days,
        });
    }
    return new
    {
        as_of = review.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
        past_label_date_units = past, today_units = today, upcoming_units = upcoming, lots = ordered,
    };
}

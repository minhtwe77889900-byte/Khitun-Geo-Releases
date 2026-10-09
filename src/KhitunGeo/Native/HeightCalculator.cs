namespace KhitunGeo.Native;

internal enum HeightOperation
{
    SetAll,
    AddToAll,
    SubtractFromAll
}

internal static class HeightCalculator
{
    public static double AbsoluteMinusDepth(double absoluteElevation, double depth)
    {
        Validate(absoluteElevation, nameof(absoluteElevation));
        Validate(depth, nameof(depth));
        var result = absoluteElevation - depth;
        if (!double.IsFinite(result)) throw new ArgumentOutOfRangeException(nameof(depth), "Результат расчёта высоты выходит за допустимый диапазон.");
        return result;
    }

    public static SurveyPoint[] Apply(IReadOnlyList<SurveyPoint> points, HeightOperation operation, double value)
    {
        Validate(value, nameof(value));
        return points.Select(point => point with
        {
            Height = operation switch
            {
                HeightOperation.SetAll => value,
                HeightOperation.AddToAll => point.Height + value,
                HeightOperation.SubtractFromAll => point.Height - value,
                _ => throw new ArgumentOutOfRangeException(nameof(operation))
            }
        }).ToArray();
    }

    private static void Validate(double value, string name)
    {
        if (!double.IsFinite(value)) throw new ArgumentOutOfRangeException(name, "Введите конечное числовое значение.");
    }
}

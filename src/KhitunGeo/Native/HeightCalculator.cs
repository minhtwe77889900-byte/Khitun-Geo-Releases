namespace KhitunGeo.Native;

internal enum HeightOperation
{
    SetAll,
    AddToAll,
    SubtractFromAll,
    AbsoluteMinusPoint,
    AbsolutePlusPoint
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
            Height = Calculate(point.Height, operation, value)
        }).ToArray();
    }

    private static double? Calculate(double? height, HeightOperation operation, double value)
    {
        var result = operation switch
            {
                HeightOperation.SetAll => value,
                HeightOperation.AddToAll => height + value,
                HeightOperation.SubtractFromAll => height - value,
                HeightOperation.AbsoluteMinusPoint => value - height,
                HeightOperation.AbsolutePlusPoint => value + height,
                _ => throw new ArgumentOutOfRangeException(nameof(operation))
            };
        if (result is double number) Validate(number, nameof(height));
        return result;
    }

    private static void Validate(double value, string name)
    {
        if (!double.IsFinite(value)) throw new ArgumentOutOfRangeException(name, "Введите конечное числовое значение.");
    }
}

namespace Foodics;

public static class FoodicsApiEnvironment
{
    public const string Sandbox = "Sandbox";
    public const string Production = "Production";

    public static string Normalize(string? value)
    {
        if (string.Equals(value, Production, System.StringComparison.OrdinalIgnoreCase))
        {
            return Production;
        }

        return Sandbox;
    }
}

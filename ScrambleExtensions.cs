using System.Text;
using System.Text.RegularExpressions;
using System.Transactions;

public static class ScrambleExtensions
{
    public static string Scramble(this string stringValue)
    {
        var sb = new StringBuilder("");
        foreach (var character in stringValue)
        {
            switch (character)
            {
                case var _ when Regex.IsMatch(character.ToString(), "[a-zA-Z]"): sb.Append(GetRandomChar()); break;
                case var _ when Regex.IsMatch(character.ToString(), "[0-9]"): sb.Append(GetRandomNumber()); break;
                default: sb.Append(character); break;
            }
        }
        return sb.ToString();
    }
    public static int Scramble(this int stringValue)
    {
        var sb = new StringBuilder("");
        foreach (var character in stringValue.ToString())
        {
            sb.Append(GetRandomNumber());
        }
        return int.Parse(sb.ToString());
    }
    public static DateTime Scramble(this DateTime stringValue)
    {
        Random r = new Random();
        var start = new DateTime(2024, 1, 1).Ticks;
        return new DateTime(r.NextInt64(start, DateTime.Now.Ticks));
    }
    public static decimal Scramble(this decimal stringValue)
    {
        var sb = new StringBuilder("");
        foreach (var character in stringValue.ToString())
        {
            switch (character)
            {
                case var _ when Regex.IsMatch(character.ToString(), "[0-9]"): sb.Append(GetRandomNumber()); break;
                default: sb.Append(character); break;
            }
        }
        return decimal.Parse(sb.ToString());
    }
    public static double Scramble(this double stringValue)
    {
        var sb = new StringBuilder("");
        foreach (var character in stringValue.ToString())
        {
            switch (character)
            {
                case var _ when Regex.IsMatch(character.ToString(), "[0-9]"): sb.Append(GetRandomNumber()); break;
                default: sb.Append(character); break;
            }
        }
        return double.Parse(sb.ToString());
    }
    public static bool Scramble(this bool scrambleValue)
    {
        Random r = new Random();
        var split = Math.Round((double)r.Next(0, 2));

        switch (split)
        {
            case 1: return true;
            case 0: return false;
            default: return false;
        }
    }

    private static char GetRandomChar()
    {
        Random r = new Random();

        var split = Math.Round((double)r.Next(0, 2));
        var numero = 0;

        switch (split)
        {
            case 0: numero = (int)Math.Round((double)r.Next(65, 90)); break;
            case 1: numero = (int)Math.Round((double)r.Next(97, 122)); break;
        }

        return Convert.ToChar(numero);
    }

    private static int GetRandomNumber()
    {
        Random r = new Random();

        return (int)Math.Round((double)r.Next(0, 10));
    }
}
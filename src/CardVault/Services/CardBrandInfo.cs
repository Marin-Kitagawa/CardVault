using System;
using System.Collections.Generic;
using System.Linq;

namespace CardVault.Services;

public enum CardBrand
{
    Generic,
    Visa,
    Mastercard,
    Amex,
    Discover,
    Jcb,
    Diners,
    Maestro,
}

/// <summary>
/// Brand detection, display names and visual palettes.
/// </summary>
public static class CardBrandInfo
{
    public static CardBrand Detect(string digits)
    {
        if (string.IsNullOrEmpty(digits)) return CardBrand.Generic;

        if (StartsWith(digits, "34", "37")) return CardBrand.Amex;
        if (digits[0] == '4') return CardBrand.Visa;

        if (StartsWith(digits, "51", "52", "53", "54", "55")) return CardBrand.Mastercard;
        if (int.TryParse(Sub4(digits), out var i1) && i1 is >= 2221 and <= 2720) return CardBrand.Mastercard;

        if (StartsWith(digits, "6011", "65") ||
            (int.TryParse(Sub4(digits), out var i2) && i2 is >= 644 and <= 649)) return CardBrand.Discover;

        if (int.TryParse(Sub4(digits), out var i3) && i3 is >= 3528 and <= 3589) return CardBrand.Jcb;

        if (StartsWith(digits, "300", "301", "302", "303", "304", "305", "36", "38")) return CardBrand.Diners;

        if (StartsWith(digits, "5018", "5020", "5038", "6304", "6759", "6761", "6762", "6763")) return CardBrand.Maestro;

        return CardBrand.Generic;
    }

    public static string DisplayName(CardBrand brand) => brand switch
    {
        CardBrand.Visa => "Visa",
        CardBrand.Mastercard => "Mastercard",
        CardBrand.Amex => "Amex",
        CardBrand.Discover => "Discover",
        CardBrand.Jcb => "JCB",
        CardBrand.Diners => "Diners Club",
        CardBrand.Maestro => "Maestro",
        _ => "Card",
    };

    public static int CvvLength(CardBrand brand) => brand == CardBrand.Amex ? 4 : 3;

    public static (bool valid, string message) NumberState(string digits)
    {
        if (digits.Length == 0) return (false, "Enter the card number");
        if (digits.Length < 12) return (false, "Number looks too short");
        if (digits.Length > 19) return (false, "Number is too long");

        var brand = Detect(digits);
        if (brand is CardBrand.Amex && digits.Length != 15) return (false, "Amex cards have 15 digits");
        if (brand is not CardBrand.Amex && digits.Length is < 13 or > 19) return (false, "Number has an unusual length");
        if (brand is not CardBrand.Generic && !Luhn.IsValid(digits)) return (false, "Check digit failed — number looks mistyped");
        if (brand is CardBrand.Generic && digits.Length >= 12) return (false, "Couldn't recognise the card brand");

        if (!Luhn.IsValid(digits)) return (false, "Card number is not valid");
        return (true, "Valid card number");
    }

    /// <summary>Start and end colors for the card face. Accent &lt; 0 → brand palette.</summary>
    public static (string start, string end) Palette(CardBrand brand, int accent)
    {
        if (accent >= 0 && accent < CustomPalettes.Count) return CustomPalettes[accent];

        return brand switch
        {
            CardBrand.Visa => ("#141E45", "#3164C7"),
            CardBrand.Mastercard => ("#3B3B44", "#16161A"),
            CardBrand.Amex => ("#1E4D3A", "#0C291D"),
            CardBrand.Discover => ("#E8871E", "#B05A00"),
            CardBrand.Jcb => ("#12355E", "#081F3C"),
            CardBrand.Diners => ("#0E639E", "#0A3C61"),
            CardBrand.Maestro => ("#3A3D44", "#191B20"),
            _ => ("#38342D", "#1A1813"),
        };
    }

    public static IReadOnlyList<(string start, string end)> CustomPalettes { get; } = new (string, string)[]
    {
        ("#7C5CFF", "#4B2CD6"),
        ("#3A7BFF", "#1F4FC4"),
        ("#12B5B0", "#0B7B7A"),
        ("#2BBF4E", "#147A2D"),
        ("#F5A623", "#C77712"),
        ("#F64B7A", "#BC2B52"),
        ("#5C6B8A", "#2F3852"),
        ("#2F3542", "#14171F"),
    };

    private static bool StartsWith(string s, params string[] prefixes)
    {
        foreach (var p in prefixes)
            if (s.StartsWith(p, StringComparison.Ordinal)) return true;
        return false;
    }

    private static string Sub4(string digits)
        => digits.Length >= 4 ? digits.Substring(0, 4) : digits;
}

/// <summary>Luhn check-digit validation.</summary>
public static class Luhn
{
    public static bool IsValid(string digits)
    {
        if (string.IsNullOrEmpty(digits)) return false;
        var sum = 0;
        var dbl = false;
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            if ((uint)(digits[i] - '0') > 9u) return false;
            var n = digits[i] - '0';
            if (dbl)
            {
                n *= 2;
                if (n > 9) n -= 9;
            }
            sum += n;
            dbl = !dbl;
        }
        return sum % 10 == 0 && digits.Length >= 12;
    }
}

/// <summary>Pretty-printing of numbers and expiry.</summary>
public static class CardFormat
{
    public static string Number(string digits)
    {
        digits = new string(Cleanse(digits).ToArray()).TrimStart('0');
        if (digits.Length == 0) return string.Empty;

        var brand = CardBrandInfo.Detect(digits);
        int[] groups = brand == CardBrand.Amex ? new[] { 4, 6, 5 } : new[] { 4, 4, 4, 4, 3 };

        var result = new System.Text.StringBuilder();
        var idx = 0;
        foreach (var size in groups)
        {
            if (idx >= digits.Length) break;
            var take = Math.Min(size, digits.Length - idx);
            if (result.Length > 0) result.Append(' ');
            result.Append(digits, idx, take);
            idx += take;
        }
        return result.ToString();
    }

    public static string Expiry(string rawDigits)
    {
        var d = new string(Cleanse(rawDigits).ToArray());
        return d.Length switch
        {
            0 => string.Empty,
            1 => d,
            _ => d.Substring(0, 2) + "/" + d.Substring(2),
        };
    }

    public static (int month, int yearFull) ParseExpiry(string formatted)
    {
        var d = new string(Cleanse(formatted).ToArray());
        if (d.Length < 4) return (0, 0);
        var month = int.TryParse(d.Substring(0, 2), out var m) ? m : 0;
        var yy = int.TryParse(d.Substring(2, 2), out var y) ? y : 0;
        return (month, 2000 + yy);
    }

    /// <summary>Masked "••••  ••••  ••••  4813" (keeps last four).</summary>
    public static string MaskedNumber(string digits)
    {
        if (string.IsNullOrEmpty(digits)) return "••••  ••••  ••••  ••••";
        var lastFour = digits.Length <= 4 ? digits : digits[^4..];
        var bullets = new string('•', Math.Max(digits.Length - 4, 0));

        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < bullets.Length; i++)
        {
            if (i > 0 && i % 4 == 0) sb.Append(' ');
            sb.Append(bullets[i]);
        }

        return sb.Length == 0 ? lastFour : sb + "  " + lastFour;
    }

    public static string Masked(string? value, char shownSymbol = '•')
        => string.IsNullOrEmpty(value) ? string.Empty : new string(shownSymbol, Math.Max(value.Length, 3));

    private static IEnumerable<char> Cleanse(string input)
    {
        foreach (var c in input)
            if (char.IsDigit(c))
                yield return c;
    }
}
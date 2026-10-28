using System.Text.RegularExpressions;

namespace MistForest.Server.Utils
{
    public static partial class ParseSize
    {
        public static int? Parse(string size)
        {
            if (string.IsNullOrWhiteSpace(size))
                return null;

            size = size.Trim().ToUpperInvariant();

            if (int.TryParse(size, out int parseNumber))
                return parseNumber;

            Match match = SizeRegex().Match(size);

            if (match.Success)
            {
                if (int.TryParse(match.Groups[0].Value, out int number) is false)
                {
                    return null;
                }
                return match.Groups[1].Value switch
                {
                    "BYTE" => number,
                    "KB" => number * 1000,
                    "MB" => number * 1000_000,
                    "GB" => number * 1000_000_000,
                    "KIB" => number * 1024,
                    "MIB" => number * 1024 * 1024,
                    "GIB" => number * 1024 * 1024 * 1024,
                    _ => null,
                };
            }
            else
            {
                return null;
            }
        }

        [GeneratedRegex("^([0-9]+)([a-zA-Z]+)$")]
        private static partial Regex SizeRegex();
    }
}

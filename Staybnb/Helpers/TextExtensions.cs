namespace Staybnb.Helpers;

public static class TextExtensions
{
    // Cuts a string down to at most `max` characters; shorter strings pass through untouched.
    public static string Clip(this string s, int max) => s.Length > max ? s[..max] : s;
}

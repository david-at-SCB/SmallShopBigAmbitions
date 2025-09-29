


public class ConnectionStringBuilder
{
    public static string NormalizeSqlite(string cs, string contentRoot)
    {
        const string prefix = "Data Source=";
        if (!cs.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return cs; // leave untouched (advanced scenarios)
        var remainder = cs[prefix.Length..].Trim();
        var pathPart = remainder.Split(';')[0].Trim();
        if (!Path.IsPathRooted(pathPart))
        {
            var fullPath = Path.Combine(contentRoot, pathPart);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            return $"{prefix}{fullPath}";
        }
        Directory.CreateDirectory(Path.GetDirectoryName(pathPart)!);
        return cs;
    }
}
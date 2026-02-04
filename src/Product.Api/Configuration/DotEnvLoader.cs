namespace Product.Api.Configuration;

public static class DotEnvLoader
{
    public static void LoadIfDevelopment()
    {
        var currentEnv =
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        if (!string.Equals(currentEnv, "Development", StringComparison.OrdinalIgnoreCase))
            return;

        var candidates = new[]
        {
            ".env.development",
            ".env",
            Path.Combine("..", ".env.development"),
            Path.Combine("..", ".env"),
            Path.Combine("..", "..", ".env.development"),
            Path.Combine("..", "..", ".env"),
            Path.Combine("..", "..", "..", ".env.development"),
            Path.Combine("..", "..", "..", ".env"),
        };

        var envPath = candidates.FirstOrDefault(File.Exists);
        if (string.IsNullOrEmpty(envPath))
            return;

        foreach (var raw in File.ReadLines(envPath))
        {
            var line = raw.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith('#'))
                continue;
            var idx = line.IndexOf('=');
            if (idx <= 0)
                continue;
            var key = line.Substring(0, idx).Trim();
            var val = line.Substring(idx + 1).Trim();
            if (
                (val.StartsWith("\"") && val.EndsWith("\""))
                || (val.StartsWith("'") && val.EndsWith("'"))
            )
                val = val.Substring(1, val.Length - 2);
            Environment.SetEnvironmentVariable(key, val, EnvironmentVariableTarget.Process);
        }
    }
}

namespace AuthService.Application.Utils;

public static class APIUtils
{
    public static async Task<T> HandleWithMinimumTime<T>(Func<Task<T>> action, int minimumDurationMs)
    {
        DateTime start = DateTime.UtcNow;

        T result = await action();

        TimeSpan elapsed = DateTime.UtcNow - start;
        TimeSpan minDuration = TimeSpan.FromMilliseconds(minimumDurationMs);

        if (elapsed < minDuration)
        {
            TimeSpan remaining = minDuration - elapsed;
            await Task.Delay(remaining);
        }

        return result;        
    }
}
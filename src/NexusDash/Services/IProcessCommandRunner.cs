namespace NexusDash.Services
{
    public interface IProcessCommandRunner
    {
        string ReadOutput(string fileName, string arguments, int timeoutMilliseconds = 1500);
    }
}

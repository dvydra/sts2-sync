namespace Sts2Sync.Core.Tests;

public static class TestHelpers
{
    public static string LoadTestData(string filename)
        => File.ReadAllText(Path.Combine("TestData", filename));
}

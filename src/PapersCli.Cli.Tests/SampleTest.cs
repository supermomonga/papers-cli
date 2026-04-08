namespace PapersCli.Cli.Tests;

public class SampleTest
{
    [Test]
    public async Task SamplePassingTest()
    {
        await Assert.That(1 + 1).IsEqualTo(2);
    }
}

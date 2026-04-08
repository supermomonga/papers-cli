using ConsoleAppFramework;

var app = ConsoleApp.Create();
app.Add<RootCommands>();
app.Run(args);

/// <summary>
/// papers-cli commands.
/// </summary>
public class RootCommands
{
    /// <summary>
    /// Display a greeting message.
    /// </summary>
    /// <param name="name">-n, Name to greet.</param>
    [Command("hello")]
    public void Hello(string name = "world")
    {
        Console.WriteLine($"Hello, {name}!");
    }
}

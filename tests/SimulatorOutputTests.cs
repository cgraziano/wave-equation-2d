using System.Text.RegularExpressions;
using Xunit;

/// <summary>
/// Runs the simulator and validates the structure of the generated frames.js.
/// These tests catch malformed JS output before it reaches the browser.
/// </summary>
public class SimulatorOutputTests : IDisposable
{
    private readonly string _outputPath;

    public SimulatorOutputTests()
    {
        _outputPath = Path.Combine(Path.GetTempPath(), $"frames-test-{Guid.NewGuid():N}.js");
        // Run the simulator, pointing output to a temp file
        Simulator.Run(_outputPath);
    }

    public void Dispose()
    {
        if (File.Exists(_outputPath)) File.Delete(_outputPath);
    }

    // -------------------------------------------------------------------------

    [Fact]
    public void Output_FileExists()
    {
        Assert.True(File.Exists(_outputPath), $"Simulator did not create output file at {_outputPath}");
    }

    [Fact]
    public void Output_StartsWithScenariosDeclaration()
    {
        string content = File.ReadAllText(_outputPath);
        Assert.Contains("const SCENARIOS = {", content);
    }

    [Fact]
    public void Output_EndsWithClosingBrace()
    {
        // After stripping trailing whitespace the last non-empty line must be "};"
        string[] lines = File.ReadAllLines(_outputPath);
        string lastLine = lines.LastOrDefault(l => l.Trim().Length > 0)?.Trim() ?? "";
        Assert.Equal("};", lastLine);
    }

    [Fact]
    public void Output_ContainsDoubleSlit_Scenario()
    {
        string content = File.ReadAllText(_outputPath);
        Assert.Contains("doubleSlit:", content);
    }

    [Fact]
    public void Output_ContainsTwoSource_Scenario()
    {
        string content = File.ReadAllText(_outputPath);
        Assert.Contains("twoSource:", content);
    }

    [Fact]
    public void Output_EachScenario_HasRequiredFields()
    {
        string content = File.ReadAllText(_outputPath);
        foreach (string field in new[] { "title:", "description:", "width:", "height:", "frameCount:", "frames:" })
        {
            int count = Regex.Matches(content, Regex.Escape(field)).Count;
            Assert.True(count >= 2, $"Expected field '{field}' to appear at least once per scenario (found {count}).");
        }
    }

    [Fact]
    public void Output_Frames_AreValidBase64()
    {
        string content = File.ReadAllText(_outputPath);

        // Extract all quoted base64 strings from the frames arrays
        var matches = Regex.Matches(content, @"""([A-Za-z0-9+/=]{20,})""");
        Assert.True(matches.Count > 0, "No base64 frame strings found in output.");

        foreach (Match match in matches)
        {
            string b64 = match.Groups[1].Value;
            var ex = Record.Exception(() => Convert.FromBase64String(b64));
            Assert.Null(ex);
        }
    }

    [Fact]
    public void Output_FrameCount_MatchesDeclaredValue()
    {
        string content = File.ReadAllText(_outputPath);

        // For each scenario block, check that the number of base64 frame strings
        // matches the declared frameCount value.
        var scenarioBlocks = Regex.Matches(content,
            @"frameCount:\s*(\d+).*?frames:\s*\[(.*?)\]",
            RegexOptions.Singleline);

        Assert.True(scenarioBlocks.Count > 0, "No scenario blocks found.");

        foreach (Match block in scenarioBlocks)
        {
            int declared = int.Parse(block.Groups[1].Value);
            int actual   = Regex.Matches(block.Groups[2].Value, @"""[A-Za-z0-9+/=]{20,}""").Count;
            Assert.Equal(declared, actual);
        }
    }
}

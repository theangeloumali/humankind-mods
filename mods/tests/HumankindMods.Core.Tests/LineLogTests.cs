
namespace HumankindMods.Core.Tests;

public class LineLogTests
{
    [Fact]
    public void Writes_timestamped_lines_and_creates_the_folder()
    {
        var dir = Path.Combine(Path.GetTempPath(), "linelog-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(dir, "nested", "diagnostics.log");
        try
        {
            using (var log = new LineLog(path))
            {
                log.Write("first");
                log.Write("second");
            }

            var lines = File.ReadAllLines(path);
            Assert.Equal(2, lines.Length);
            Assert.Matches(@"^\d{4}-\d\d-\d\d \d\d:\d\d:\d\d first$", lines[0]);
            Assert.EndsWith(" second", lines[1]);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Appends_to_an_existing_file()
    {
        var path = Path.GetTempFileName();
        try
        {
            using (var log = new LineLog(path)) log.Write("one");
            using (var log = new LineLog(path)) log.Write("two");

            Assert.Equal(2, File.ReadAllLines(path).Length);
        }
        finally
        {
            File.Delete(path);
        }
    }
}

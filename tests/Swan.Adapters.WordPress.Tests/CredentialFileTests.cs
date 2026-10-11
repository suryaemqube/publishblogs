using Swan.Adapters.WordPress;
using Xunit;

namespace Swan.Adapters.WordPress.Tests;

public class CredentialFileTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "swan-tests-" + Guid.NewGuid().ToString("N"));

    public CredentialFileTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Write(string content)
    {
        var path = Path.Combine(_dir, "creds.txt");
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void Parses_key_value_lines_and_ignores_comments_and_blanks()
    {
        var path = Write("# comment\n\nRestOrigin=https://wp.example.com\nUsername = writer \nApplicationPassword=a b c\n");

        var map = CredentialFile.Load(path);

        Assert.Equal("https://wp.example.com", CredentialFile.Require(map, "RestOrigin"));
        Assert.Equal("writer", CredentialFile.Require(map, "username"));
        Assert.Equal("a b c", CredentialFile.Require(map, "ApplicationPassword"));
    }

    [Fact]
    public void Empty_file_is_rejected_with_a_clear_message()
    {
        var path = Write("");
        var ex = Assert.Throws<InvalidDataException>(() => CredentialFile.Load(path));
        Assert.Contains("empty", ex.Message);
    }

    [Fact]
    public void Missing_key_is_reported_by_name()
    {
        var map = CredentialFile.Load(Write("Username=writer\n"));
        var ex = Assert.Throws<InvalidDataException>(() => CredentialFile.Require(map, "ApplicationPassword"));
        Assert.Contains("ApplicationPassword", ex.Message);
    }
}

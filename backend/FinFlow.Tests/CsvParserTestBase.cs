using System.Text;

namespace FinFlow.Tests;

/// <summary>
/// Writes CSV strings to temporary files and cleans them up afterwards.
/// Tests pass the file paths to CanParse/Parse without needing real bank data.
/// </summary>
public abstract class CsvParserTestBase : IDisposable
{
    static CsvParserTestBase()
    {
        // BankCsvParserBase needs Windows-1252 as a fallback encoding; on .NET Core the
        // provider must be registered once, otherwise GetEncoding(1252) throws.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    private readonly List<string> _tempFiles = [];

    protected string CreateTempCsv(string content, Encoding? encoding = null)
    {
        string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".csv");
        File.WriteAllText(path, content, encoding ?? new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        _tempFiles.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (string f in _tempFiles)
            if (File.Exists(f)) File.Delete(f);
    }
}

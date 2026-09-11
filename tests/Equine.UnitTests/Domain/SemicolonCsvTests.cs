using Equine.Infrastructure.Import;
using Shouldly;
using Xunit;

namespace Equine.UnitTests.Domain;

public class SemicolonCsvTests
{
    [Fact]
    public void Reads_headers_and_quoted_semicolons()
    {
        var csv = "name;email\n\"Anna; A\";anna@ex.se\n";
        using var reader = new StringReader(csv);
        var rows = SemicolonCsv.Read(reader);
        rows.Count.ShouldBe(1);
        rows[0]["name"].ShouldBe("Anna; A");
        rows[0]["email"].ShouldBe("anna@ex.se");
    }
}

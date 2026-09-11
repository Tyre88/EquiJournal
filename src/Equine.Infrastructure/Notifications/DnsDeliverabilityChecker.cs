using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Equine.Infrastructure.Notifications;

public sealed class DnsDeliverabilityResult
{
    public bool Passed { get; init; }
    public string Domain { get; init; } = "";
    public bool HasSpf { get; init; }
    public bool HasDmarc { get; init; }
    public string? Error { get; init; }
}

public sealed class DnsDeliverabilityChecker
{
    public async Task<DnsDeliverabilityResult> CheckAsync(string domain, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(domain)
            || domain.Contains("localhost", StringComparison.OrdinalIgnoreCase)
            || domain.EndsWith(".local", StringComparison.OrdinalIgnoreCase))
        {
            return new DnsDeliverabilityResult { Passed = true, Domain = domain, HasSpf = true, HasDmarc = true };
        }

        try
        {
            var spfRecords = await QueryTxtAsync(domain, cancellationToken);
            var dmarcRecords = await QueryTxtAsync($"_dmarc.{domain}", cancellationToken);
            var hasSpf = spfRecords.Any(r => r.Contains("v=spf1", StringComparison.OrdinalIgnoreCase));
            var hasDmarc = dmarcRecords.Any(r => r.Contains("v=DMARC1", StringComparison.OrdinalIgnoreCase));
            return new DnsDeliverabilityResult
            {
                Domain = domain,
                HasSpf = hasSpf,
                HasDmarc = hasDmarc,
                Passed = hasSpf && hasDmarc
            };
        }
        catch (Exception ex)
        {
            return new DnsDeliverabilityResult { Domain = domain, Passed = false, Error = ex.Message };
        }
    }

    public static async Task<IReadOnlyList<string>> QueryTxtAsync(string name, CancellationToken cancellationToken)
    {
        var query = BuildTxtQuery(name);
        using var udp = new UdpClient();
        udp.Client.ReceiveTimeout = 3000;
        udp.Client.SendTimeout = 3000;
        await udp.SendAsync(query, query.Length, "8.8.8.8", 53);
        var receive = udp.ReceiveAsync(cancellationToken);
        var completed = await Task.WhenAny(receive.AsTask(), Task.Delay(3000, cancellationToken));
        if (completed != receive.AsTask())
            return [];
        return ParseTxt(receive.AsTask().Result.Buffer);
    }

    private static byte[] BuildTxtQuery(string name)
    {
        using var ms = new MemoryStream();
        var id = (ushort)Random.Shared.Next(1, 65535);
        ms.WriteByte((byte)(id >> 8));
        ms.WriteByte((byte)id);
        ms.WriteByte(0x01);
        ms.WriteByte(0x00);
        ms.WriteByte(0x00);
        ms.WriteByte(0x01);
        ms.Write(new byte[6], 0, 6);
        foreach (var label in name.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            var bytes = Encoding.ASCII.GetBytes(label);
            ms.WriteByte((byte)bytes.Length);
            ms.Write(bytes, 0, bytes.Length);
        }
        ms.WriteByte(0);
        ms.WriteByte(0x00);
        ms.WriteByte(0x10);
        ms.WriteByte(0x00);
        ms.WriteByte(0x01);
        return ms.ToArray();
    }

    private static IReadOnlyList<string> ParseTxt(byte[] response)
    {
        var results = new List<string>();
        if (response.Length < 12) return results;
        var answers = (response[6] << 8) | response[7];
        var offset = 12;
        while (offset < response.Length && response[offset] != 0)
        {
            if ((response[offset] & 0xC0) == 0xC0) { offset += 2; break; }
            offset += response[offset] + 1;
        }
        offset++;
        offset += 4;
        for (var i = 0; i < answers && offset + 10 < response.Length; i++)
        {
            if ((response[offset] & 0xC0) == 0xC0) offset += 2;
            else
            {
                while (offset < response.Length && response[offset] != 0)
                    offset += response[offset] + 1;
                offset++;
            }
            if (offset + 8 >= response.Length) break;
            var type = (response[offset] << 8) | response[offset + 1];
            offset += 8;
            var rdlen = (response[offset] << 8) | response[offset + 1];
            offset += 2;
            if (type == 16)
            {
                var end = Math.Min(offset + rdlen, response.Length);
                var cursor = offset;
                var sb = new StringBuilder();
                while (cursor < end)
                {
                    var len = response[cursor];
                    cursor++;
                    if (cursor + len > end) break;
                    sb.Append(Encoding.ASCII.GetString(response, cursor, len));
                    cursor += len;
                }
                results.Add(sb.ToString());
            }
            offset += rdlen;
        }
        return results;
    }
}

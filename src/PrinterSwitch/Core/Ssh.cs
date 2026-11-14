using System.IO;
using System.Net.Sockets;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace PrinterSwitch.Core;

public sealed record CommandResult(int Code, string Output, string Error)
{
    public bool Ok => Code == 0;
    public string All => (Output + "\n" + Error).Trim();
}

/// <summary>Thrown when the server presents a different host key than the one trusted earlier.</summary>
public sealed class HostKeyMismatchException(string expected, string actual)
    : Exception($"host key changed: expected SHA256:{expected}, got SHA256:{actual}")
{
    public string Expected { get; } = expected;
    public string Actual { get; } = actual;
}

/// <summary>Thin wrapper around SSH.NET: connections, commands and host key pinning.</summary>
public static class Ssh
{
    public static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(8);

    /// <summary>
    /// Opens a connection. <paramref name="trustedFingerprint"/> empty means trust on first use;
    /// the fingerprint actually seen is returned through <paramref name="seenFingerprint"/>.
    /// </summary>
    public static SshClient Connect(string host, int port, string user, AuthenticationMethod auth,
                                    string trustedFingerprint, out string seenFingerprint)
    {
        var info = new ConnectionInfo(host, port, user, auth) { Timeout = ConnectTimeout };
        var client = new SshClient(info) { KeepAliveInterval = TimeSpan.FromSeconds(15) };
        string seen = "";
        client.HostKeyReceived += (_, e) =>
        {
            seen = e.FingerPrintSHA256;
            e.CanTrust = trustedFingerprint.Length == 0 || trustedFingerprint == seen;
        };
        try
        {
            client.Connect();
        }
        catch (SshConnectionException) when (trustedFingerprint.Length > 0 && seen.Length > 0 && seen != trustedFingerprint)
        {
            client.Dispose();
            throw new HostKeyMismatchException(trustedFingerprint, seen);
        }
        catch
        {
            client.Dispose();
            throw;
        }
        seenFingerprint = seen;
        return client;
    }

    public static SshClient Connect(Settings s)
        => Connect(s.Host, s.Port, s.User, KeyAuth(s.User, s.KeyPath), s.HostKeyFingerprint, out _);

    public static AuthenticationMethod KeyAuth(string user, string keyPath)
        => new PrivateKeyAuthenticationMethod(user, new PrivateKeyFile(keyPath));

    public static CommandResult Run(SshClient client, string command, TimeSpan timeout, string? stdin = null)
    {
        using var cmd = client.CreateCommand(command);
        cmd.CommandTimeout = timeout;
        using var cts = new CancellationTokenSource(timeout);
        Task task = cmd.ExecuteAsync(cts.Token);
        if (stdin != null)
        {
            using var input = cmd.CreateInputStream();
            byte[] data = Encoding.UTF8.GetBytes(stdin);
            input.Write(data, 0, data.Length);
        }
        try
        {
            task.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            return new CommandResult(-1, cmd.Result, cmd.Error + "\n(timed out after " + (int)timeout.TotalSeconds + " s)");
        }
        return new CommandResult(cmd.ExitStatus ?? -1, cmd.Result, cmd.Error);
    }

    /// <summary>Human readable reason for a connection failure.</summary>
    public static string Describe(Exception e) => e switch
    {
        HostKeyMismatchException => L.T("err.hostkey"),
        SshAuthenticationException => L.T("err.auth"),
        SocketException se when se.SocketErrorCode == SocketError.HostNotFound => L.T("err.hostnotfound"),
        SocketException se when se.SocketErrorCode == SocketError.ConnectionRefused => L.T("err.refused"),
        SocketException => L.T("err.unreachable"),
        SshOperationTimeoutException => L.T("err.unreachable"),
        TimeoutException => L.T("err.unreachable"),
        SshException => e.Message,
        _ => e.Message
    };

    // -------------------------------------------------------------- keys ---

    static string KeyFolder => Settings.Folder;

    /// <summary>Unencrypted private keys that could be used, the app's own first.</summary>
    public static IEnumerable<string> CandidateKeys()
    {
        string own = Path.Combine(KeyFolder, "id_rsa_printerswitch");
        if (File.Exists(own)) yield return own;
        string ssh = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh");
        foreach (string name in new[] { "id_ed25519", "id_ecdsa", "id_rsa" })
        {
            string p = Path.Combine(ssh, name);
            if (!File.Exists(p)) continue;
            bool usable;
            try { using var _ = new PrivateKeyFile(p); usable = true; }
            catch { usable = false; } // passphrase protected or unsupported
            if (usable) yield return p;
        }
    }

    /// <summary>Creates (or reuses) the app's own RSA key. Returns the private key path.</summary>
    public static string EnsureOwnKey()
    {
        Directory.CreateDirectory(KeyFolder);
        string path = Path.Combine(KeyFolder, "id_rsa_printerswitch");
        if (File.Exists(path)) return path;
        using var rsa = RSA.Create(3072);
        File.WriteAllText(path, rsa.ExportRSAPrivateKeyPem() + "\n");
        File.WriteAllText(path + ".pub", PublicKeyLine(rsa) + "\n");
        return path;
    }

    /// <summary>The authorized_keys line for a private key file.</summary>
    public static string PublicKeyLineFor(string privateKeyPath)
    {
        string pub = privateKeyPath + ".pub";
        if (File.Exists(pub)) return File.ReadAllText(pub).Trim();
        using var rsa = RSA.Create();
        rsa.ImportFromPem(File.ReadAllText(privateKeyPath));
        return PublicKeyLine(rsa);
    }

    static string PublicKeyLine(RSA rsa)
    {
        var p = rsa.ExportParameters(false);
        using var ms = new MemoryStream();
        WriteString(ms, Encoding.ASCII.GetBytes("ssh-rsa"));
        WriteString(ms, MpInt(p.Exponent!));
        WriteString(ms, MpInt(p.Modulus!));
        return "ssh-rsa " + Convert.ToBase64String(ms.ToArray()) + " printer-switch@" + Environment.MachineName;
    }

    static byte[] MpInt(byte[] bigEndianUnsigned)
        => new BigInteger(bigEndianUnsigned, isUnsigned: true, isBigEndian: true).ToByteArray(isBigEndian: true);

    static void WriteString(Stream s, byte[] data)
    {
        Span<byte> len = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(len, data.Length);
        s.Write(len);
        s.Write(data);
    }
}

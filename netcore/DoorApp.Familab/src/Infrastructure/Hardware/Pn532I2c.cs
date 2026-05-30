using System.Device.I2c;
using System.Diagnostics;

namespace DoorApp.Familab.Infrastructure.Hardware;

/// <summary>
/// Minimal PN532 NFC controller driver over I2C, implementing the subset of the
/// protocol the door controller needs: SAM configuration and reading a passive
/// ISO14443A target (the same operations the Python adafruit_pn532 library performed).
/// </summary>
public sealed class Pn532I2c : IDisposable
{
    // PN532 command bytes.
    private const byte CmdSamConfiguration = 0x14;
    private const byte CmdInListPassiveTarget = 0x4A;

    // Frame markers.
    private const byte HostToPn532 = 0xD4;
    private const byte Pn532ToHost = 0xD5;
    private const byte Preamble = 0x00;
    private const byte StartCode2 = 0xFF;

    private static readonly byte[] AckFrame = { 0x00, 0x00, 0xFF, 0x00, 0xFF, 0x00 };

    private readonly I2cDevice _device;
    private bool _disposed;

    public Pn532I2c(int busId, int address)
    {
        _device = I2cDevice.Create(new I2cConnectionSettings(busId, address));
    }

    /// <summary>Configure the Secure Access Module in normal mode (required after power-up).</summary>
    public bool SamConfiguration()
    {
        // mode = normal (0x01), timeout = 20 (×50ms), use IRQ = 0x01
        if (!WriteCommand(new byte[] { CmdSamConfiguration, 0x01, 0x14, 0x01 }))
        {
            return false;
        }

        return ReadResponse(CmdSamConfiguration, out _);
    }

    /// <summary>
    /// Reads a single passive ISO14443A target (106 kbps) and returns its UID, or null
    /// if no card is present within <paramref name="timeout"/>.
    /// </summary>
    public byte[]? ReadPassiveTargetUid(TimeSpan timeout)
    {
        // 0x01 = max 1 target, 0x00 = 106 kbps type A
        if (!WriteCommand(new byte[] { CmdInListPassiveTarget, 0x01, 0x00 }, (int)timeout.TotalMilliseconds))
        {
            return null;
        }

        if (!ReadResponse(CmdInListPassiveTarget, out var data, (int)timeout.TotalMilliseconds))
        {
            return null;
        }

        // data layout: [NbTg, Tg, SENS_RES(2), SEL_RES, NFCIDLength, NFCID...]
        if (data.Length < 6 || data[0] < 1)
        {
            return null;
        }

        int uidLen = data[5];
        if (uidLen <= 0 || 6 + uidLen > data.Length)
        {
            return null;
        }

        return data[6..(6 + uidLen)];
    }

    private bool WriteCommand(byte[] payloadBody, int timeoutMs = 1000)
    {
        // payload = TFI + command + args
        var payload = new byte[payloadBody.Length + 1];
        payload[0] = HostToPn532;
        Array.Copy(payloadBody, 0, payload, 1, payloadBody.Length);

        byte length = (byte)payload.Length;
        byte lengthChecksum = (byte)(~length + 1);

        int sum = 0;
        foreach (var b in payload)
        {
            sum += b;
        }
        byte dataChecksum = (byte)(~sum + 1);

        var frame = new byte[payload.Length + 7];
        var i = 0;
        frame[i++] = Preamble;
        frame[i++] = Preamble;
        frame[i++] = StartCode2;
        frame[i++] = length;
        frame[i++] = lengthChecksum;
        Array.Copy(payload, 0, frame, i, payload.Length);
        i += payload.Length;
        frame[i++] = dataChecksum;
        frame[i] = Preamble;

        try
        {
            _device.Write(frame);
        }
        catch (Exception)
        {
            return false;
        }

        return WaitReady(timeoutMs) && ReadAck();
    }

    private bool WaitReady(int timeoutMs)
    {
        var sw = Stopwatch.StartNew();
        Span<byte> status = stackalloc byte[1];
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            try
            {
                _device.Read(status);
            }
            catch (Exception)
            {
                return false;
            }

            if ((status[0] & 0x01) == 0x01)
            {
                return true;
            }

            Thread.Sleep(5);
        }

        return false;
    }

    private bool ReadAck()
    {
        // First byte read is the I2C ready/status byte, followed by the 6-byte ACK frame.
        var buffer = new byte[1 + AckFrame.Length];
        try
        {
            _device.Read(buffer);
        }
        catch (Exception)
        {
            return false;
        }

        for (var i = 0; i < AckFrame.Length; i++)
        {
            if (buffer[i + 1] != AckFrame[i])
            {
                return false;
            }
        }

        return true;
    }

    private bool ReadResponse(byte expectedCommand, out byte[] data, int timeoutMs = 1000)
    {
        data = Array.Empty<byte>();

        if (!WaitReady(timeoutMs))
        {
            return false;
        }

        // status + (preamble[2] + startcode + len + lcs + tfi + respcmd + data + dcs + postamble)
        var buffer = new byte[64];
        try
        {
            _device.Read(buffer);
        }
        catch (Exception)
        {
            return false;
        }

        // buffer[0] is the ready/status byte; frame starts at buffer[1].
        // Expected: 00 00 FF LEN LCS D5 <respcmd> <data...> DCS 00
        if (buffer[1] != Preamble || buffer[2] != Preamble || buffer[3] != StartCode2)
        {
            return false;
        }

        int length = buffer[4];
        if (length < 2 || 6 + length > buffer.Length)
        {
            return false;
        }

        if (buffer[6] != Pn532ToHost || buffer[7] != expectedCommand + 1)
        {
            return false;
        }

        // data length excludes TFI and response command bytes.
        int dataLen = length - 2;
        if (dataLen < 0 || 8 + dataLen > buffer.Length)
        {
            return false;
        }

        data = buffer[8..(8 + dataLen)];
        return true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _device.Dispose();
    }
}

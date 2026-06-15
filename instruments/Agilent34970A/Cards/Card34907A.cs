namespace Agilent34970A.Cards;

/// <summary>
/// Represents the Agilent 34907A Multifunction Module (DAC + digital I/O).
/// </summary>
public class Card34907A
{
    public int Slot { get; }

    public Card34907A(int slot)
    {
        if (slot != 100 && slot != 200 && slot != 300)
            throw new ArgumentOutOfRangeException(nameof(slot), "Slot must be 100, 200, or 300.");
        Slot = slot;
    }

    /// <summary>
    /// Builds a SCPI command to set a DAC output voltage.
    /// dacChannel: 1 or 2.
    /// </summary>
    public string BuildSetDacCommand(int dacChannel, double voltage)
    {
        if (dacChannel != 1 && dacChannel != 2)
            throw new ArgumentOutOfRangeException(nameof(dacChannel), "DAC channel must be 1 or 2.");

        int scpiChannel = Slot + dacChannel;
        return $"SOUR:VOLT {voltage.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)},(@{scpiChannel})";
    }

    /// <summary>
    /// Builds a SCPI command to write a byte to the lower digital output port (port 1).
    /// </summary>
    public string BuildDigitalWriteCommand(byte value)
    {
        int scpiChannel = Slot + 1;
        return $"SOUR:DIG:DATA:BYTE {value},(@{scpiChannel})";
    }

    /// <summary>
    /// Builds a SCPI query command to read a byte from the digital input port (port 1).
    /// </summary>
    public string BuildDigitalReadCommand()
    {
        int scpiChannel = Slot + 1;
        return $"SENS:DIG:DATA:BYTE? (@{scpiChannel})";
    }

    public override string ToString() => $"34907A in slot {Slot}";
}

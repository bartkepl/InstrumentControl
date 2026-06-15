using System.Text;

namespace Agilent34970A.Cards;

public enum MuxFunction { VDC, VAC, OHM2W, OHM4W, TEMP_TC, TEMP_RTD, FREQ, PERIOD }

public enum TcType { B, E, J, K, N, R, S, T }

/// <summary>
/// Represents the Agilent 34901A 20-channel multiplexer card.
/// </summary>
public class Card34901A
{
    public int Slot { get; }
    public int ChannelCount => 20;

    public Card34901A(int slot)
    {
        if (slot != 100 && slot != 200 && slot != 300)
            throw new ArgumentOutOfRangeException(nameof(slot), "Slot must be 100, 200, or 300.");
        Slot = slot;
    }

    /// <summary>
    /// Returns a SCPI channel list string such as "@101,102,103".
    /// Channels are 1-based (1–20); they are offset by the slot number.
    /// </summary>
    public string GetChannelList(IEnumerable<int> channels)
    {
        var numbers = channels
            .Select(ch =>
            {
                if (ch < 1 || ch > 20)
                    throw new ArgumentOutOfRangeException(nameof(channels), $"Channel {ch} is out of range 1-20.");
                return (Slot + ch).ToString();
            });
        return "@" + string.Join(",", numbers);
    }

    /// <summary>
    /// Builds a CONF command for the given measurement function on the specified channels.
    /// </summary>
    public string BuildConfCommand(
        MuxFunction function,
        string range,
        IEnumerable<int> channels,
        TcType tcType = TcType.K)
    {
        string channelList = GetChannelList(channels);

        return function switch
        {
            MuxFunction.VDC    => $"CONF:VOLT:DC {range},DEF,({channelList})",
            MuxFunction.VAC    => $"CONF:VOLT:AC {range},DEF,({channelList})",
            MuxFunction.OHM2W  => $"CONF:RES {range},DEF,({channelList})",
            MuxFunction.OHM4W  => $"CONF:FRES {range},DEF,({channelList})",
            MuxFunction.TEMP_TC  => BuildTempTcCommand(tcType, channels),
            MuxFunction.TEMP_RTD => $"CONF:TEMP RTD,85,({channelList})",
            MuxFunction.FREQ   => $"CONF:FREQ {range},DEF,({channelList})",
            MuxFunction.PERIOD => $"CONF:PER {range},DEF,({channelList})",
            _ => throw new ArgumentOutOfRangeException(nameof(function))
        };
    }

    /// <summary>
    /// Builds a CONF:TEMP command for a thermocouple measurement on the specified channels.
    /// </summary>
    public string BuildTempTcCommand(TcType tcType, IEnumerable<int> channels)
    {
        string channelList = GetChannelList(channels);
        return $"CONF:TEMP TC,{tcType},({channelList})";
    }

    public override string ToString() => $"34901A in slot {Slot}";
}

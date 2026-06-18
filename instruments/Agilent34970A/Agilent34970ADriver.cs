using System.Globalization;
using System.Windows;
using Agilent34970A.Cards;
using Agilent34970A.Views;
using InstrumentControl.Core.Base;
using InstrumentControl.Core.Interfaces;
using InstrumentControl.Core.Models;
using InstrumentControl.Core.Services;

namespace Agilent34970A;

[InstrumentDriver]
public class Agilent34970ADriver : InstrumentDriverBase
{
    // ── Identity ──────────────────────────────────────────────────────────────
    public override string DriverName => "Agilent34970A";
    public override string Manufacturer => "Agilent Technologies";
    public override string Model => "34970A";
    public override string Description => "Data Acquisition/Switch Unit";
    public override string[] SupportedResourcePatterns =>
        new[] { "GPIB?*::?*::INSTR", "USB?*::?*::INSTR" };

    /// <summary>Poprawne numery slotów (forma SCPI).</summary>
    public static readonly int[] Slots = { 100, 200, 300 };

    // ── Card management (per-karta) ─────────────────────────────────────────────

    /// <summary>Slot → obiekt karty (CardBase: Card34901A, Card34907A, GenericCard).</summary>
    public Dictionary<int, CardBase> Cards { get; } = new();

    /// <summary>Ręczna konfiguracja karty (tryb offline / symulacja).</summary>
    public void SetCard(int slot, string? model)
    {
        var card = CardBase.Create(slot, model);
        if (card == null) Cards.Remove(slot);
        else Cards[slot] = card;
    }

    // Zgodność wstecz z wcześniejszym API.
    public void AddCard34901A(int slot)
    {
        Cards[slot] = new Card34901A(slot);
        RaiseStatus($"Skonfigurowano kartę 34901A w slocie {slot}");
    }

    public void AddCard34907A(int slot)
    {
        Cards[slot] = new Card34907A(slot);
        RaiseStatus($"Skonfigurowano kartę 34907A w slocie {slot}");
    }

    /// <summary>
    /// Wykrywa karty zainstalowane w slotach przy pomocy SYSTem:CTYPe?.
    /// Aktualizuje <see cref="Cards"/> i zwraca mapę slot → numer modelu
    /// (pusty string dla pustego slotu).
    /// </summary>
    public async Task<IReadOnlyDictionary<int, string>> DetectCardsAsync()
    {
        var detected = new Dictionary<int, string>();
        Cards.Clear();

        foreach (int slot in Slots)
        {
            string model;
            try
            {
                // Odpowiedź: "<firma>,<model>,<nr seryjny>,<wersja>", np.
                // "Agilent Technologies,34901A,0,1.0"; pusty slot → model "0".
                string resp = await Query($"SYST:CTYP? {slot}");
                model = ParseModelFromCtype(resp);
            }
            catch (Exception ex)
            {
                detected[slot] = "";
                RaiseStatus($"Slot {slot}: błąd odczytu typu karty ({ex.Message})");
                continue;
            }

            detected[slot] = model;
            var card = CardBase.Create(slot, model);
            if (card != null)
            {
                Cards[slot] = card;
                RaiseStatus($"Slot {slot}: wykryto {card.DisplayName}");
            }
            else
            {
                RaiseStatus($"Slot {slot}: pusty");
            }
        }

        return detected;
    }

    private static string ParseModelFromCtype(string ctypResponse)
    {
        // Drugie pole = numer modelu. "0" oznacza pusty slot.
        var parts = ctypResponse.Split(',');
        if (parts.Length < 2) return "";
        string model = parts[1].Trim();
        return model is "0" or "" ? "" : model;
    }

    // ── Skanowanie mieszane (per-kanał) ─────────────────────────────────────────

    /// <summary>
    /// Skanuje zbiór kanałów, gdzie KAŻDY kanał ma własną funkcję pomiarową.
    /// Pozwala to zmierzyć w jednym przebiegu np. 2× napięcie i 1× RTD 4W.
    /// Sekwencja: CONF per kanał → NPLC/skalowanie per kanał → ustawienia skanu
    /// (wyzwalanie, timer, liczba przebiegów, opóźnienie) → ROUT:SCAN → READ?.
    /// </summary>
    public async Task<List<MeasurementResult>> ScanAsync(
        IReadOnlyList<ChannelMeasurement> channels, ScanSettings? settings = null)
    {
        if (channels.Count == 0) return new List<MeasurementResult>();
        settings ??= new ScanSettings();

        // 1. Walidacja względem fizycznej karty w danym slocie (jeśli znana).
        foreach (var ch in channels)
        {
            int slot = SlotOf(ch.Channel);
            if (Cards.TryGetValue(slot, out var card) && card is Card34901A mux)
                mux.Validate(ch);
        }

        // 1b. Pomiar 4-przewodowy (FRES / RTD 4W) rezerwuje kanał źródłowy n ORAZ parę n+10.
        //     Para nie może być osobno skonfigurowana na inny pomiar.
        var present = new HashSet<int>(channels.Select(c => c.Channel));
        foreach (var ch in channels)
        {
            if (ch.Function is not (MuxFunction.OHM4W or MuxFunction.TEMP_RTD4W)) continue;
            int paired = ch.Channel + 10;
            if (present.Contains(paired))
                throw new InvalidOperationException(
                    $"Kanał {paired} jest zarezerwowany jako para 4-przewodowa kanału {ch.Channel} " +
                    $"(pomiar {ch.Function}) i nie może być osobno konfigurowany w tym skanie.");
        }

        // 2. Konfiguracja każdego kanału osobno: funkcja, NPLC, skalowanie Mx+B.
        foreach (var ch in channels)
        {
            await Write(ch.BuildConfCommand());
            string? nplc = ch.BuildNplcCommand();
            if (nplc != null) await Write(nplc);
            foreach (var scaleCmd in ch.BuildScalingCommands())
                await Write(scaleCmd);
        }

        // 3. Lista skanu = wszystkie kanały w zadanej kolejności.
        string list = string.Join(",", channels.Select(c => c.Channel));
        await Write($"ROUT:SCAN (@{list})");

        // 4. Opóźnienie kanału (jeśli zadane).
        if (settings.ChannelDelay >= 0)
            await Write($"ROUT:CHAN:DEL {settings.ChannelDelay.ToString(CultureInfo.InvariantCulture)},(@{list})");

        // 5. Wyzwalanie / odstęp / liczba przebiegów.
        await Write($"TRIG:SOUR {settings.ScpiTriggerSource}");
        await Write($"TRIG:COUN {Math.Max(1, settings.ScanCount)}");
        if (settings.IsTimer)
            await Write($"TRIG:TIM {settings.TimerInterval.ToString(CultureInfo.InvariantCulture)}");

        // 6. READ? inicjuje przebieg(i) i blokuje do zakończenia skanu.
        int sweeps = Math.Max(1, settings.ScanCount);
        int timeoutMs = Math.Max(8000, channels.Count * sweeps * 1500);
        string response = await QueryWithTimeout("READ?", timeoutMs);

        // 7. Parsowanie — wartości w kolejności listy skanu (round-robin przy wielu przebiegach).
        var values = ParseDoubleArray(response);
        var results = new List<MeasurementResult>();
        for (int i = 0; i < values.Count; i++)
        {
            var ch = channels[i % channels.Count];
            var r = new MeasurementResult
            {
                InstrumentName = DriverName,
                ChannelId = ch.Channel.ToString(),
                Function = ch.Function.ToString(),
                Value = values[i],
                Unit = ch.Unit,
                IsValid = !double.IsNaN(values[i])
            };
            results.Add(r);
            RaiseMeasurement(r);
        }

        return results;
    }

    /// <summary>
    /// Skan z jedną wspólną funkcją dla całej listy kanałów (np. "101,102,201:205").
    /// </summary>
    public Task<List<MeasurementResult>> ScanUniformAsync(
        string channelSpec, MuxFunction function, string range = "AUTO", TcType tcType = TcType.K)
    {
        var channels = ExpandToInts(channelSpec)
            .Select(ch => new ChannelMeasurement(ch, function, range) { TcType = tcType })
            .ToList();
        return ScanAsync(channels);
    }

    /// <summary>Szybki skan napięcia DC po liście kanałów SCPI (np. "101,102,201:205").</summary>
    public Task<List<MeasurementResult>> ScanChannelListAsync(string scpiChannelList) =>
        ScanUniformAsync(scpiChannelList, MuxFunction.VDC);

    /// <summary>
    /// Skan kanałów w jednym slocie z jedną funkcją (kanały 1-bazowe).
    /// </summary>
    public Task<List<MeasurementResult>> ScanAsync(
        int slot, IEnumerable<int> channels, MuxFunction function, string range = "AUTO")
    {
        var list = channels
            .Select(ch => new ChannelMeasurement(slot + ch, function, range))
            .ToList();
        return ScanAsync(list);
    }

    /// <summary>Pomiar temperatury termoparą dla listy kanałów.</summary>
    public Task<List<MeasurementResult>> MeasureTemperatureTCAsync(string channelList, string tcType = "K")
    {
        var tc = Enum.TryParse<TcType>(tcType, ignoreCase: true, out var t) ? t : TcType.K;
        return ScanUniformAsync(channelList, MuxFunction.TEMP_TC, "AUTO", tc);
    }

    /// <summary>
    /// Parsuje mieszaną specyfikację kanałów, gdzie każdy kanał ma własną funkcję.
    /// Format wpisów (rozdzielane ';' lub nową linią): <c>kanał=FUNK[:param][@zakres]</c>
    /// np. <c>101=VDC; 102=VDC@10; 103=RTD4W:85; 104=TC:K</c>.
    /// </summary>
    public List<ChannelMeasurement> ParseChannelSpec(string spec)
    {
        var list = new List<ChannelMeasurement>();
        if (string.IsNullOrWhiteSpace(spec)) return list;

        foreach (var raw in spec.Split(new[] { ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var entry = raw.Trim();
            if (entry.Length == 0) continue;

            string range = "AUTO";
            int at = entry.IndexOf('@');
            if (at >= 0)
            {
                range = entry[(at + 1)..].Trim();
                entry = entry[..at].Trim();
            }

            var eq = entry.Split('=', 2);
            if (eq.Length != 2 || !int.TryParse(eq[0].Trim(), out int channel)) continue;

            var fp = eq[1].Split(':', 2);
            string func = fp[0].Trim();
            string param = fp.Length > 1 ? fp[1].Trim() : "";

            list.Add(ChannelMeasurement.FromUi(channel, func, range, param));
        }

        return list;
    }

    /// <summary>Skan na podstawie mieszanej specyfikacji (patrz <see cref="ParseChannelSpec"/>).</summary>
    public Task<List<MeasurementResult>> ScanSpecAsync(string spec) => ScanAsync(ParseChannelSpec(spec));

    // ── 34907A: DAC ─────────────────────────────────────────────────────────────

    public async Task SetDacAsync(int slot, int dacChannel, double voltage)
    {
        var card = GetCard34907A(slot);
        await Write(card.BuildSetDacCommand(dacChannel, voltage));
        await ThrowOnDeviceError($"DAC{dacChannel} (slot {slot})");
        RaiseStatus($"DAC{dacChannel} slot {slot}: {voltage:F4} V (kanał {card.DacChannel(dacChannel)})");
    }

    public async Task<double> ReadDacAsync(int slot, int dacChannel)
    {
        var card = GetCard34907A(slot);
        return await QueryDouble(card.BuildReadDacCommand(dacChannel));
    }

    // ── 34907A: Digital I/O ─────────────────────────────────────────────────────

    public Task SetDigitalOutputAsync(int slot, byte value) => SetDigitalOutputAsync(slot, 1, value);

    public async Task SetDigitalOutputAsync(int slot, int port, byte value)
    {
        var card = GetCard34907A(slot);
        await Write(card.BuildDigitalWriteCommand(port, value));
        await ThrowOnDeviceError($"Digital OUT port {port} (slot {slot})");
        RaiseStatus($"Digital OUT slot {slot} port {port}: 0x{value:X2}");
    }

    public Task<byte> ReadDigitalInputAsync(int slot) => ReadDigitalInputAsync(slot, 1);

    public async Task<byte> ReadDigitalInputAsync(int slot, int port)
    {
        var card = GetCard34907A(slot);
        string response = await Query(card.BuildDigitalReadCommand(port));
        if (byte.TryParse(response.Trim(), out byte result))
            return result;
        if (double.TryParse(response.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
            return (byte)(int)d;
        throw new InvalidOperationException($"Nieprawidłowa odpowiedź na Digital READ: '{response}'");
    }

    // ── 34907A: Totalizer (kanał s03) ───────────────────────────────────────────

    public async Task<double> ReadTotalizerAsync(int slot)
    {
        var card = GetCard34907A(slot);
        string response = await Query(card.BuildTotalizerReadCommand());
        return double.TryParse(response.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double d)
            ? d : double.NaN;
    }

    public async Task ResetTotalizerAsync(int slot)
    {
        var card = GetCard34907A(slot);
        await Write(card.BuildTotalizerResetCommand());
        RaiseStatus($"Totalizer slot {slot} — reset (kanał {card.TotalizerChannel})");
    }

    // ── Diagnostyka błędów SCPI ─────────────────────────────────────────────────

    /// <summary>Odczyt kolejki błędów: SYSTem:ERRor? → "&lt;kod&gt;,\"&lt;opis&gt;\"".</summary>
    public Task<string> ReadDeviceErrorAsync() => Query("SYST:ERR?");

    /// <summary>Rzuca wyjątek, jeśli przyrząd zgłosił błąd (kod != 0).</summary>
    private async Task ThrowOnDeviceError(string context)
    {
        string err = (await Query("SYST:ERR?")).Trim();
        var parts = err.Split(',', 2);
        if (parts.Length >= 1 && int.TryParse(parts[0].Trim(), out int code) && code != 0)
            throw new InvalidOperationException($"{context}: przyrząd zgłosił błąd {err}");
    }

    // ── Plugin integration ──────────────────────────────────────────────────────

    public override FrameworkElement CreateFrontPanel() =>
        new Agilent34970AFrontPanelView(this);

    public override IEnumerable<ISequenceBlock> GetAvailableBlocks() =>
        Agilent34970ABlocks.CreateAllBlocks();

    static Agilent34970ADriver()
    {
        Agilent34970ABlocks.RegisterAll();
    }

    // ── Private helpers ─────────────────────────────────────────────────────────

    private async Task<string> QueryWithTimeout(string command, int timeoutMs)
    {
        if (Connection == null) throw new InvalidOperationException("Brak połączenia");
        return await Connection.QueryAsync(command, timeoutMs);
    }

    private Card34907A GetCard34907A(int slot)
    {
        if (!Cards.TryGetValue(slot, out var cardObj) || cardObj is not Card34907A card)
            throw new InvalidOperationException(
                $"Slot {slot} nie zawiera karty 34907A. Najpierw wykryj karty lub dodaj 34907A.");
        return card;
    }

    /// <summary>Slot kanału bezwzględnego: 101 → 100, 215 → 200.</summary>
    private static int SlotOf(int absChannel) => absChannel / 100 * 100;

    /// <summary>Rozwija listę kanałów z zakresami (np. "101,201:205") do listy int.</summary>
    private static List<int> ExpandToInts(string input)
    {
        var result = new List<int>();
        foreach (var token in input.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = token.Trim();
            if (trimmed.Contains(':'))
            {
                var parts = trimmed.Split(':');
                if (parts.Length == 2
                    && int.TryParse(parts[0], out int start)
                    && int.TryParse(parts[1], out int end))
                {
                    for (int ch = start; ch <= end; ch++)
                        result.Add(ch);
                }
            }
            else if (int.TryParse(trimmed, out int ch))
            {
                result.Add(ch);
            }
        }
        return result;
    }

    private static List<double> ParseDoubleArray(string response)
    {
        var result = new List<double>();
        foreach (var token in response.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            result.Add(double.TryParse(
                token.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double d)
                ? d : double.NaN);
        }
        return result;
    }
}

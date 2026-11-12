namespace PadPilot.Core;

/// <summary>
/// DualSense adaptif tetik efekt paketleri (11 bayt). Kodlama: Nielk1'in TriggerEffectGenerator belgesi
/// (Sony'nin resmi efektleri). Konumlar 0-9 arasi bolgelerdir (0 = tetik serbest, 9 = sona yakin).
/// </summary>
public static class TriggerEffects
{
    public const int Size = 11;

    public static byte[] Build(TriggerSettings t)
    {
        int start = Math.Clamp(t.Start, 0, 9), end = Math.Clamp(t.End, 0, 9), str = Math.Clamp(t.Strength, 1, 8);
        return t.Effect switch
        {
            TriggerEffectKind.Resistance => Feedback(start, str),
            TriggerEffectKind.Weapon => Weapon(Math.Clamp(start, 2, 7), Math.Clamp(Math.Max(end, start + 1), 3, 8), str),
            TriggerEffectKind.Bow => Bow(Math.Min(start, 8), Math.Clamp(Math.Max(end, start + 1), 1, 8), str, Math.Clamp(t.Snap, 1, 8)),
            TriggerEffectKind.Vibration => Vibration(start, str, t.Frequency),
            TriggerEffectKind.Galloping => Galloping(Math.Min(start, 8), Math.Clamp(Math.Max(end, start + 1), 1, 9), 2, 5, t.Frequency),
            TriggerEffectKind.Machine => Machine(Math.Min(start, 8), Math.Clamp(Math.Max(end, start + 1), 1, 9), str - 1, Math.Clamp(t.Snap, 1, 8) - 1, t.Frequency, 3),
            TriggerEffectKind.Slope => Slope(Math.Min(start, 8), Math.Clamp(Math.Max(end, start + 1), 1, 9), 1, str),
            _ => Off(),
        };
    }

    public static byte[] Off() { var d = new byte[Size]; d[0] = 0x05; return d; }

    /// <summary>Konumdan itibaren sabit direnc.</summary>
    public static byte[] Feedback(int position, int strength)
    {
        var s = new int[10];
        for (int i = position; i < 10; i++) s[i] = strength;
        return MultiFeedback(s);
    }

    /// <summary>Her bolge icin ayri direnc (0 = direnc yok, 1-8).</summary>
    public static byte[] MultiFeedback(ReadOnlySpan<int> strengths)
    {
        uint active = 0; ulong forces = 0;
        for (int i = 0; i < 10; i++)
        {
            if (strengths[i] <= 0) continue;
            forces |= (ulong)((strengths[i] - 1) & 7) << (3 * i);
            active |= 1u << i;
        }
        if (active == 0) return Off();
        var d = new byte[Size];
        d[0] = 0x21; d[1] = (byte)active; d[2] = (byte)(active >> 8);
        for (int i = 0; i < 4; i++) d[3 + i] = (byte)(forces >> (8 * i));
        return d;
    }

    /// <summary>Baslangictan bitise dogrusal artan direnc.</summary>
    public static byte[] Slope(int start, int end, int startStrength, int endStrength)
    {
        var s = new int[10];
        for (int i = start; i < 10; i++)
        {
            double k = i >= end ? 1 : (double)(i - start) / Math.Max(1, end - start);
            s[i] = (int)Math.Round(startStrength + (endStrength - startStrength) * k);
        }
        return MultiFeedback(s);
    }

    /// <summary>Silah tetigi: baslangicta direnc, bitiste "tik" diye kirilir.</summary>
    public static byte[] Weapon(int start, int end, int strength)
    {
        uint zones = (1u << start) | (1u << end);
        var d = new byte[Size];
        d[0] = 0x25; d[1] = (byte)zones; d[2] = (byte)(zones >> 8); d[3] = (byte)((strength - 1) & 7);
        return d;
    }

    /// <summary>Yay: gerildikce direnc artar, birakinca geri teper.</summary>
    public static byte[] Bow(int start, int end, int strength, int snap)
    {
        uint zones = (1u << start) | (1u << end);
        uint pair = (uint)(((strength - 1) & 7) | (((snap - 1) & 7) << 3));
        var d = new byte[Size];
        d[0] = 0x22; d[1] = (byte)zones; d[2] = (byte)(zones >> 8); d[3] = (byte)pair; d[4] = (byte)(pair >> 8);
        return d;
    }

    /// <summary>Konumdan itibaren titresim (otomatik silah hissi).</summary>
    public static byte[] Vibration(int position, int amplitude, int frequency)
    {
        uint active = 0; ulong amps = 0;
        for (int i = position; i < 10; i++)
        {
            amps |= (ulong)((amplitude - 1) & 7) << (3 * i);
            active |= 1u << i;
        }
        var d = new byte[Size];
        d[0] = 0x26; d[1] = (byte)active; d[2] = (byte)(active >> 8);
        for (int i = 0; i < 4; i++) d[3 + i] = (byte)(amps >> (8 * i));
        d[9] = (byte)Math.Clamp(frequency, 1, 255);
        return d;
    }

    /// <summary>Dortnala: iki darbeli ritmik geri tepme.</summary>
    public static byte[] Galloping(int start, int end, int firstFoot, int secondFoot, int frequency)
    {
        uint zones = (1u << start) | (1u << end);
        uint ratio = (uint)((secondFoot & 7) | ((firstFoot & 7) << 3));
        var d = new byte[Size];
        d[0] = 0x23; d[1] = (byte)zones; d[2] = (byte)(zones >> 8); d[3] = (byte)ratio; d[4] = (byte)Math.Clamp(frequency, 1, 255);
        return d;
    }

    /// <summary>Makine: iki genlik arasinda gidip gelen titresim.</summary>
    public static byte[] Machine(int start, int end, int strengthA, int strengthB, int frequency, int period)
    {
        uint zones = (1u << start) | (1u << end);
        uint pair = (uint)((strengthA & 7) | ((strengthB & 7) << 3));
        var d = new byte[Size];
        d[0] = 0x27; d[1] = (byte)zones; d[2] = (byte)(zones >> 8); d[3] = (byte)pair;
        d[4] = (byte)Math.Clamp(frequency, 1, 255); d[5] = (byte)period;
        return d;
    }
}

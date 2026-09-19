using System;
using System.Buffers.Binary;

namespace CardVault.Security;

/// <summary>
/// Salsa20/20 stream cipher (256-bit key, 64-bit nonce) — the inner stream
/// cipher used by KeePass KDBX 3.1 databases to mask protected XML values.
/// Not available in the .NET crypto stack, so implemented directly from the
/// eSTREAM specification and validated against the standard test vectors.
/// </summary>
public sealed class Salsa20
{
    private static readonly uint[] Sigma = { 0x61707865u, 0x3320646eu, 0x79622d32u, 0x6b206574u };

    private readonly uint[] _state = new uint[16];
    private readonly byte[] _block = new byte[64];
    private ulong _counter;
    private int _pos = 64;

    public Salsa20(ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce)
    {
        if (key.Length != 32) throw new ArgumentException("Salsa20 requires a 32-byte key.", nameof(key));
        if (nonce.Length != 8) throw new ArgumentException("Salsa20 requires an 8-byte nonce.", nameof(nonce));

        _state[0] = Sigma[0];
        _state[5] = Sigma[1];
        _state[10] = Sigma[2];
        _state[15] = Sigma[3];
        for (var i = 0; i < 4; i++)
            _state[1 + i] = BinaryPrimitives.ReadUInt32LittleEndian(key.Slice(i * 4, 4));
        for (var i = 0; i < 4; i++)
            _state[11 + i] = BinaryPrimitives.ReadUInt32LittleEndian(key.Slice((4 + i) * 4, 4));
        _state[6] = BinaryPrimitives.ReadUInt32LittleEndian(nonce[..4]);
        _state[7] = BinaryPrimitives.ReadUInt32LittleEndian(nonce[4..]);
        _state[8] = 0;
        _state[9] = 0;
    }

    /// <summary>XOR <paramref name="input"/> with the keystream into <paramref name="output"/> (encrypt == decrypt).</summary>
    public void Transcode(ReadOnlySpan<byte> input, Span<byte> output)
    {
        if (output.Length < input.Length) throw new ArgumentException("Output is shorter than input.");
        for (var i = 0; i < input.Length; i++)
        {
            if (_pos == 64)
            {
                NextBlock();
                _pos = 0;
            }
            output[i] = (byte)(input[i] ^ _block[_pos++]);
        }
    }

    private void NextBlock()
    {
        var x = (uint[])_state.Clone();
        x[8] = (uint)_counter;
        x[9] = (uint)(_counter >> 32);

        for (var r = 0; r < 10; r++)
        {
            ColumnRound(x);
            RowRound(x);
        }

        for (var i = 0; i < 16; i++)
            x[i] += _state[i];

        for (var i = 0; i < 16; i++)
            BinaryPrimitives.WriteUInt32LittleEndian(_block.AsSpan(i * 4, 4), x[i]);

        _counter++;
    }

    private static void ColumnRound(uint[] x)
    {
        QuarterRound(ref x[0], ref x[4], ref x[8], ref x[12]);
        QuarterRound(ref x[5], ref x[9], ref x[13], ref x[1]);
        QuarterRound(ref x[10], ref x[14], ref x[2], ref x[6]);
        QuarterRound(ref x[15], ref x[3], ref x[7], ref x[11]);
    }

    private static void RowRound(uint[] x)
    {
        QuarterRound(ref x[0], ref x[1], ref x[2], ref x[3]);
        QuarterRound(ref x[5], ref x[6], ref x[7], ref x[4]);
        QuarterRound(ref x[10], ref x[11], ref x[8], ref x[9]);
        QuarterRound(ref x[15], ref x[12], ref x[13], ref x[14]);
    }

    private static void QuarterRound(ref uint y0, ref uint y1, ref uint y2, ref uint y3)
    {
        y1 ^= Rotl(y0 + y3, 7);
        y2 ^= Rotl(y1 + y0, 9);
        y3 ^= Rotl(y2 + y1, 13);
        y0 ^= Rotl(y3 + y2, 18);
    }

    private static uint Rotl(uint v, int c) => (v << c) | (v >> (32 - c));
}
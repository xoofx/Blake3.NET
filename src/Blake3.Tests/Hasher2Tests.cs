using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using NUnit.Framework;

namespace Blake3.Tests;

/// <summary>
/// Differential and boundary tests for <see cref="Hasher2"/>.
/// </summary>
public class Hasher2Tests
{
    private const string VectorContext = "BLAKE3 2019-12-27 16:29:52 test vectors context";
    private static readonly byte[] VectorKey = Encoding.ASCII.GetBytes("whats the Elvish word for friend");

    private static readonly int[] BoundaryLengths =
    [
        0, 1, 2, 3, 4, 5, 7, 8, 15, 16, 31, 32, 63, 64, 65,
        127, 128, 129, 255, 512, 1023, 1024, 1025, 2048, 2049,
        3072, 3073, 4096, 4097, 8192, 8193, 16384, 31744, 102400,
    ];

    [Test]
    public void OfficialEmptyInputVectorMatches()
    {
        const string expected =
            "af1349b9f5f9a1a6a0404dea36dcc9499bcb25c9adc112b7cc9a93cae41f3262" +
            "e00f03e7b69af26b7faaf09fcd333050338ddfe085b8cc869ca98b206c08243a" +
            "26f5487789e8f660afe6c99ef9e0c52b92e7393024a80459cf91f476f9ffdbd" +
            "a7001c22e159b402631f277ca96f2defdf1078282314e763699a31c5363165421" +
            "cce14d";
        var output = new byte[131];

        Hasher2.Hash(ReadOnlySpan<byte>.Empty, output);

        Assert.That(Convert.ToHexStringLower(output), Is.EqualTo(expected));
    }

    [TestCaseSource(nameof(BoundaryLengths))]
    public void RegularHashMatchesNativeAtBoundaries(int length)
    {
        var input = CreateVectorInput(length);
        var nativeOutput = new byte[131];
        var managedOutput = new byte[131];

        Hasher.Hash(input, nativeOutput);
        Hasher2.Hash(input, managedOutput);

        Assert.That(managedOutput, Is.EqualTo(nativeOutput));
        Assert.That(Hasher2.Hash(input), Is.EqualTo(Hasher.Hash(input)));
    }

    [TestCaseSource(nameof(BoundaryLengths))]
    public void FragmentedUpdatesMatchNative(int length)
    {
        var input = CreateVectorInput(length);
        using var managed = Hasher2.New();
        var position = 0;
        var nextWrite = 1;
        while (position < input.Length)
        {
            var take = Math.Min(nextWrite, input.Length - position);
            managed.Update(input.AsSpan(position, take));
            position += take;
            nextWrite = (nextWrite * 3) % 1600 + 1;
        }

        Assert.That(managed.Finalize(), Is.EqualTo(Hasher.Hash(input)));

        var first = managed.Finalize();
        managed.Update(new byte[] { 0xA5 });
        var extendedInput = input.Append((byte)0xA5).ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(Hasher2.Hash(extendedInput), Is.EqualTo(Hasher.Hash(extendedInput)), "One-shot extended input must match");
            Assert.That(first, Is.EqualTo(Hasher.Hash(input)), "Finalization must be idempotent");
            Assert.That(managed.Finalize(), Is.EqualTo(Hasher.Hash(extendedInput)), "Updates after finalization must be accepted");
        });
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(64)]
    [TestCase(1024)]
    [TestCase(1025)]
    [TestCase(16384)]
    public void KeyedAndDeriveKeyModesMatchNative(int length)
    {
        var input = CreateVectorInput(length);

        using var nativeKeyed = Hasher.NewKeyed(VectorKey);
        using var managedKeyed = Hasher2.NewKeyed(VectorKey);
        nativeKeyed.Update(input);
        managedKeyed.Update(input);

        using var nativeDerived = Hasher.NewDeriveKey(VectorContext);
        using var managedDerived = Hasher2.NewDeriveKey(VectorContext);
        nativeDerived.Update(input);
        managedDerived.Update(input);

        Assert.Multiple(() =>
        {
            Assert.That(managedKeyed.Finalize(), Is.EqualTo(nativeKeyed.Finalize()));
            Assert.That(managedDerived.Finalize(), Is.EqualTo(nativeDerived.Finalize()));
        });
    }

    [Test]
    public void XofSeekResetJoinAndGenericUpdatesMatchNative()
    {
        var input = CreateVectorInput(4097);
        using var native = Hasher.New();
        using var managed = Hasher2.New();
        native.UpdateWithJoin(input.AsSpan(0, 1000));
        managed.UpdateWithJoin(input.AsSpan(0, 1000));
        native.Update(input.AsSpan(1000));
        managed.Update(input.AsSpan(1000));

        foreach (var (offset, length) in new[] { (0L, 0), (0L, 1), (1L, 63), (63L, 131), (64L, 128), (1025L, 257) })
        {
            var nativeOutput = new byte[length];
            var managedOutput = new byte[length];
            native.Finalize(offset, nativeOutput);
            managed.Finalize(offset, managedOutput);
            Assert.That(managedOutput, Is.EqualTo(nativeOutput), $"offset={offset}, length={length}");
        }

        native.Reset();
        managed.Reset();
        var values = new uint[] { 0, 1, 0x12345678, uint.MaxValue };
        native.Update<uint>(values);
        managed.Update<uint>(values);
        Assert.That(managed.Finalize(), Is.EqualTo(native.Finalize()));
        Assert.That(Hasher2.Hash(MemoryMarshal.AsBytes(values.AsSpan())), Is.EqualTo(managed.Finalize()));
    }

    [Test]
    public void DeriveKeyByteContextUsesNativeLossyUtf8Semantics()
    {
        byte[] invalidUtf8 = [0x66, 0x80, 0x80, 0x6F];
        using var native = Hasher.NewDeriveKey(invalidUtf8);
        using var managed = Hasher2.NewDeriveKey(invalidUtf8);

        Assert.That(managed.Finalize(), Is.EqualTo(native.Finalize()));
    }

    [Test]
    public void DisposedHasherRejectsOperations()
    {
        var hasher = Hasher2.New();
        hasher.Dispose();

        Assert.Multiple(() =>
        {
            Assert.Throws<ObjectDisposedException>(() => hasher.Update([1]));
            Assert.Throws<ObjectDisposedException>(() => hasher.Finalize());
            Assert.Throws<ObjectDisposedException>(() => hasher.Reset());
        });
    }

    [Test]
    public void KeyMustBeExactly32Bytes()
    {
        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Hasher2.NewKeyed(new byte[31]));
            Assert.Throws<ArgumentOutOfRangeException>(() => Hasher2.NewKeyed(new byte[33]));
        });
    }

    private static byte[] CreateVectorInput(int length) => Enumerable.Range(0, length).Select(index => (byte)(index % 251)).ToArray();
}

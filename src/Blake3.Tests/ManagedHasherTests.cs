extern alias ManagedBlake3;

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using ManagedHash = ManagedBlake3::Blake3.Hash;
using ManagedHasher = ManagedBlake3::Blake3.Hasher;
using NUnit.Framework;

namespace Blake3.Tests;

/// <summary>
/// Differential and boundary tests for <see cref="ManagedHasher"/>.
/// </summary>
public class ManagedHasherTests
{
    private const string VectorContext = "BLAKE3 2019-12-27 16:29:52 test vectors context";
    private static readonly byte[] VectorKey = Encoding.ASCII.GetBytes("whats the Elvish word for friend");

    private static readonly int[] BoundaryLengths =
    [
        0, 1, 2, 3, 4, 5, 6, 7, 8, 15, 16, 31, 32, 63, 64, 65, 100,
        127, 128, 129, 255, 512, 960, 1000, 1023, 1024, 1025, 2048, 2049,
        3072, 3073, 4096, 4097, 5120, 5121, 6144, 6145, 7168, 7169, 8192,
        8193, 16383, 16384, 16385, 31743, 31744, 31745, 102400,
    ];

    // Mirrors the upstream BLAKE3 test_compare_update_multiple cases through four chunks.
    private static readonly int[] IncrementalUpdateLengths =
    [
        0, 1, 2, 3, 4, 5, 6, 7, 8, 63, 64, 65, 127, 128, 129,
        1023, 1024, 1025, 2048, 2049, 3072, 3073, 4096,
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

        ManagedHasher.Hash(ReadOnlySpan<byte>.Empty, output);

        Assert.That(Convert.ToHexStringLower(output), Is.EqualTo(expected));
    }

    [Test]
    public void AllOfficialVectorsMatch()
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "test_vectors.json")));
        var root = document.RootElement;
        var key = Encoding.ASCII.GetBytes(root.GetProperty("key").GetString()!);
        var context = root.GetProperty("context_string").GetString()!;

        foreach (var testCase in root.GetProperty("cases").EnumerateArray())
        {
            var inputLength = testCase.GetProperty("input_len").GetInt32();
            var input = CreateVectorInput(inputLength);
            var expectedHash = Convert.FromHexString(testCase.GetProperty("hash").GetString()!);
            var expectedKeyedHash = Convert.FromHexString(testCase.GetProperty("keyed_hash").GetString()!);
            var expectedDeriveKey = Convert.FromHexString(testCase.GetProperty("derive_key").GetString()!);
            var actualHash = new byte[expectedHash.Length];
            var actualKeyedHash = new byte[expectedKeyedHash.Length];
            var actualDeriveKey = new byte[expectedDeriveKey.Length];

            ManagedHasher.Hash(input, actualHash);
            using (var keyedHasher = ManagedHasher.NewKeyed(key))
            {
                keyedHasher.Update(input);
                keyedHasher.Finalize(actualKeyedHash);
            }

            using (var deriveHasher = ManagedHasher.NewDeriveKey(context))
            {
                deriveHasher.Update(input);
                deriveHasher.Finalize(actualDeriveKey);
            }

            Assert.Multiple(() =>
            {
                Assert.That(actualHash, Is.EqualTo(expectedHash), $"regular input_len={inputLength}");
                Assert.That(actualKeyedHash, Is.EqualTo(expectedKeyedHash), $"keyed input_len={inputLength}");
                Assert.That(actualDeriveKey, Is.EqualTo(expectedDeriveKey), $"derive input_len={inputLength}");
                Assert.That(ManagedHasher.Hash(input).AsSpan().SequenceEqual(expectedHash.AsSpan(0, Hash.Size)), Is.True,
                    $"default output input_len={inputLength}");
            });
        }
    }

    [TestCaseSource(nameof(BoundaryLengths))]
    public void RegularHashMatchesNativeAtBoundaries(int length)
    {
        var input = CreateVectorInput(length);
        var nativeOutput = new byte[131];
        var managedOutput = new byte[131];

        Hasher.Hash(input, nativeOutput);
        ManagedHasher.Hash(input, managedOutput);

        Assert.That(managedOutput, Is.EqualTo(nativeOutput));
        AssertManagedHashEqualsNative(ManagedHasher.Hash(input), Hasher.Hash(input));
    }

    [TestCase(32767)]
    [TestCase(32768)]
    [TestCase(32769)]
    [TestCase(65535)]
    [TestCase(65536)]
    [TestCase(65537)]
    [TestCase(1000000)]
    public void OneShotWideSubtreesMatchNative(int length)
    {
        var input = CreateVectorInput(length);
        var nativeOutput = new byte[131];
        var managedOutput = new byte[131];

        Hasher.Hash(input, nativeOutput);
        ManagedHasher.Hash(input, managedOutput);

        Assert.Multiple(() =>
        {
            Assert.That(managedOutput, Is.EqualTo(nativeOutput));
            AssertManagedHashEqualsNative(ManagedHasher.Hash(input), Hasher.Hash(input));
        });
    }

    [TestCaseSource(nameof(BoundaryLengths))]
    public void FragmentedUpdatesMatchNative(int length)
    {
        var input = CreateVectorInput(length);
        using var managed = ManagedHasher.New();
        var position = 0;
        var nextWrite = 1;
        while (position < input.Length)
        {
            var take = Math.Min(nextWrite, input.Length - position);
            managed.Update(input.AsSpan(position, take));
            position += take;
            nextWrite = (nextWrite * 3) % 1600 + 1;
        }

        AssertManagedHashEqualsNative(managed.Finalize(), Hasher.Hash(input));

        var first = managed.Finalize();
        managed.Update(new byte[] { 0xA5 });
        var extendedInput = input.Append((byte)0xA5).ToArray();
        Assert.Multiple(() =>
        {
            AssertManagedHashEqualsNative(ManagedHasher.Hash(extendedInput), Hasher.Hash(extendedInput), "One-shot extended input must match");
            AssertManagedHashEqualsNative(first, Hasher.Hash(input), "Finalization must be idempotent");
            AssertManagedHashEqualsNative(managed.Finalize(), Hasher.Hash(extendedInput), "Updates after finalization must be accepted");
        });
    }

    [Test]
    public void EveryPairOfBoundaryUpdatesMatchesNative()
    {
        const int OutputLength = 303;
        foreach (var firstLength in IncrementalUpdateLengths)
        {
            foreach (var secondLength in IncrementalUpdateLengths)
            {
                var input = CreateVectorInput(firstLength + secondLength);
                var expected = new byte[OutputLength];
                var actual = new byte[OutputLength];
                Hasher.Hash(input, expected);

                using var managed = ManagedHasher.New();
                managed.Update(input.AsSpan(0, firstLength));
                managed.Update(input.AsSpan(firstLength, secondLength));
                managed.Finalize(actual);

                Assert.That(actual, Is.EqualTo(expected),
                    $"firstLength={firstLength}, secondLength={secondLength}");
            }
        }
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
        using var managedKeyed = ManagedHasher.NewKeyed(VectorKey);
        nativeKeyed.Update(input);
        managedKeyed.Update(input);

        using var nativeDerived = Hasher.NewDeriveKey(VectorContext);
        using var managedDerived = ManagedHasher.NewDeriveKey(VectorContext);
        nativeDerived.Update(input);
        managedDerived.Update(input);

        Assert.Multiple(() =>
        {
            AssertManagedHashEqualsNative(managedKeyed.Finalize(), nativeKeyed.Finalize());
            AssertManagedHashEqualsNative(managedDerived.Finalize(), nativeDerived.Finalize());
        });
    }

    [Test]
    public void XofSeekResetJoinAndGenericUpdatesMatchNative()
    {
        var input = CreateVectorInput(4097);
        using var native = Hasher.New();
        using var managed = ManagedHasher.New();
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

        var highOffset = ((ulong)uint.MaxValue * 64) + 31;
        var nativeHighOutput = new byte[131];
        var managedHighOutput = new byte[131];
        native.Finalize(highOffset, nativeHighOutput);
        managed.Finalize(highOffset, managedHighOutput);
        Assert.That(managedHighOutput, Is.EqualTo(nativeHighOutput), $"offset={highOffset}, length=131");

        native.Reset();
        managed.Reset();
        var values = new uint[] { 0, 1, 0x12345678, uint.MaxValue };
        native.Update<uint>(values);
        managed.Update<uint>(values);
        AssertManagedHashEqualsNative(managed.Finalize(), native.Finalize());
        Assert.That(ManagedHasher.Hash(MemoryMarshal.AsBytes(values.AsSpan())), Is.EqualTo(managed.Finalize()));
    }

    [Test]
    public void ResetPreservesKeyedAndDeriveKeyModes()
    {
        var discardedInput = CreateVectorInput((3 * 1024) + 7);
        var input = CreateVectorInput(1027);

        using var nativeKeyed = Hasher.NewKeyed(VectorKey);
        using var managedKeyed = ManagedHasher.NewKeyed(VectorKey);
        managedKeyed.Update(discardedInput);
        managedKeyed.Reset();
        nativeKeyed.Update(input);
        managedKeyed.Update(input);

        using var nativeDerived = Hasher.NewDeriveKey(VectorContext);
        using var managedDerived = ManagedHasher.NewDeriveKey(VectorContext);
        managedDerived.Update(discardedInput);
        managedDerived.Reset();
        nativeDerived.Update(input);
        managedDerived.Update(input);

        Assert.Multiple(() =>
        {
            AssertManagedHashEqualsNative(managedKeyed.Finalize(), nativeKeyed.Finalize());
            AssertManagedHashEqualsNative(managedDerived.Finalize(), nativeDerived.Finalize());
        });
    }

    [TestCase(262143)]
    [TestCase(262144)]
    [TestCase(300123)]
    [TestCase(524288)]
    [TestCase(1000000)]
    [TestCase(1048576)]
    [TestCase(1048577)]
    public void ParallelUpdateMatchesOrderedUpdateAtLargeBoundaries(int length)
    {
        var input = CreateVectorInput(length);
        using var ordered = ManagedHasher.New();
        using var joined = ManagedHasher.New();
        ordered.Update(input);
        joined.UpdateWithJoin(input);

        var orderedOutput = new byte[257];
        var joinedOutput = new byte[257];
        ordered.Finalize(63, orderedOutput);
        joined.Finalize(63, joinedOutput);

        Assert.Multiple(() =>
        {
            Assert.That(joinedOutput, Is.EqualTo(orderedOutput));
            AssertManagedHashEqualsNative(joined.Finalize(), Hasher.Hash(input));
        });
    }

    [TestCase(3 * 1024)]
    [TestCase((3 * 1024) + 17)]
    public void ParallelUpdatePreservesPartialChunksTreeOrderAndLaterUpdates(int prefixLength)
    {
        const int FirstParallelLength = 900123;
        const int SecondParallelLength = 700321;
        const int TailLength = 29;
        var input = CreateVectorInput(prefixLength + FirstParallelLength + SecondParallelLength + TailLength);
        using var ordered = ManagedHasher.New();
        using var joined = ManagedHasher.New();

        ordered.Update(input);
        joined.Update(input.AsSpan(0, prefixLength));
        joined.UpdateWithJoin(input.AsSpan(prefixLength, FirstParallelLength));
        joined.UpdateWithJoin(input.AsSpan(prefixLength + FirstParallelLength, SecondParallelLength));
        joined.Update(input.AsSpan(prefixLength + FirstParallelLength + SecondParallelLength, TailLength));

        var firstOutput = joined.Finalize();
        Assert.That(firstOutput, Is.EqualTo(ordered.Finalize()));
        Assert.That(joined.Finalize(), Is.EqualTo(firstOutput), "Finalization must remain idempotent");

        byte[] suffix = [0xA5, 0x5A, 0x11, 0x22, 0x33];
        ordered.Update(suffix);
        joined.Update(suffix);
        Assert.That(joined.Finalize(), Is.EqualTo(ordered.Finalize()), "Updates after finalization must be accepted");
    }

    [Test]
    public void ParallelKeyedDerivedAndGenericUpdatesMatchOrderedUpdates()
    {
        var input = CreateVectorInput(700123);
        using var orderedKeyed = ManagedHasher.NewKeyed(VectorKey);
        using var joinedKeyed = ManagedHasher.NewKeyed(VectorKey);
        orderedKeyed.Update(input);
        joinedKeyed.UpdateWithJoin(input);

        using var orderedDerived = ManagedHasher.NewDeriveKey(VectorContext);
        using var joinedDerived = ManagedHasher.NewDeriveKey(VectorContext);
        orderedDerived.Update(input);
        joinedDerived.UpdateWithJoin(input);

        var values = Enumerable.Range(0, 100003).Select(index => unchecked((uint)(index * 2654435761u))).ToArray();
        using var orderedGeneric = ManagedHasher.New();
        using var joinedGeneric = ManagedHasher.New();
        orderedGeneric.Update<uint>(values);
        joinedGeneric.UpdateWithJoin<uint>(values);

        Assert.Multiple(() =>
        {
            Assert.That(joinedKeyed.Finalize(), Is.EqualTo(orderedKeyed.Finalize()));
            Assert.That(joinedDerived.Finalize(), Is.EqualTo(orderedDerived.Finalize()));
            Assert.That(joinedGeneric.Finalize(), Is.EqualTo(orderedGeneric.Finalize()));
        });
    }

    [Test]
    public void ParallelUpdateMatchesEveryStartingChunkAlignment()
    {
        const int ParallelLength = 300123;
        for (var prefixChunks = 0; prefixChunks < 32; prefixChunks++)
        {
            var prefixLength = (prefixChunks * 1024) + 17;
            var input = CreateVectorInput(prefixLength + ParallelLength);
            using var ordered = ManagedHasher.New();
            using var joined = ManagedHasher.New();
            ordered.Update(input);
            joined.Update(input.AsSpan(0, prefixLength));
            joined.UpdateWithJoin(input.AsSpan(prefixLength));

            Assert.That(joined.Finalize(), Is.EqualTo(ordered.Finalize()), $"Prefix chunks: {prefixChunks}");
        }
    }

    [Test]
    public void DeriveKeyByteContextUsesNativeLossyUtf8Semantics()
    {
        byte[] invalidUtf8 = [0x66, 0x80, 0x80, 0x6F];
        using var native = Hasher.NewDeriveKey(invalidUtf8);
        using var managed = ManagedHasher.NewDeriveKey(invalidUtf8);

        AssertManagedHashEqualsNative(managed.Finalize(), native.Finalize());
    }

    [TestCase("")]
    [TestCase("BLAKE3 managed context \u2026 \u2603 \u2014 \ud83d\udd11")]
    [TestCase("context with unpaired surrogate \ud800")]
    public void DeriveKeyStringContextMatchesNativeForUnicode(string context)
    {
        using var native = Hasher.NewDeriveKey(context);
        using var managed = ManagedHasher.NewDeriveKey(context);
        var input = CreateVectorInput(1025);
        native.Update(input);
        managed.Update(input);

        AssertManagedHashEqualsNative(managed.Finalize(), native.Finalize());
    }

    [Test]
    public void DisposedHasherRejectsOperations()
    {
        var hasher = ManagedHasher.New();
        hasher.Dispose();

        Assert.Multiple(() =>
        {
            Assert.Throws<ObjectDisposedException>(() => hasher.Update([1]));
            Assert.Throws<ObjectDisposedException>(() => hasher.Update<int>([1]));
            Assert.Throws<ObjectDisposedException>(() => hasher.UpdateWithJoin([1]));
            Assert.Throws<ObjectDisposedException>(() => hasher.UpdateWithJoin<int>([1]));
            Assert.Throws<ObjectDisposedException>(() => hasher.Finalize());
            Assert.Throws<ObjectDisposedException>(() => hasher.Finalize(new byte[1]));
            Assert.Throws<ObjectDisposedException>(() => hasher.Finalize(0L, new byte[1]));
            Assert.Throws<ObjectDisposedException>(() => hasher.Finalize(0UL, new byte[1]));
            Assert.Throws<ObjectDisposedException>(() => hasher.Reset());
            Assert.DoesNotThrow(hasher.Dispose);
        });
    }

    [Test]
    public void InvalidArgumentsAreRejected()
    {
        using var hasher = ManagedHasher.New();
        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ManagedHasher.NewKeyed(new byte[31]));
            Assert.Throws<ArgumentOutOfRangeException>(() => ManagedHasher.NewKeyed(new byte[33]));
            Assert.Throws<ArgumentNullException>(() => ManagedHasher.NewDeriveKey((string)null!));
            Assert.Throws<ArgumentOutOfRangeException>(() => hasher.Finalize(-1L, new byte[1]));
        });
    }

    private static void AssertManagedHashEqualsNative(ManagedHash actual, Hash expected, string message = null)
    {
        Assert.That(actual.AsSpan().SequenceEqual(expected.AsSpan()), Is.True, message);
    }

    private static byte[] CreateVectorInput(int length) => Enumerable.Range(0, length).Select(index => (byte)(index % 251)).ToArray();
}

extern alias ManagedBlake3;

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using ManagedBlake3HashAlgorithm = ManagedBlake3::Blake3.Blake3HashAlgorithm;
using ManagedBlake3Stream = ManagedBlake3::Blake3.Blake3Stream;
using ManagedHash = ManagedBlake3::Blake3.Hash;
using ManagedHasher = ManagedBlake3::Blake3.Hasher;
using NUnit.Framework;

namespace Blake3.Tests;

/// <summary>
/// Verifies the public surface and shared APIs exposed by the managed package.
/// </summary>
public class ManagedPackageTests
{
    [Test]
    public void PublicHasherApiMatchesNativePackage()
    {
        Assert.That(GetPublicMethodSignatures(typeof(ManagedHasher)),
            Is.EqualTo(GetPublicMethodSignatures(typeof(Hasher))));
        Assert.That(GetPublicConstructorSignatures(typeof(ManagedHasher)),
            Is.EqualTo(GetPublicConstructorSignatures(typeof(Hasher))));

        var constructor = typeof(ManagedHasher).GetConstructor(Type.EmptyTypes);
        var obsolete = constructor!.GetCustomAttribute<ObsoleteAttribute>();
        Assert.Multiple(() =>
        {
            Assert.That(obsolete, Is.Not.Null);
            Assert.That(obsolete!.IsError, Is.True);
        });
    }

    [Test]
    public void SharedHashAlgorithmAndStreamUseManagedHasher()
    {
        var input = Enumerable.Range(0, 4097).Select(index => (byte)(index % 251)).ToArray();
        var expected = new byte[Hash.Size];
        Hasher.Hash(input, expected);

        using var algorithm = new ManagedBlake3HashAlgorithm();
        Assert.That(algorithm.ComputeHash(input), Is.EqualTo(expected));

        using var backend = new MemoryStream();
        using var stream = new ManagedBlake3Stream(backend, dispose: false);
        stream.Write(input);
        ManagedHash streamHash = stream.ComputeHash();
        Assert.That(streamHash.AsSpan().ToArray(), Is.EqualTo(expected));
    }

    [Test]
    public void ManagedAssemblyDoesNotReferenceNativeAssembly()
    {
        var references = typeof(ManagedHasher).Assembly.GetReferencedAssemblies().Select(name => name.Name);

        Assert.That(references, Does.Not.Contain(typeof(Hasher).Assembly.GetName().Name));
        Assert.That(typeof(ManagedHash).Assembly, Is.SameAs(typeof(ManagedHasher).Assembly));
    }

    private static string[] GetPublicMethodSignatures(Type type)
    {
        return type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(method =>
            {
                var parameters = string.Join(",", method.GetParameters()
                    .Select(parameter => $"{FormatType(parameter.ParameterType)} {parameter.Name}"));
                var kind = method.IsStatic ? "static" : "instance";
                return $"{kind} {FormatType(method.ReturnType)} {method.Name}`{method.GetGenericArguments().Length}({parameters})";
            })
            .OrderBy(signature => signature, StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] GetPublicConstructorSignatures(Type type)
    {
        return type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Select(constructor => string.Join(",", constructor.GetParameters()
                .Select(parameter => $"{FormatType(parameter.ParameterType)} {parameter.Name}")))
            .OrderBy(signature => signature, StringComparer.Ordinal)
            .ToArray();
    }

    private static string FormatType(Type type)
    {
        if (type.IsGenericParameter)
        {
            return $"!{type.GenericParameterPosition}";
        }

        if (type.IsByRef)
        {
            return $"{FormatType(type.GetElementType()!)}&";
        }

        if (type.IsArray)
        {
            return $"{FormatType(type.GetElementType()!)}[]";
        }

        if (!type.IsGenericType)
        {
            return type.FullName ?? type.Name;
        }

        var definitionName = type.GetGenericTypeDefinition().FullName!;
        definitionName = definitionName[..definitionName.IndexOf('`')];
        return $"{definitionName}<{string.Join(",", type.GetGenericArguments().Select(FormatType))}>";
    }
}

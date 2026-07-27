// Vendored from LayeredCraft.SourceGeneratorTools
// Source: https://github.com/LayeredCraft/source-generator-tools/blob/main/src/LayeredCraft.SourceGeneratorTools/Types/EquatableArray/EquatableArrayExtensions.cs
// Copyright (c) 2025 LayeredCraft
// Licensed under the MIT License
//
// Vendored rather than referenced as a package dependency - see
// docs/adr/0005-generator-implementation-conventions.md and EquatableArray.cs.

namespace Compono.Generators.Types;

internal static class EquatableArrayExtensions
{
    extension<T>(IEnumerable<T> enumerable)
        where T : IEquatable<T>
    {
        public EquatableArray<T> ToEquatableArray() => new(enumerable.ToArray());
    }
}

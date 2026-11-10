namespace Compono.Generators.Tests;

internal static class ComposeDataAotStandIns
{
    public const string Source = """
        namespace Xunit
        {
            public interface ITheoryDataRow { }

            public sealed class TheoryDataRow : ITheoryDataRow
            {
                public TheoryDataRow(object?[] data) { }
            }

            public sealed class TheoryDataRow<T1> : ITheoryDataRow
            {
                public TheoryDataRow(T1 item1) { }
            }

            [System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = true)]
            public sealed class InlineDataAttribute : System.Attribute
            {
                public InlineDataAttribute(params object?[] data) { }
            }

            [System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = true)]
            public sealed class MemberDataAttribute : System.Attribute
            {
                public MemberDataAttribute(string memberName) { }
            }

            [System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = false)]
            public sealed class FactAttribute : System.Attribute
            {
                public string? Skip { get; set; }
                public string? SkipUnless { get; set; }
                public string? SkipWhen { get; set; }
            }

            [System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = false)]
            public sealed class TheoryAttribute : System.Attribute
            {
                public string? Skip { get; set; }
                public string? SkipUnless { get; set; }
                public string? SkipWhen { get; set; }
            }

            public abstract class TheoryData : System.Collections.IEnumerable
            {
                System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
                    throw new System.NotImplementedException();
            }

            public class TheoryData<T1> : TheoryData
            {
                public void Add(T1 item1) { }
            }

            public class TheoryData<T1, T2> : TheoryData
            {
                public void Add(T1 item1, T2 item2) { }
            }

            public class TheoryData<T1, T2, T3> : TheoryData
            {
                public void Add(T1 item1, T2 item2, T3 item3) { }
            }
        }

        namespace Xunit.Sdk
        {
            public sealed class DisposalTracker { }
        }

        namespace Xunit.v3
        {
            [System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = false)]
            public abstract class DataAttribute : System.Attribute
            {
                public string? Label { get; set; }
                public string? Skip { get; set; }
                public string? TestDisplayName { get; set; }
                public bool Explicit { get; set; }
                public int Timeout { get; set; }
                public bool DisableParallelization { get; set; }
                public string[]? Traits { get; set; }
                public System.Type? SkipType { get; set; }
                public string? SkipUnless { get; set; }
                public string? SkipWhen { get; set; }
            }

            public static class RegisteredEngineConfig
            {
                public static void RegisterTheoryDataRowFactory(
                    string testClassIndex,
                    string methodName,
                    bool disableDiscoveryEnumeration,
                    System.Func<Xunit.Sdk.DisposalTracker, System.Threading.Tasks.ValueTask<System.Collections.Generic.IReadOnlyCollection<Xunit.ITheoryDataRow>>> factory) { }
            }
        }

        namespace Compono.XunitV3.Aot
        {
            [System.AttributeUsage(System.AttributeTargets.Parameter, AllowMultiple = false)]
            public sealed class SharedAttribute : System.Attribute { }

            [System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = false)]
            public sealed class ComposeAttribute : Xunit.v3.DataAttribute { }

            [System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = false)]
            public sealed class ComposeAttribute<TProfile> : Xunit.v3.DataAttribute where TProfile : ICompositionProfile, new() { }

            [System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = false)]
            public sealed class ComposeAttribute<TProfile, TConfig> : Xunit.v3.DataAttribute where TProfile : ICompositionProfile
            {
                public ComposeAttribute(params object?[] configArguments) { }
            }

            [System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = false)]
            public sealed class ComposeDataAttribute : Xunit.v3.DataAttribute
            {
                public ComposeDataAttribute(string memberName) { MemberName = memberName; }

                public string MemberName { get; }

                public System.Type? MemberType { get; set; }

                public int Seed { get; set; }
            }

            [System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = false)]
            public sealed class ComposeDataAttribute<TProfile> : Xunit.v3.DataAttribute where TProfile : ICompositionProfile, new()
            {
                public ComposeDataAttribute(string memberName) { MemberName = memberName; }

                public string MemberName { get; }

                public System.Type? MemberType { get; set; }

                public int Seed { get; set; }
            }

            [System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = false)]
            public sealed class ComposeDataAttribute<TProfile, TConfig> : Xunit.v3.DataAttribute where TProfile : ICompositionProfile
            {
                public ComposeDataAttribute(string memberName) { MemberName = memberName; }

                public string MemberName { get; }

                public System.Type? MemberType { get; set; }

                public int Seed { get; set; }
            }
        }
        """;
}

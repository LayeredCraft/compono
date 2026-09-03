namespace Compono.Tests;

public sealed class GeneratorInfrastructureEditorBrowsableTests
{
    [Fact]
    public void DirectGeneratedCodeHooks_AreHidden_WhileRuntimeIntegrationHooksRemainVisible()
    {
        AssertHidden(typeof(PlanCache<>).GetProperty(nameof(PlanCache<object>.Instance))!);
        AssertHidden(typeof(CollectionPlanCache<>).GetProperty(nameof(CollectionPlanCache<object>.Instance))!);
        AssertHidden(typeof(RowInvokerRegistry).GetMethod(nameof(RowInvokerRegistry.Register))!);
        AssertHidden(typeof(GeneratedTestDoubleRegistry).GetMethod(nameof(GeneratedTestDoubleRegistry.RegisterFactory))!);
        AssertHidden(typeof(ReturnConfig<>).GetProperty(nameof(ReturnConfig<object>.HasConfiguredValue))!);
        AssertHidden(typeof(ReturnConfig<>).GetProperty(nameof(ReturnConfig<object>.HasConfiguredException))!);
        AssertHidden(typeof(ReturnConfig<>).GetProperty(nameof(ReturnConfig<object>.HasConfiguredSequence))!);
        AssertHidden(typeof(ReturnConfig<>).GetProperty(nameof(ReturnConfig<object>.ConfiguredValue))!);
        AssertHidden(typeof(ReturnConfig<>).GetProperty(nameof(ReturnConfig<object>.ConfiguredException))!);
        AssertHidden(typeof(ReturnConfig<>).GetProperty(nameof(ReturnConfig<object>.ConfiguredCallCount))!);
        AssertHidden(typeof(ReturnConfig<>).GetMethod(nameof(ReturnConfig<object>.RecordCall))!);
        AssertHidden(typeof(ReturnConfig<>).GetMethod(nameof(ReturnConfig<object>.ClearConfiguredResponse))!);
        AssertHidden(typeof(ReturnConfig<>).GetMethod(nameof(ReturnConfig<object>.NextSequenceOutcome))!);
        AssertHidden(typeof(ReturnConfigBuilder<>).GetConstructors().Single());

        AssertVisible(typeof(RowInvokerRegistry).GetMethod(nameof(RowInvokerRegistry.TryGet))!);
        AssertVisible(typeof(GeneratedTestDoubleRegistry).GetMethod(nameof(GeneratedTestDoubleRegistry.TryCreate))!);
    }

    private static void AssertHidden(System.Reflection.MemberInfo member) =>
        EditorBrowsableState(member).Should().Be(System.ComponentModel.EditorBrowsableState.Never);

    private static void AssertVisible(System.Reflection.MemberInfo member) =>
        member.GetCustomAttributes(typeof(System.ComponentModel.EditorBrowsableAttribute), inherit: false)
            .Should().BeEmpty();

    private static System.ComponentModel.EditorBrowsableState EditorBrowsableState(System.Reflection.MemberInfo member)
    {
        var attributes = member.GetCustomAttributes(typeof(System.ComponentModel.EditorBrowsableAttribute), inherit: false);

        attributes.Should().ContainSingle();
        return ((System.ComponentModel.EditorBrowsableAttribute)attributes[0]).State;
    }
}

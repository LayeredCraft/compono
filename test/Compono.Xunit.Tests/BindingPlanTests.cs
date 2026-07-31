using Compono.Xunit.Binding;
using Compono.Xunit.Tests.Fixtures;

namespace Compono.Xunit.Tests;

public sealed class BindingPlanTests
{
    [Fact]
    public void Build_HasNoSignatureError_ForAnOrdinarySignature()
    {
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.Simple))!;

        var plan = BindingPlan.Build(method);

        plan.SignatureError.Should().BeNull();
        plan.Parameters.Should().HaveCount(2);
    }

    [Fact]
    public void Build_ReportsASignatureError_ForAGenericMethod()
    {
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.Generic))!;

        var plan = BindingPlan.Build(method);

        plan.SignatureError.Should().Contain("generic");
        plan.Parameters.Should().BeEmpty();
    }

    [Fact]
    public void Build_ReportsASignatureError_ForARefParameter()
    {
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithRefParameter))!;

        var plan = BindingPlan.Build(method);

        plan.SignatureError.Should().Contain("value");
        plan.Parameters.Should().BeEmpty();
    }

    [Fact]
    public void Build_ReportsASignatureError_ForAnOutParameter()
    {
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithOutParameter))!;

        var plan = BindingPlan.Build(method);

        plan.SignatureError.Should().NotBeNull();
        plan.Parameters.Should().BeEmpty();
    }

    [Fact]
    public void Build_ReportsASignatureError_ForAnInParameter()
    {
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithInParameter))!;

        var plan = BindingPlan.Build(method);

        plan.SignatureError.Should().NotBeNull();
        plan.Parameters.Should().BeEmpty();
    }

    [Fact]
    public void Build_ReportsASignatureError_ForAParamsParameter()
    {
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithParamsParameter))!;

        var plan = BindingPlan.Build(method);

        plan.SignatureError.Should().Contain("params");
        plan.Parameters.Should().BeEmpty();
    }

    [Fact]
    public void Build_ReportsASignatureError_ForDuplicateSharedParameterTypes()
    {
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithDuplicateShared))!;

        var plan = BindingPlan.Build(method);

        plan.SignatureError.Should().Contain("first").And.Contain("second");
        plan.Parameters.Should().BeEmpty();
    }

    [Fact]
    public void Build_ReportsASignatureError_ForMultipleComposeFamilyAttributes()
    {
        // [AttributeUsage(AllowMultiple = false)] is enforced per exact attribute type, not across
        // the Compose family - [Compose] and [Compose<TProfile>] are distinct types that each
        // individually satisfy their own AllowMultiple = false, so stacking both compiles without
        // this explicit check (PR #23 review).
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithMultipleComposeAttributes))!;

        var plan = BindingPlan.Build(method);

        plan.SignatureError.Should().Contain("Compose");
        plan.Parameters.Should().BeEmpty();
    }

    [Fact]
    public void Build_MarksTheParameterAsShared_WhenAttributed()
    {
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithShared))!;

        var plan = BindingPlan.Build(method);

        plan.Parameters[0].IsShared.Should().BeTrue();
        plan.Parameters[1].IsShared.Should().BeFalse();
    }

    [Fact]
    public void Build_CapturesEachParametersNullability()
    {
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithNullableParameters))!;

        var plan = BindingPlan.Build(method);

        plan.Parameters[0].Descriptor.Nullability.Should().Be(Nullability.Nullable, "nullableReference is string?");
        plan.Parameters[1].Descriptor.Nullability.Should().Be(Nullability.NotNullable, "notNullableReference is string");
        plan.Parameters[2].Descriptor.Nullability.Should().Be(Nullability.Nullable, "nullableValue is int?");
        plan.Parameters[3].Descriptor.Nullability.Should().Be(Nullability.NotNullable, "notNullableValue is int");
    }

    [Fact]
    public void Build_DescriptorUsesParameterPositionNameAndDeclaringType()
    {
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.Simple))!;

        var plan = BindingPlan.Build(method);

        plan.Parameters[0].Descriptor.Kind.Should().Be(CompositionRequestKind.TestParameter);
        plan.Parameters[0].Descriptor.Ordinal.Should().Be(0);
        plan.Parameters[0].Descriptor.Name.Should().Be("number");
        plan.Parameters[0].Descriptor.DeclaringType.Should().Be(typeof(SampleTestMethods));
        plan.Parameters[1].Descriptor.Ordinal.Should().Be(1);
        plan.Parameters[1].Descriptor.Name.Should().Be("text");
    }
}

using Compono.TUnit.Binding;
using Compono.TUnit.Tests.Fixtures;

namespace Compono.TUnit.Tests;

public sealed class BindingPlanTests
{
    [Test]
    public async Task Build_HasNoSignatureError_ForAnOrdinarySignature()
    {
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.Simple))!;

        var plan = BindingPlan.Build(MethodMetadataTestFactory.Create(method));

        await Assert.That(plan.SignatureError).IsNull();
        await Assert.That(plan.Parameters.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Build_ReportsASignatureError_ForAGenericMethod()
    {
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.Generic))!;

        var plan = BindingPlan.Build(MethodMetadataTestFactory.Create(method));

        await Assert.That(plan.SignatureError).Contains("generic");
        await Assert.That(plan.Parameters).IsEmpty();
    }

    [Test]
    public async Task Build_ReportsASignatureError_ForARefParameter()
    {
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithRefParameter))!;

        var plan = BindingPlan.Build(MethodMetadataTestFactory.Create(method));

        await Assert.That(plan.SignatureError).Contains("value");
        await Assert.That(plan.Parameters).IsEmpty();
    }

    [Test]
    public async Task Build_ReportsASignatureError_ForAnOutParameter()
    {
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithOutParameter))!;

        var plan = BindingPlan.Build(MethodMetadataTestFactory.Create(method));

        await Assert.That(plan.SignatureError).IsNotNull();
        await Assert.That(plan.Parameters).IsEmpty();
    }

    [Test]
    public async Task Build_ReportsASignatureError_ForAnInParameter()
    {
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithInParameter))!;

        var plan = BindingPlan.Build(MethodMetadataTestFactory.Create(method));

        await Assert.That(plan.SignatureError).IsNotNull();
        await Assert.That(plan.Parameters).IsEmpty();
    }

    [Test]
    public async Task Build_ReportsASignatureError_ForAParamsParameter()
    {
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithParamsParameter))!;

        var plan = BindingPlan.Build(MethodMetadataTestFactory.Create(method));

        await Assert.That(plan.SignatureError).Contains("params");
        await Assert.That(plan.Parameters).IsEmpty();
    }

    [Test]
    public async Task Build_ReportsASignatureError_ForARefStructByValueParameter()
    {
        // ADR-0041's dispatch-eligibility guard, runtime side: a ref struct (e.g. Span<int>) can
        // never legally be a generic type argument to CompositionRow.Resolve<T>()/etc. at all - this
        // used to be a latent, undiagnosed gap under the old MakeGenericMethod-based design (it would
        // have thrown its own unclear error for the same shape); now it's a clear, intentional
        // ValidateSignature rejection instead.
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithRefStructParameter))!;

        var plan = BindingPlan.Build(MethodMetadataTestFactory.Create(method));

        await Assert.That(plan.SignatureError).Contains("ref struct");
        await Assert.That(plan.SignatureError).Contains("value");
        await Assert.That(plan.Parameters).IsEmpty();
    }

    [Test]
    public async Task Build_ReportsASignatureError_ForDuplicateSharedParameterTypes()
    {
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithDuplicateShared))!;

        var plan = BindingPlan.Build(MethodMetadataTestFactory.Create(method));

        await Assert.That(plan.SignatureError).Contains("first");
        await Assert.That(plan.SignatureError).Contains("second");
        await Assert.That(plan.Parameters).IsEmpty();
    }

    [Test]
    public async Task Build_MarksTheParameterAsShared_WhenAttributed()
    {
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithShared))!;

        var plan = BindingPlan.Build(MethodMetadataTestFactory.Create(method));

        await Assert.That(plan.Parameters[0].IsShared).IsTrue();
        await Assert.That(plan.Parameters[1].IsShared).IsFalse();
    }

    [Test]
    public async Task Build_CapturesEachParametersNullability()
    {
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithNullableParameters))!;

        var plan = BindingPlan.Build(MethodMetadataTestFactory.Create(method));

        await Assert.That(plan.Parameters[0].Descriptor.Nullability).IsEqualTo(Nullability.Nullable);
        await Assert.That(plan.Parameters[1].Descriptor.Nullability).IsEqualTo(Nullability.NotNullable);
        await Assert.That(plan.Parameters[2].Descriptor.Nullability).IsEqualTo(Nullability.Nullable);
        await Assert.That(plan.Parameters[3].Descriptor.Nullability).IsEqualTo(Nullability.NotNullable);
    }

    [Test]
    public async Task Build_DescriptorUsesParameterPositionNameAndDeclaringType()
    {
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.Simple))!;

        var plan = BindingPlan.Build(MethodMetadataTestFactory.Create(method));

        await Assert.That(plan.Parameters[0].Descriptor.Kind).IsEqualTo(CompositionRequestKind.TestParameter);
        await Assert.That(plan.Parameters[0].Descriptor.Ordinal).IsEqualTo(0);
        await Assert.That(plan.Parameters[0].Descriptor.Name).IsEqualTo("number");
        await Assert.That(plan.Parameters[0].Descriptor.DeclaringType).IsEqualTo(typeof(SampleTestMethods));
        await Assert.That(plan.Parameters[1].Descriptor.Ordinal).IsEqualTo(1);
        await Assert.That(plan.Parameters[1].Descriptor.Name).IsEqualTo("text");
    }
}

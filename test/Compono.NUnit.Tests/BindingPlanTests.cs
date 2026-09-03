using Compono.NUnit.Binding;
using Compono.NUnit.SignatureFixtures;
using Compono.NUnit.Tests.Fixtures;
using NUnit.Framework;

namespace Compono.NUnit.Tests;

[TestFixture]
public sealed class BindingPlanTests
{
    [Test]
    public void Build_HasNoSignatureError_ForAnOrdinarySignature()
    {
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.Simple))!;

        var plan = BindingPlan.Build(method);

        Assert.That(plan.SignatureError, Is.Null);
        Assert.That(plan.Parameters.Count, Is.EqualTo(2));
    }

    [Test]
    public void Build_ReportsASignatureError_ForAGenericMethod()
    {
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.Generic))!;

        var plan = BindingPlan.Build(method);

        Assert.That(plan.SignatureError, Does.Contain("generic"));
        Assert.That(plan.Parameters.Count, Is.EqualTo(0));
    }

    [Test]
    public void Build_ReportsASignatureError_ForARefParameter()
    {
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithRefParameter))!;

        var plan = BindingPlan.Build(method);

        Assert.That(plan.SignatureError, Is.Not.Null);
        Assert.That(plan.Parameters.Count, Is.EqualTo(0));
    }

    [Test]
    public void Build_ReportsASignatureError_ForAnOutParameter()
    {
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithOutParameter))!;

        var plan = BindingPlan.Build(method);

        Assert.That(plan.SignatureError, Is.Not.Null);
    }

    [Test]
    public void Build_ReportsASignatureError_ForAnInParameter()
    {
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithInParameter))!;

        var plan = BindingPlan.Build(method);

        Assert.That(plan.SignatureError, Is.Not.Null);
    }

    [Test]
    public void Build_ReportsASignatureError_ForAParamsParameter()
    {
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithParamsParameter))!;

        var plan = BindingPlan.Build(method);

        Assert.That(plan.SignatureError, Does.Contain("params"));
    }

    [Test]
    public void Build_ReportsASignatureError_ForARefStructByValueParameter()
    {
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithRefStructParameter))!;

        var plan = BindingPlan.Build(method);

        Assert.That(plan.SignatureError, Does.Contain("ref struct"));
    }

    [Test]
    public void Build_ReportsASignatureError_ForDuplicateSharedParameterTypes()
    {
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithDuplicateShared))!;

        var plan = BindingPlan.Build(method);

        Assert.That(plan.SignatureError, Does.Contain("first"));
        Assert.That(plan.SignatureError, Does.Contain("second"));
    }

    [Test]
    public void Build_ReportsASignatureError_ForMultipleComposeFamilyAttributes()
    {
        var method = typeof(InvalidSignatureFixtures).GetMethod(nameof(InvalidSignatureFixtures.WithMultipleComposeAttributes))!;

        var plan = BindingPlan.Build(method);

        Assert.That(plan.SignatureError, Does.Contain("Compose"));
    }

    [Test]
    public void Build_ReportsASignatureError_ForComposeStackedWithTheTwoTypeParameterForm()
    {
        var method = typeof(InvalidSignatureFixtures).GetMethod(nameof(InvalidSignatureFixtures.WithComposeAndTwoTypeParameterComposeAttributes))!;

        var plan = BindingPlan.Build(method);

        Assert.That(plan.SignatureError, Does.Contain("Compose<TProfile, TConfig>"));
    }

    [Test]
    public void Build_MarksTheParameterAsShared_WhenAttributed()
    {
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithShared))!;

        var plan = BindingPlan.Build(method);

        Assert.That(plan.Parameters[0].IsShared, Is.True);
        Assert.That(plan.Parameters[1].IsShared, Is.False);
    }

    [Test]
    public void Build_CapturesEachParametersNullability()
    {
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithNullableParameters))!;

        var plan = BindingPlan.Build(method);

        Assert.That(plan.Parameters[0].Descriptor.Nullability, Is.EqualTo(Nullability.Nullable));
        Assert.That(plan.Parameters[1].Descriptor.Nullability, Is.EqualTo(Nullability.NotNullable));
        Assert.That(plan.Parameters[2].Descriptor.Nullability, Is.EqualTo(Nullability.Nullable));
        Assert.That(plan.Parameters[3].Descriptor.Nullability, Is.EqualTo(Nullability.NotNullable));
    }

    [Test]
    public void Build_DescriptorUsesParameterPositionNameAndDeclaringType()
    {
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.Simple))!;

        var plan = BindingPlan.Build(method);

        Assert.That(plan.Parameters[0].Descriptor.Kind, Is.EqualTo(CompositionRequestKind.TestParameter));
        Assert.That(plan.Parameters[0].Descriptor.Ordinal, Is.EqualTo(0));
        Assert.That(plan.Parameters[0].Descriptor.Name, Is.EqualTo("number"));
        Assert.That(plan.Parameters[0].Descriptor.DeclaringType, Is.EqualTo(typeof(SampleTestMethods)));
        Assert.That(plan.Parameters[1].Descriptor.Ordinal, Is.EqualTo(1));
        Assert.That(plan.Parameters[1].Descriptor.Name, Is.EqualTo("text"));
    }
}

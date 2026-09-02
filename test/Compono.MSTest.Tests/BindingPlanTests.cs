using Compono.MSTest.Binding;
using Compono.MSTest.Tests.Fixtures;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Compono.MSTest.Tests;

[TestClass]
public sealed class BindingPlanTests
{
    [TestMethod]
    public void Build_HasNoSignatureError_ForAnOrdinarySignature()
    {
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.Simple))!;

        var plan = BindingPlan.Build(method);

        Assert.IsNull(plan.SignatureError);
        Assert.AreEqual(2, plan.Parameters.Count);
    }

    [TestMethod]
    public void Build_ReportsASignatureError_ForAGenericMethod()
    {
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.Generic))!;

        var plan = BindingPlan.Build(method);

        StringAssert.Contains(plan.SignatureError, "generic");
        Assert.AreEqual(0, plan.Parameters.Count);
    }

    [TestMethod]
    public void Build_ReportsASignatureError_ForARefParameter()
    {
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithRefParameter))!;

        var plan = BindingPlan.Build(method);

        Assert.IsNotNull(plan.SignatureError);
        Assert.AreEqual(0, plan.Parameters.Count);
    }

    [TestMethod]
    public void Build_ReportsASignatureError_ForAnOutParameter()
    {
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithOutParameter))!;

        var plan = BindingPlan.Build(method);

        Assert.IsNotNull(plan.SignatureError);
    }

    [TestMethod]
    public void Build_ReportsASignatureError_ForAnInParameter()
    {
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithInParameter))!;

        var plan = BindingPlan.Build(method);

        Assert.IsNotNull(plan.SignatureError);
    }

    [TestMethod]
    public void Build_ReportsASignatureError_ForAParamsParameter()
    {
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithParamsParameter))!;

        var plan = BindingPlan.Build(method);

        StringAssert.Contains(plan.SignatureError, "params");
    }

    [TestMethod]
    public void Build_ReportsASignatureError_ForARefStructByValueParameter()
    {
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithRefStructParameter))!;

        var plan = BindingPlan.Build(method);

        StringAssert.Contains(plan.SignatureError, "ref struct");
    }

    [TestMethod]
    public void Build_ReportsASignatureError_ForDuplicateSharedParameterTypes()
    {
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithDuplicateShared))!;

        var plan = BindingPlan.Build(method);

        StringAssert.Contains(plan.SignatureError, "first");
        StringAssert.Contains(plan.SignatureError, "second");
    }

    [TestMethod]
    public void Build_ReportsASignatureError_ForMultipleComposeFamilyAttributes()
    {
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithMultipleComposeAttributes))!;

        var plan = BindingPlan.Build(method);

        StringAssert.Contains(plan.SignatureError, "Compose");
    }

    [TestMethod]
    public void Build_ReportsASignatureError_ForComposeStackedWithTheTwoTypeParameterForm()
    {
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithComposeAndTwoTypeParameterComposeAttributes))!;

        var plan = BindingPlan.Build(method);

        StringAssert.Contains(plan.SignatureError, "Compose<TProfile, TConfig>");
    }

    [TestMethod]
    public void Build_MarksTheParameterAsShared_WhenAttributed()
    {
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithShared))!;

        var plan = BindingPlan.Build(method);

        Assert.IsTrue(plan.Parameters[0].IsShared);
        Assert.IsFalse(plan.Parameters[1].IsShared);
    }

    [TestMethod]
    public void Build_CapturesEachParametersNullability()
    {
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithNullableParameters))!;

        var plan = BindingPlan.Build(method);

        Assert.AreEqual(Nullability.Nullable, plan.Parameters[0].Descriptor.Nullability);
        Assert.AreEqual(Nullability.NotNullable, plan.Parameters[1].Descriptor.Nullability);
        Assert.AreEqual(Nullability.Nullable, plan.Parameters[2].Descriptor.Nullability);
        Assert.AreEqual(Nullability.NotNullable, plan.Parameters[3].Descriptor.Nullability);
    }

    [TestMethod]
    public void Build_DescriptorUsesParameterPositionNameAndDeclaringType()
    {
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.Simple))!;

        var plan = BindingPlan.Build(method);

        Assert.AreEqual(CompositionRequestKind.TestParameter, plan.Parameters[0].Descriptor.Kind);
        Assert.AreEqual(0, plan.Parameters[0].Descriptor.Ordinal);
        Assert.AreEqual("number", plan.Parameters[0].Descriptor.Name);
        Assert.AreEqual(typeof(SampleTestMethods), plan.Parameters[0].Descriptor.DeclaringType);
        Assert.AreEqual(1, plan.Parameters[1].Descriptor.Ordinal);
        Assert.AreEqual("text", plan.Parameters[1].Descriptor.Name);
    }
}

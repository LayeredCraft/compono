using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Compono.XunitV3.Binding;
using Xunit;
using Xunit.Sdk;
using Xunit.v3;

namespace Compono.XunitV3;

/// <summary>
/// Composes an xUnit v3 theory row's parameters through Compono, reading the rows from a public static
/// field, property, or parameterless method - the Composed theory rows counterpart to
/// <see cref="ComposeAttribute"/>. Each row is a separate theory row whose parameters are composed the
/// same way <see cref="ComposeAttribute"/> composes a single row. See
/// <c>docs/adr/0070-xunitv3-composedata-composed-theory-rows.md</c> for the full collection and row
/// shapes, the leading-positional binding algorithm, the seed policy, and the diagnostics.
/// </summary>
/// <remarks>
/// Deliberately unsealed - <see cref="ComposeDataAttribute{TProfile}"/> and
/// <see cref="ComposeDataAttribute{TProfile, TConfig}"/> are the two designed extension points,
/// mirroring <see cref="ComposeAttribute"/>'s own generic siblings. Any other subclass is rejected by
/// <see cref="GetData"/> with a <see cref="CompositionException"/> rather than being silently ignored.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class ComposeDataAttribute : DataAttribute
{
    // DisableParallelization does not exist on the xUnit 3.2.2 floor; the property is located once per
    // process and stays null there, making the read below a no-op (ADR-0070, RESEARCH-0038 §9.2 S1).
    private static readonly PropertyInfo? AttributeDisableParallelizationProperty =
        typeof(DataAttribute).GetProperty("DisableParallelization");

    private int? _seed;
    private MemberAccessor? _memberAccessor;
    private BindingPlan? _bindingPlan;
    private object? _bindingPlanLock;

    /// <summary>
    /// Creates a <see cref="ComposeDataAttribute"/>.
    /// </summary>
    /// <param name="memberName">
    /// The name of the public static field, property, or parameterless method that supplies this
    /// theory's rows. Resolved against <see cref="MemberType"/> when it is set, otherwise against the
    /// test class itself.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="memberName"/> is <see langword="null"/>.</exception>
    public ComposeDataAttribute(string memberName)
    {
        ArgumentNullException.ThrowIfNull(memberName);

        MemberName = memberName;
    }

    /// <summary>
    /// The name of the public static member that supplies this theory's rows.
    /// </summary>
    public string MemberName { get; }

    /// <summary>
    /// The type that declares <see cref="MemberName"/>. <see langword="null"/> (the default) means the
    /// test class itself, matching xUnit's own <c>MemberData</c> convention. The public fields,
    /// properties, and methods are preserved for dynamic access, since the member is located by
    /// reflection.
    /// </summary>
    [DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicFields
        | DynamicallyAccessedMemberTypes.PublicProperties
        | DynamicallyAccessedMemberTypes.PublicMethods)]
    public Type? MemberType { get; set; }

    /// <summary>
    /// An explicit root seed pinned to every row this attribute produces - the same underlying contract
    /// as <see cref="ComposeAttribute.Seed"/>, but restricted to non-negative values (enforced by the
    /// execution algorithm, not here). A plain <see langword="int"/>, not <see langword="int?"/> - an
    /// attribute named argument cannot target a <see cref="Nullable{T}"/> property (CS0655); see
    /// <see cref="SeedAsNullable"/> for the property the execution algorithm actually reads.
    /// </summary>
    public int Seed
    {
        get => _seed ?? default;
        set => _seed = value;
    }

    /// <summary>
    /// The value actually assigned to <see cref="Seed"/>, or <see langword="null"/> if it was never
    /// set - distinguishes "configured to 0" from "never configured," which <see cref="Seed"/> alone
    /// cannot (its getter falls back to <see langword="default"/>, i.e. <c>0</c>, when unset).
    /// </summary>
    internal int? SeedAsNullable => _seed;

    /// <summary>
    /// The row's per-row profile configuration type for the
    /// <see cref="ComposeDataAttribute{TProfile, TConfig}"/> form (element 0 of every row), or
    /// <see langword="null"/> for the other two forms where every element is a row value.
    /// </summary>
    internal virtual Type? ConfigurationType => null;

    /// <inheritdoc />
    public override bool SupportsDiscoveryEnumeration() => false;

    /// <summary>
    /// Builds one fresh composer per row, applies the form's profile (from the row's configuration for
    /// the two-type-parameter form), and pins the attribute-level seed when set. Overridden by the two
    /// generic forms.
    /// </summary>
    /// <param name="builder">The builder for the row's fresh composer.</param>
    /// <param name="config">The row's configuration value, for the two-type-parameter form.</param>
    internal virtual void ApplyProfile(CompositionBuilder builder, object? config)
    {
    }

    /// <inheritdoc />
    public override ValueTask<IReadOnlyCollection<ITheoryDataRow>> GetData(MethodInfo testMethod, DisposalTracker disposalTracker) =>
        GetDataCore(testMethod, TestContext.Current.CancellationToken);

    /// <summary>
    /// The execution algorithm, with the cancellation token supplied by the caller. <see cref="GetData"/>
    /// forwards <see cref="TestContext.Current"/>'s token; tests exercise this directly with a
    /// controllable token to verify cooperative cancellation.
    /// </summary>
    internal async ValueTask<IReadOnlyCollection<ITheoryDataRow>> GetDataCore(MethodInfo testMethod, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(testMethod);

        EnsureSupportedForm();

        var attributeSeed = SeedAsNullable;
        if (attributeSeed is { } negativeSeed && negativeSeed < 0)
        {
            throw ComposeDataRows.NegativeAttributeSeed(negativeSeed);
        }

        var bindingPlan = EnsureBindingPlan(testMethod);
        var methodDisplayName = BindingPlan.MethodDisplayName(testMethod);
        if (bindingPlan.SignatureError is not null)
        {
            throw new CompositionException(bindingPlan.SignatureError + ComposeDataRows.PreRowSeedLine(attributeSeed));
        }

        var accessor = EnsureMemberAccessor(testMethod, methodDisplayName);
        var configType = ConfigurationType;

        // The attribute-level metadata is identical for every row, so build it once per call (F8).
        var attributeMetadata = BuildAttributeMetadata();

        var rawRows = await ReadRawRowsAsync(accessor, methodDisplayName, cancellationToken).ConfigureAwait(false);

        var result = new List<ITheoryDataRow>(rawRows.Count);
        for (var index = 0; index < rawRows.Count; index++)
        {
            result.Add(ComposeRow(rawRows[index], index, accessor, bindingPlan, testMethod, methodDisplayName, attributeSeed, configType, attributeMetadata));
        }

        return result;
    }

    private void EnsureSupportedForm()
    {
        var type = GetType();
        if (type == typeof(ComposeDataAttribute))
        {
            return;
        }

        if (type.IsGenericType)
        {
            var definition = type.GetGenericTypeDefinition();
            if (definition == typeof(ComposeDataAttribute<>) || definition == typeof(ComposeDataAttribute<,>))
            {
                return;
            }
        }

        throw new CompositionException(
            $"ComposeData attribute type '{type}' is not supported. Only ComposeDataAttribute, "
            + "ComposeDataAttribute<TProfile>, and ComposeDataAttribute<TProfile, TConfig> are supported.");
    }

    private BindingPlan EnsureBindingPlan(MethodInfo testMethod) =>
        LazyInitializer.EnsureInitialized(ref _bindingPlan, ref _bindingPlanLock, () => BindingPlan.Build(testMethod))!;

    private MemberAccessor EnsureMemberAccessor(MethodInfo testMethod, string methodDisplayName) =>
        LazyInitializer.EnsureInitialized(ref _memberAccessor, () => BuildMemberAccessor(testMethod, methodDisplayName));

    private MemberAccessor BuildMemberAccessor(MethodInfo testMethod, string methodDisplayName)
    {
        var targetType = MemberType ?? testMethod.ReflectedType ?? testMethod.DeclaringType
            ?? throw new CompositionException($"ComposeData member '{MemberName}' is declared on an unresolved test method '{methodDisplayName}'.");
        var memberDisplayName = $"{targetType.FullName}.{MemberName}";
        const BindingFlags publicStatic = BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy;

        try
        {
            if (targetType.GetField(MemberName, publicStatic) is { } field)
            {
                return BuildAccessor(field.FieldType, () => field.GetValue(null), memberDisplayName, methodDisplayName);
            }

            if (targetType.GetProperty(MemberName, publicStatic) is { GetMethod: { IsPublic: true, IsStatic: true } getter } property)
            {
                return BuildAccessor(property.PropertyType, () => getter.Invoke(null, null), memberDisplayName, methodDisplayName);
            }

            if (targetType.GetMethod(MemberName, publicStatic, binder: null, types: Type.EmptyTypes, modifiers: null) is { } method)
            {
                return BuildAccessor(method.ReturnType, () => method.Invoke(null, null), memberDisplayName, methodDisplayName);
            }
        }
        catch (AmbiguousMatchException exception)
        {
            throw MemberResolutionFailure(memberDisplayName, methodDisplayName, exception);
        }

        var candidates = targetType.GetMember(
            MemberName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.FlattenHierarchy);

        if (candidates.Length == 0)
        {
            throw ComposeDataRows.MemberNotFound(memberDisplayName, methodDisplayName);
        }

        if (Array.Exists(candidates, static member => member is MethodInfo candidate && candidate.GetParameters().Length > 0))
        {
            throw ComposeDataRows.MemberRequiresArguments(memberDisplayName, methodDisplayName);
        }

        throw ComposeDataRows.MemberNotPublicStatic(memberDisplayName, methodDisplayName);
    }

    private static MemberAccessor BuildAccessor(Type returnType, Func<object?> read, string memberDisplayName, string methodDisplayName)
    {
        if (TryGetAsyncEnumerableElementType(returnType, out var elementType))
        {
            // The reflection used to enumerate a value-type element stream is invariant for the member,
            // so resolve it once here rather than per row (F8).
            var enumerableInterface = typeof(IAsyncEnumerable<>).MakeGenericType(elementType);
            var enumeratorInterface = typeof(IAsyncEnumerator<>).MakeGenericType(elementType);
            return new MemberAccessor
            {
                MemberDisplayName = memberDisplayName,
                Shape = MemberShape.AsyncEnumerable,
                Read = read,
                AsyncElementType = elementType,
                AsyncGetEnumeratorMethod = enumerableInterface.GetMethod(nameof(IAsyncEnumerable<object?>.GetAsyncEnumerator)),
                AsyncMoveNextMethod = enumeratorInterface.GetMethod(nameof(IAsyncEnumerator<object?>.MoveNextAsync)),
                AsyncCurrentProperty = enumeratorInterface.GetProperty(nameof(IAsyncEnumerator<object?>.Current)),
                AsyncDisposeMethod = typeof(IAsyncDisposable).GetMethod(nameof(IAsyncDisposable.DisposeAsync)),
            };
        }

        if (returnType == typeof(Task) || returnType == typeof(ValueTask))
        {
            throw ComposeDataRows.UnsupportedReturnType(memberDisplayName, methodDisplayName, returnType);
        }

        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            return new MemberAccessor
            {
                MemberDisplayName = memberDisplayName,
                Shape = MemberShape.TaskResult,
                Read = read,
                ResultProperty = returnType.GetProperty(nameof(Task<int>.Result)),
            };
        }

        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(ValueTask<>))
        {
            var asTask = returnType.GetMethod(nameof(ValueTask<int>.AsTask), Type.EmptyTypes);
            return new MemberAccessor
            {
                MemberDisplayName = memberDisplayName,
                Shape = MemberShape.ValueTaskResult,
                Read = read,
                AsTaskMethod = asTask,
                ResultProperty = asTask?.ReturnType.GetProperty(nameof(Task<int>.Result)),
            };
        }

        if (returnType != typeof(string) && typeof(IEnumerable).IsAssignableFrom(returnType))
        {
            return new MemberAccessor
            {
                MemberDisplayName = memberDisplayName,
                Shape = MemberShape.SyncCollection,
                Read = read,
            };
        }

        throw ComposeDataRows.UnsupportedReturnType(memberDisplayName, methodDisplayName, returnType);
    }

    private static bool TryGetAsyncEnumerableElementType(Type type, out Type elementType)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>))
        {
            elementType = type.GetGenericArguments()[0];
            return true;
        }

        foreach (var candidate in type.GetInterfaces())
        {
            if (candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>))
            {
                elementType = candidate.GetGenericArguments()[0];
                return true;
            }
        }

        elementType = typeof(object);
        return false;
    }

    private static async ValueTask<List<object?>> ReadRawRowsAsync(MemberAccessor accessor, string methodDisplayName, CancellationToken cancellationToken)
    {
        var rows = new List<object?>();

        switch (accessor.Shape)
        {
            case MemberShape.SyncCollection:
                AppendSync(InvokeRead(accessor, methodDisplayName), rows, accessor, methodDisplayName, cancellationToken);
                break;

            case MemberShape.TaskResult:
            case MemberShape.ValueTaskResult:
                AppendSync(await AwaitReadAsync(accessor, methodDisplayName).ConfigureAwait(false), rows, accessor, methodDisplayName, cancellationToken);
                break;

            case MemberShape.AsyncEnumerable:
                await AppendAsync(accessor, rows, methodDisplayName, cancellationToken).ConfigureAwait(false);
                break;
        }

        return rows;
    }

    private static object? InvokeRead(MemberAccessor accessor, string methodDisplayName)
    {
        try
        {
            return accessor.Read();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            RethrowIfReflectedCancellation(exception);
            throw ComposeDataRows.MemberReadFailure(accessor.MemberDisplayName, methodDisplayName, exception);
        }
    }

    // A JIT member read always goes through reflection (field.GetValue / MethodInfo.Invoke), which wraps
    // whatever the member threw in TargetInvocationException. Cooperative cancellation must reach the
    // caller unchanged: the generated AOT path reads the member directly, so its OperationCanceledException
    // is never wrapped, and the two engines would otherwise diverge (ADR-0070 contract item 13). Unwrap
    // the reflected cause and rethrow it before the composition-failure wrapper turns it into a
    // CompositionException. Non-cancellation causes still flow through MemberReadFailure, which unwraps the
    // TargetInvocationException for the reported cause.
    private static void RethrowIfReflectedCancellation(Exception exception)
    {
        if (exception is TargetInvocationException { InnerException: OperationCanceledException cancellation })
        {
            ExceptionDispatchInfo.Capture(cancellation).Throw();
        }
    }

    private static async ValueTask<object?> AwaitReadAsync(MemberAccessor accessor, string methodDisplayName)
    {
        object? pending;
        try
        {
            pending = accessor.Read();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            RethrowIfReflectedCancellation(exception);
            throw ComposeDataRows.MemberReadFailure(accessor.MemberDisplayName, methodDisplayName, exception);
        }

        if (pending is null)
        {
            return null;
        }

        Task task;
        try
        {
            task = accessor.AsTaskMethod is not null
                ? (Task)accessor.AsTaskMethod.Invoke(pending, null)!
                : (Task)pending;
            await task.ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not CompositionException and not OperationCanceledException)
        {
            throw ComposeDataRows.MemberReadFailure(accessor.MemberDisplayName, methodDisplayName, exception);
        }

        return accessor.ResultProperty?.GetValue(task);
    }

    private static void AppendSync(object? source, List<object?> rows, MemberAccessor accessor, string methodDisplayName, CancellationToken cancellationToken)
    {
        if (source is null)
        {
            throw ComposeDataRows.NullResult(accessor.MemberDisplayName, methodDisplayName);
        }

        if (!ComposeDataRows.IsSupportedCollectionResult(source))
        {
            throw ComposeDataRows.UnsupportedReturnType(accessor.MemberDisplayName, methodDisplayName, source.GetType());
        }

        try
        {
            foreach (var row in (IEnumerable)source)
            {
                cancellationToken.ThrowIfCancellationRequested();
                rows.Add(row);
            }
        }
        catch (Exception exception) when (exception is not CompositionException and not OperationCanceledException)
        {
            // A lazily-evaluated collection can throw mid-enumeration; wrap it the same way the async
            // paths do, but never convert cooperative cancellation into a composition failure.
            throw ComposeDataRows.MemberReadFailure(accessor.MemberDisplayName, methodDisplayName, exception);
        }
    }

    private static async ValueTask AppendAsync(MemberAccessor accessor, List<object?> rows, string methodDisplayName, CancellationToken cancellationToken)
    {
        object? source;
        try
        {
            source = accessor.Read();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            RethrowIfReflectedCancellation(exception);
            throw ComposeDataRows.MemberReadFailure(accessor.MemberDisplayName, methodDisplayName, exception);
        }

        if (source is null)
        {
            throw ComposeDataRows.NullResult(accessor.MemberDisplayName, methodDisplayName);
        }

        if (source is IAsyncEnumerable<object?> enumerable)
        {
            try
            {
                await foreach (var row in enumerable.WithCancellation(cancellationToken).ConfigureAwait(false))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    rows.Add(row);
                }
            }
            catch (Exception exception) when (exception is not CompositionException and not OperationCanceledException)
            {
                throw ComposeDataRows.MemberReadFailure(accessor.MemberDisplayName, methodDisplayName, exception);
            }

            return;
        }

        await AppendAsyncByReflectionAsync(accessor, source, rows, methodDisplayName, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask AppendAsyncByReflectionAsync(
        MemberAccessor accessor,
        object source,
        List<object?> rows,
        string methodDisplayName,
        CancellationToken cancellationToken)
    {
        // The compiler-generated async iterator implements IAsyncEnumerator<T> explicitly, so the
        // members are not public on its concrete type - dispatch through the interface members cached
        // on the accessor at build time. Obtaining the enumerator is itself part of reading the member,
        // so a failure there is wrapped the same way the synchronous and fast async paths wrap theirs.
        object? enumerator;
        try
        {
            enumerator = accessor.AsyncGetEnumeratorMethod!.Invoke(source, [cancellationToken]);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw ComposeDataRows.MemberReadFailure(accessor.MemberDisplayName, methodDisplayName, exception);
        }

        Exception? failure = null;

        try
        {
            while (await ((ValueTask<bool>)accessor.AsyncMoveNextMethod!.Invoke(enumerator, null)!).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                rows.Add(accessor.AsyncCurrentProperty!.GetValue(enumerator));
            }
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        try
        {
            await ((ValueTask)accessor.AsyncDisposeMethod!.Invoke(enumerator, null)!).ConfigureAwait(false);
        }
        catch (Exception disposeException)
        {
            // When enumeration already failed, the primary failure wins and the dispose failure is
            // secondary; when enumeration succeeded, a dispose failure is the real failure and must
            // not be silently swallowed.
            failure ??= disposeException;
        }

        if (failure is not null)
        {
            // Cooperative cancellation propagates as cancellation, and an already composition-shaped
            // failure is never double-wrapped - both preserve the original failure-context contract
            // (ADR-0070). Everything else becomes a member-read failure carrying the original
            // exception (and its diagnostic, when it has one).
            if (failure is OperationCanceledException or CompositionException)
            {
                ExceptionDispatchInfo.Capture(failure).Throw();
            }

            throw ComposeDataRows.MemberReadFailure(accessor.MemberDisplayName, methodDisplayName, failure);
        }
    }

    private ITheoryDataRow ComposeRow(
        object? rawRow,
        int index,
        MemberAccessor accessor,
        BindingPlan bindingPlan,
        MethodInfo testMethod,
        string methodDisplayName,
        int? attributeSeed,
        Type? configType,
        in ComposeDataAttributeMetadata attributeMetadata)
    {
        var memberDisplayName = accessor.MemberDisplayName;
        var initialContext = new RowContext(index, null, memberDisplayName, methodDisplayName);
        var rowData = ComposeDataRows.NormalizeRow(rawRow, in initialContext, attributeSeed);
        var context = new RowContext(index, rowData.Source, memberDisplayName, methodDisplayName);

        var values = rowData.Values;
        object? config = null;
        if (configType is not null)
        {
            if (values.Length == 0 || values[0] is not { } configValue || !configType.IsInstanceOfType(configValue))
            {
                throw ComposeDataRows.RowFailure(
                    context,
                    $"the first row element must be a non-null '{configType}' profile configuration value",
                    ComposeDataRows.PreRowSeedLine(attributeSeed));
            }

            config = configValue;
            var remaining = new object?[values.Length - 1];
            Array.Copy(values, 1, remaining, 0, remaining.Length);
            values = remaining;
        }

        CompositionRow row;
        try
        {
            var composer = Composer.Create(builder =>
            {
                ApplyProfile(builder, config);
                if (attributeSeed is { } seed)
                {
                    builder.WithSeed(seed);
                }
            });

            row = composer.CreateRow(testMethod.DeclaringType!);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Covers the profile constructor / Configure throwing, a core configuration conflict such
            // as the attribute seed colliding with a profile WithSeed (CompositionConfigurationException),
            // and any other pre-CreateRow failure - all R0 with a pre-row seed line (ADR-0070).
            throw ComposeDataRows.RowFailure(context, exception.Message, ComposeDataRows.PreRowSeedLine(attributeSeed), exception);
        }

        if (row.Seed < 0)
        {
            throw ComposeDataRows.NegativeRowSeed(context, row.Seed);
        }

        var parameters = bindingPlan.Parameters;
        if (values.Length > parameters.Count)
        {
            throw ComposeDataRows.TooManyValues(context, values.Length, parameters.Count, row.Seed);
        }

        for (var i = 0; i < values.Length; i++)
        {
            var parameter = parameters[i];
            switch (PositionalArgumentBinder.Validate(parameter.ParameterType, parameter.Descriptor.Nullability, values[i]))
            {
                case PositionalArgumentValidation.NullNotAllowed:
                    throw ComposeDataRows.RowFailure(
                        context,
                        $"Row value for parameter '{parameter.Name}' on '{methodDisplayName}' is null, but the parameter is not nullable.",
                        ComposeDataRows.RowSeedLine(row.Seed));
                case PositionalArgumentValidation.TypeMismatch:
                    throw ComposeDataRows.RowFailure(
                        context,
                        $"Row value for parameter '{parameter.Name}' on '{methodDisplayName}' has type '{values[i]!.GetType()}', which is not assignable to '{parameter.ParameterType}'.",
                        ComposeDataRows.RowSeedLine(row.Seed));
            }
        }

        var composed = new object?[parameters.Count];

        for (var i = 0; i < parameters.Count; i++)
        {
            var parameter = parameters[i];
            if (!parameter.IsShared)
            {
                continue;
            }

            composed[i] = i < values.Length
                ? ShareExplicit(parameter, row, values[i], context)
                : ResolveShared(parameter, row, context);
        }

        for (var i = 0; i < parameters.Count; i++)
        {
            var parameter = parameters[i];
            if (parameter.IsShared)
            {
                continue;
            }

            composed[i] = i < values.Length ? values[i] : Resolve(parameter, row, context);
        }

        return ComposeDataRows.CreateDataRow(composed, context, attributeMetadata, row.Seed);
    }

    private static object? Resolve(ParameterBindingPlan parameter, CompositionRow row, in RowContext context)
    {
        try
        {
            return parameter.ResolveInvoker(row, parameter.Descriptor);
        }
        catch (CompositionException exception)
        {
            throw ComposeDataRows.RowFailure(context, exception.Message, ComposeDataRows.RowSeedLine(row.Seed), exception);
        }
    }

    private static object? ResolveShared(ParameterBindingPlan parameter, CompositionRow row, in RowContext context)
    {
        try
        {
            return parameter.ResolveSharedInvoker(row, parameter.Descriptor);
        }
        catch (CompositionException exception)
        {
            throw ComposeDataRows.RowFailure(context, exception.Message, ComposeDataRows.RowSeedLine(row.Seed), exception);
        }
    }

    private static object? ShareExplicit(ParameterBindingPlan parameter, CompositionRow row, object? value, in RowContext context)
    {
        try
        {
            parameter.ShareExplicitInvoker(row, parameter.Descriptor, value);
            return value;
        }
        catch (CompositionException exception)
        {
            throw ComposeDataRows.RowFailure(context, exception.Message, ComposeDataRows.RowSeedLine(row.Seed), exception);
        }
    }

    private static CompositionException MemberResolutionFailure(string member, string methodDisplayName, Exception exception) =>
        new(
            $"ComposeData member '{member}' for '{methodDisplayName}' is ambiguous: more than one inherited or hidden static member matches. Set MemberType to disambiguate, or rename the member.",
            (exception as CompositionException)?.Diagnostic,
            exception);

    private ComposeDataAttributeMetadata BuildAttributeMetadata() => new()
    {
        Explicit = ExplicitAsNullable,
        Label = Label,
        Skip = Skip,
        TestDisplayName = TestDisplayName,
        Timeout = TimeoutAsNullable,
        SkipUnless = SkipUnless,
        SkipWhen = SkipWhen,
        SkipType = SkipType,
        Traits = Traits is { Length: > 0 } ? Traits : null,
        DisableParallelization = AttributeDisableParallelizationProperty?.GetValue(this) as bool?,
    };

    private enum MemberShape
    {
        SyncCollection,
        TaskResult,
        ValueTaskResult,
        AsyncEnumerable,
    }

    private sealed class MemberAccessor
    {
        public required string MemberDisplayName { get; init; }

        public required MemberShape Shape { get; init; }

        public required Func<object?> Read { get; init; }

        public PropertyInfo? ResultProperty { get; init; }

        public MethodInfo? AsTaskMethod { get; init; }

        public Type? AsyncElementType { get; init; }

        public MethodInfo? AsyncGetEnumeratorMethod { get; init; }

        public MethodInfo? AsyncMoveNextMethod { get; init; }

        public PropertyInfo? AsyncCurrentProperty { get; init; }

        public MethodInfo? AsyncDisposeMethod { get; init; }
    }
}

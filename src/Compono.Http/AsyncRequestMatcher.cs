namespace Compono.Http;

/// <summary>
/// One matching condition on a request - the internal unit every <c>OnX(path)</c>/<c>When</c>/
/// <c>WhenAsync</c>/<c>WithHeader</c>/<c>WithBody</c>/<c>WithFormBody</c>/<c>WithJsonBody{T}</c>
/// condition compiles to (ADR-0062 D1). A synchronously-completing condition returns an
/// already-completed <see cref="ValueTask{TResult}"/> - no <see cref="Task"/> allocation, no
/// suspension. Never public; a consumer only ever sees the public methods that produce one.
/// </summary>
internal delegate ValueTask<bool> AsyncRequestMatcher(HttpRequestMessage request, CancellationToken cancellationToken);

﻿/*
 * Licensed under the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
 * See https://github.com/openiddict/openiddict-core for more information concerning
 * the license and the contributors participating to this project.
 */

using System.Collections.Immutable;
using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Extensions;
using Owin;
using static OpenIddict.Server.Owin.OpenIddictServerOwinConstants;
using JsonWebTokenTypes = OpenIddict.Server.Owin.OpenIddictServerOwinConstants.JsonWebTokenTypes;

namespace OpenIddict.Server.Owin;

public static partial class OpenIddictServerOwinHandlers
{
    public static class Session
    {
        public static ImmutableArray<OpenIddictServerHandlerDescriptor> DefaultHandlers { get; } = [
            /*
             * Logout request extraction:
             */
            ExtractGetOrPostRequest<ExtractLogoutRequestContext>.Descriptor,
            RestoreCachedRequestParameters.Descriptor,
            CacheRequestParameters.Descriptor,

            /*
             * Logout request handling:
             */
            EnablePassthroughMode<HandleLogoutRequestContext, RequireLogoutEndpointPassthroughEnabled>.Descriptor,

            /*
             * Logout response processing:
             */
            RemoveCachedRequest.Descriptor,
            AttachHttpResponseCode<ApplyLogoutResponseContext>.Descriptor,
            AttachOwinResponseChallenge<ApplyLogoutResponseContext>.Descriptor,
            SuppressFormsAuthenticationRedirect<ApplyLogoutResponseContext>.Descriptor,
            AttachCacheControlHeader<ApplyLogoutResponseContext>.Descriptor,
            ProcessHostRedirectionResponse.Descriptor,
            ProcessPassthroughErrorResponse<ApplyLogoutResponseContext, RequireLogoutEndpointPassthroughEnabled>.Descriptor,
            ProcessLocalErrorResponse<ApplyLogoutResponseContext>.Descriptor,
            ProcessQueryResponse.Descriptor,
            ProcessEmptyResponse<ApplyLogoutResponseContext>.Descriptor
        ];

        /// <summary>
        /// Contains the logic responsible for restoring cached requests from the request_id, if specified.
        /// Note: this handler is not used when the OpenID Connect request is not initially handled by OWIN.
        /// </summary>
        public sealed class RestoreCachedRequestParameters : IOpenIddictServerHandler<ExtractLogoutRequestContext>
        {
            private readonly IDistributedCache _cache;

            public RestoreCachedRequestParameters() => throw new InvalidOperationException(SR.GetResourceString(SR.ID0116));

            public RestoreCachedRequestParameters(IDistributedCache cache)
                => _cache = cache ?? throw new ArgumentNullException(nameof(cache));

            /// <summary>
            /// Gets the default descriptor definition assigned to this handler.
            /// </summary>
            public static OpenIddictServerHandlerDescriptor Descriptor { get; }
                = OpenIddictServerHandlerDescriptor.CreateBuilder<ExtractLogoutRequestContext>()
                    .AddFilter<RequireOwinRequest>()
                    .AddFilter<RequireLogoutRequestCachingEnabled>()
                    .UseSingletonHandler<RestoreCachedRequestParameters>()
                    .SetOrder(ExtractGetOrPostRequest<ExtractLogoutRequestContext>.Descriptor.Order + 1_000)
                    .SetType(OpenIddictServerHandlerType.BuiltIn)
                    .Build();

            /// <inheritdoc/>
            public async ValueTask HandleAsync(ExtractLogoutRequestContext context)
            {
                if (context is null)
                {
                    throw new ArgumentNullException(nameof(context));
                }

                Debug.Assert(context.Request is not null, SR.GetResourceString(SR.ID4008));

                // If a request_id parameter can be found in the logout request,
                // restore the complete logout request from the distributed cache.

                if (string.IsNullOrEmpty(context.Request.RequestId))
                {
                    return;
                }

                // Note: the cache key is always prefixed with a specific marker
                // to avoid collisions with the other types of cached payloads.
                var token = await _cache.GetStringAsync(Cache.LogoutRequest + context.Request.RequestId);
                if (token is null || !context.Options.JsonWebTokenHandler.CanReadToken(token))
                {
                    context.Logger.LogInformation(SR.GetResourceString(SR.ID6150), Parameters.RequestId);

                    context.Reject(
                        error: Errors.InvalidRequest,
                        description: SR.FormatID2052(Parameters.RequestId),
                        uri: SR.FormatID8000(SR.ID2052));

                    return;
                }

                var parameters = context.Options.TokenValidationParameters.Clone();
                parameters.ValidIssuer ??= (context.Options.Issuer ?? context.BaseUri)?.AbsoluteUri;
                parameters.ValidAudience ??= parameters.ValidIssuer;
                parameters.ValidTypes = [JsonWebTokenTypes.Private.LogoutRequest];

                var result = await context.Options.JsonWebTokenHandler.ValidateTokenAsync(token, parameters);
                if (!result.IsValid)
                {
                    context.Logger.LogInformation(SR.GetResourceString(SR.ID6150), Parameters.RequestId);

                    context.Reject(
                        error: Errors.InvalidRequest,
                        description: SR.FormatID2052(Parameters.RequestId),
                        uri: SR.FormatID8000(SR.ID2052));

                    return;
                }

                using var document = JsonDocument.Parse(
                    Base64UrlEncoder.Decode(((JsonWebToken) result.SecurityToken).InnerToken.EncodedPayload));
                if (document.RootElement.ValueKind is not JsonValueKind.Object)
                {
                    throw new InvalidOperationException(SR.GetResourceString(SR.ID0118));
                }

                // Restore the request parameters from the serialized payload.
                foreach (var parameter in document.RootElement.EnumerateObject())
                {
                    if (!context.Request.HasParameter(parameter.Name))
                    {
                        context.Request.AddParameter(parameter.Name, parameter.Value.Clone());
                    }
                }
            }
        }

        /// <summary>
        /// Contains the logic responsible for caching logout requests, if applicable.
        /// Note: this handler is not used when the OpenID Connect request is not initially handled by OWIN.
        /// </summary>
        public sealed class CacheRequestParameters : IOpenIddictServerHandler<ExtractLogoutRequestContext>
        {
            private readonly IDistributedCache _cache;
            private readonly IOptionsMonitor<OpenIddictServerOwinOptions> _options;

            public CacheRequestParameters() => throw new InvalidOperationException(SR.GetResourceString(SR.ID0116));

            public CacheRequestParameters(
                IDistributedCache cache,
                IOptionsMonitor<OpenIddictServerOwinOptions> options)
            {
                _cache = cache ?? throw new ArgumentNullException(nameof(cache));
                _options = options ?? throw new ArgumentNullException(nameof(options));
            }

            /// <summary>
            /// Gets the default descriptor definition assigned to this handler.
            /// </summary>
            public static OpenIddictServerHandlerDescriptor Descriptor { get; }
                = OpenIddictServerHandlerDescriptor.CreateBuilder<ExtractLogoutRequestContext>()
                    .AddFilter<RequireOwinRequest>()
                    .AddFilter<RequireLogoutRequestCachingEnabled>()
                    .UseSingletonHandler<CacheRequestParameters>()
                    .SetOrder(RestoreCachedRequestParameters.Descriptor.Order + 1_000)
                    .SetType(OpenIddictServerHandlerType.BuiltIn)
                    .Build();

            /// <inheritdoc/>
            public async ValueTask HandleAsync(ExtractLogoutRequestContext context)
            {
                if (context is null)
                {
                    throw new ArgumentNullException(nameof(context));
                }

                if (context is not { BaseUri.IsAbsoluteUri: true, RequestUri.IsAbsoluteUri: true })
                {
                    throw new InvalidOperationException(SR.GetResourceString(SR.ID0127));
                }

                Debug.Assert(context.Request is not null, SR.GetResourceString(SR.ID4008));

                // This handler only applies to OWIN requests. If The OWIN request cannot be resolved,
                // this may indicate that the request was incorrectly processed by another server stack.
                var request = context.Transaction.GetOwinRequest() ??
                    throw new InvalidOperationException(SR.GetResourceString(SR.ID0120));

                // Don't cache the request if the request doesn't include any parameter.
                // If a request_id parameter can be found in the logout request,
                // ignore the following logic to prevent an infinite redirect loop.
                if (context.Request.Count is 0 || !string.IsNullOrEmpty(context.Request.RequestId))
                {
                    return;
                }

                // Generate a 256-bit request identifier using a crypto-secure random number generator.
                context.Request.RequestId = Base64UrlEncoder.Encode(OpenIddictHelpers.CreateRandomArray(size: 256));

                // Build a list of claims matching the parameters extracted from the request.
                //
                // Note: in most cases, parameters should be representated as strings as requests are
                // typically resolved from the query string or the request form, where parameters
                // are natively represented as strings. However, requests can also be extracted from
                // different places where they can be represented as complex JSON representations
                // (e.g requests extracted from a JSON Web Token that may be encrypted and/or signed).
                var claims = from parameter in context.Request.GetParameters()
                             let element = (JsonElement) parameter.Value
                             let type = element.ValueKind switch
                             {
                                 JsonValueKind.String                          => ClaimValueTypes.String,
                                 JsonValueKind.Number                          => ClaimValueTypes.Integer64,
                                 JsonValueKind.True or JsonValueKind.False     => ClaimValueTypes.Boolean,
                                 JsonValueKind.Null or JsonValueKind.Undefined => JsonClaimValueTypes.JsonNull,
                                 JsonValueKind.Array                           => JsonClaimValueTypes.JsonArray,
                                 JsonValueKind.Object or _                     => JsonClaimValueTypes.Json
                             }
                             select new Claim(parameter.Key, element.ToString()!, type);

                // Store the serialized logout request parameters in the distributed cache.
                var token = context.Options.JsonWebTokenHandler.CreateToken(new SecurityTokenDescriptor
                {
                    Audience = (context.Options.Issuer ?? context.BaseUri)?.AbsoluteUri,
                    EncryptingCredentials = context.Options.EncryptionCredentials.First(),
                    Issuer = (context.Options.Issuer ?? context.BaseUri)?.AbsoluteUri,
                    SigningCredentials = context.Options.SigningCredentials.First(),
                    Subject = new ClaimsIdentity(claims, TokenValidationParameters.DefaultAuthenticationType),
                    TokenType = JsonWebTokenTypes.Private.LogoutRequest
                });

                // Note: the cache key is always prefixed with a specific marker
                // to avoid collisions with the other types of cached payloads.
                await _cache.SetStringAsync(Cache.LogoutRequest + context.Request.RequestId,
                    token, _options.CurrentValue.LogoutRequestCachingPolicy);

                // Create a new GET logout request containing only the request_id parameter.
                var location = WebUtilities.AddQueryString(
                    uri: new UriBuilder(context.RequestUri) { Query = null }.Uri.AbsoluteUri,
                    name: Parameters.RequestId,
                    value: context.Request.RequestId);

                request.Context.Response.Redirect(location);

                // Mark the response as handled to skip the rest of the pipeline.
                context.HandleRequest();
            }
        }

        /// <summary>
        /// Contains the logic responsible for removing cached logout requests from the distributed cache.
        /// Note: this handler is not used when the OpenID Connect request is not initially handled by OWIN.
        /// </summary>
        public sealed class RemoveCachedRequest : IOpenIddictServerHandler<ApplyLogoutResponseContext>
        {
            private readonly IDistributedCache _cache;

            public RemoveCachedRequest() => throw new InvalidOperationException(SR.GetResourceString(SR.ID0116));

            public RemoveCachedRequest(IDistributedCache cache)
                => _cache = cache ?? throw new ArgumentNullException(nameof(cache));

            /// <summary>
            /// Gets the default descriptor definition assigned to this handler.
            /// </summary>
            public static OpenIddictServerHandlerDescriptor Descriptor { get; }
                = OpenIddictServerHandlerDescriptor.CreateBuilder<ApplyLogoutResponseContext>()
                    .AddFilter<RequireOwinRequest>()
                    .AddFilter<RequireLogoutRequestCachingEnabled>()
                    .UseSingletonHandler<RemoveCachedRequest>()
                    .SetOrder(int.MinValue + 100_000)
                    .SetType(OpenIddictServerHandlerType.BuiltIn)
                    .Build();

            /// <inheritdoc/>
            public ValueTask HandleAsync(ApplyLogoutResponseContext context)
            {
                if (context is null)
                {
                    throw new ArgumentNullException(nameof(context));
                }

                if (string.IsNullOrEmpty(context.Request?.RequestId))
                {
                    return default;
                }

                // Note: the ApplyLogoutResponse event is called for both successful
                // and errored logout responses but discrimination is not necessary here,
                // as the logout request must be removed from the distributed cache in both cases.

                // Note: the cache key is always prefixed with a specific marker
                // to avoid collisions with the other types of cached payloads.
                return new(_cache.RemoveAsync(Cache.LogoutRequest + context.Request.RequestId));
            }
        }

        /// <summary>
        /// Contains the logic responsible for processing logout responses.
        /// Note: this handler is not used when the OpenID Connect request is not initially handled by OWIN.
        /// </summary>
        public sealed class ProcessQueryResponse : IOpenIddictServerHandler<ApplyLogoutResponseContext>
        {
            /// <summary>
            /// Gets the default descriptor definition assigned to this handler.
            /// </summary>
            public static OpenIddictServerHandlerDescriptor Descriptor { get; }
                = OpenIddictServerHandlerDescriptor.CreateBuilder<ApplyLogoutResponseContext>()
                    .AddFilter<RequireOwinRequest>()
                    .UseSingletonHandler<ProcessQueryResponse>()
                    .SetOrder(250_000)
                    .SetType(OpenIddictServerHandlerType.BuiltIn)
                    .Build();

            /// <inheritdoc/>
            public ValueTask HandleAsync(ApplyLogoutResponseContext context)
            {
                if (context is null)
                {
                    throw new ArgumentNullException(nameof(context));
                }

                // This handler only applies to OWIN requests. If The OWIN request cannot be resolved,
                // this may indicate that the request was incorrectly processed by another server stack.
                var response = context.Transaction.GetOwinRequest()?.Context.Response ??
                    throw new InvalidOperationException(SR.GetResourceString(SR.ID0120));

                if (string.IsNullOrEmpty(context.PostLogoutRedirectUri))
                {
                    return default;
                }

                context.Logger.LogInformation(SR.GetResourceString(SR.ID6151), context.PostLogoutRedirectUri, context.Response);

                var location = context.PostLogoutRedirectUri;

                // Note: while initially not allowed by the core OAuth 2.0 specification, multiple parameters
                // with the same name are used by derived drafts like the OAuth 2.0 token exchange specification.
                // For consistency, multiple parameters with the same name are also supported by this endpoint.
                foreach (var (key, value) in
                    from parameter in context.Response.GetParameters()
                    let values = (string?[]?) parameter.Value
                    where values is not null
                    from value in values
                    where !string.IsNullOrEmpty(value)
                    select (parameter.Key, Value: value))
                {
                    location = WebUtilities.AddQueryString(location, key, value);
                }

                response.Redirect(location);
                context.HandleRequest();

                return default;
            }
        }

        /// <summary>
        /// Contains the logic responsible for processing logout responses that should trigger a host redirection.
        /// Note: this handler is not used when the OpenID Connect request is not initially handled by OWIN.
        /// </summary>
        public sealed class ProcessHostRedirectionResponse : IOpenIddictServerHandler<ApplyLogoutResponseContext>
        {
            /// <summary>
            /// Gets the default descriptor definition assigned to this handler.
            /// </summary>
            public static OpenIddictServerHandlerDescriptor Descriptor { get; }
                = OpenIddictServerHandlerDescriptor.CreateBuilder<ApplyLogoutResponseContext>()
                    .AddFilter<RequireOwinRequest>()
                    .UseSingletonHandler<ProcessHostRedirectionResponse>()
                    .SetOrder(ProcessPassthroughErrorResponse<ApplyLogoutResponseContext, RequireLogoutEndpointPassthroughEnabled>.Descriptor.Order + 250)
                    .SetType(OpenIddictServerHandlerType.BuiltIn)
                    .Build();

            /// <inheritdoc/>
            public ValueTask HandleAsync(ApplyLogoutResponseContext context)
            {
                if (context is null)
                {
                    throw new ArgumentNullException(nameof(context));
                }

                // This handler only applies to OWIN requests. If The OWIN request cannot be resolved,
                // this may indicate that the request was incorrectly processed by another server stack.
                var response = context.Transaction.GetOwinRequest()?.Context.Response ??
                    throw new InvalidOperationException(SR.GetResourceString(SR.ID0120));

                // Note: this handler only executes if no post_logout_redirect_uri was specified
                // and if the response doesn't correspond to an error, that must be handled locally.
                if (!string.IsNullOrEmpty(context.PostLogoutRedirectUri) ||
                    !string.IsNullOrEmpty(context.Response.Error))
                {
                    return default;
                }

                var properties = context.Transaction.GetProperty<AuthenticationProperties>(typeof(AuthenticationProperties).FullName!);
                if (properties is not null && !string.IsNullOrEmpty(properties.RedirectUri))
                {
                    response.Redirect(properties.RedirectUri);

                    context.Logger.LogInformation(SR.GetResourceString(SR.ID6144));
                    context.HandleRequest();
                }

                return default;
            }
        }
    }
}

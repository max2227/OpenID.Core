﻿/*
 * Licensed under the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
 * See https://github.com/openiddict/openiddict-core for more information concerning
 * the license and the contributors participating to this project.
 */

using System.Collections.Immutable;
using System.Diagnostics;
using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Extensions;
using static OpenIddict.Abstractions.OpenIddictExceptions;
using static OpenIddict.Client.OpenIddictClientModels;

namespace OpenIddict.Client;

/// <summary>
/// Provides high-level APIs for performing various authentication operations.
/// </summary>
public class OpenIddictClientService
{
    private readonly IServiceProvider _provider;

    /// <summary>
    /// Creates a new instance of the <see cref="OpenIddictClientService"/> class.
    /// </summary>
    /// <param name="provider">The service provider.</param>
    public OpenIddictClientService(IServiceProvider provider)
        => _provider = provider ?? throw new ArgumentNullException(nameof(provider));

    /// <summary>
    /// Gets all the client registrations that were registered in the client options.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
    /// <returns>The client registrations that were registered in the client options.</returns>
    public virtual ValueTask<ImmutableArray<OpenIddictClientRegistration>> GetClientRegistrationsAsync(
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return new(Task.FromCanceled<ImmutableArray<OpenIddictClientRegistration>>(cancellationToken));
        }

        var options = _provider.GetRequiredService<IOptionsMonitor<OpenIddictClientOptions>>();
        return new(options.CurrentValue.Registrations switch
        {
            [  ]               => ImmutableArray.Create<OpenIddictClientRegistration>(),
            [..] registrations => registrations.ToImmutableArray()
        });
    }

    /// <summary>
    /// Resolves the client registration associated with the specified issuer <paramref name="uri"/>.
    /// </summary>
    /// <param name="uri">The issuer.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
    /// <returns>The <see cref="OpenIddictClientRegistration"/> associated with the specified issuer <paramref name="uri"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="uri"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// No <see cref="OpenIddictClientRegistration"/> was registered with the specified issuer <paramref name="uri"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Multiple <see cref="OpenIddictClientRegistration"/> instances share the same issuer <paramref name="uri"/>.
    /// </exception>
    public virtual ValueTask<OpenIddictClientRegistration> GetClientRegistrationByIssuerAsync(
        Uri uri, CancellationToken cancellationToken = default)
    {
        if (uri is null)
        {
            throw new ArgumentNullException(nameof(uri));
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return new(Task.FromCanceled<OpenIddictClientRegistration>(cancellationToken));
        }

        var options = _provider.GetRequiredService<IOptionsMonitor<OpenIddictClientOptions>>();

        return options.CurrentValue.Registrations.FindAll(registration => registration.Issuer == uri) switch
        {
            [var registration] => new(registration),

            [] => new(Task.FromException<OpenIddictClientRegistration>(
                new InvalidOperationException(SR.GetResourceString(SR.ID0292)))),

            _ => new(Task.FromException<OpenIddictClientRegistration>(
                new InvalidOperationException(SR.GetResourceString(SR.ID0404))))
        };
    }

    /// <summary>
    /// Resolves the client registration associated with the specified provider <paramref name="name"/>.
    /// </summary>
    /// <param name="name">The provider name.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
    /// <returns>The <see cref="OpenIddictClientRegistration"/> associated with the specified provider <paramref name="name"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// No <see cref="OpenIddictClientRegistration"/> was registered with the specified provider <paramref name="name"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Multiple <see cref="OpenIddictClientRegistration"/> instances share the same provider <paramref name="name"/>.
    /// </exception>
    public virtual ValueTask<OpenIddictClientRegistration> GetClientRegistrationByProviderNameAsync(
        string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException(SR.FormatID0366(nameof(name)), nameof(name));
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return new(Task.FromCanceled<OpenIddictClientRegistration>(cancellationToken));
        }

        var options = _provider.GetRequiredService<IOptionsMonitor<OpenIddictClientOptions>>();

        return options.CurrentValue.Registrations.FindAll(registration => string.Equals(
            registration.ProviderName, name, StringComparison.Ordinal)) switch
        {
            [var registration] => new(registration),

            [] => new(Task.FromException<OpenIddictClientRegistration>(
                new InvalidOperationException(SR.GetResourceString(SR.ID0397)))),

            _ => new(Task.FromException<OpenIddictClientRegistration>(
                new InvalidOperationException(SR.GetResourceString(SR.ID0409))))
        };
    }

    /// <summary>
    /// Resolves the client registration associated with the specified <paramref name="identifier"/>.
    /// </summary>
    /// <param name="identifier">The registration identifier.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
    /// <returns>The <see cref="OpenIddictClientRegistration"/> associated with the specified <paramref name="identifier"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="identifier"/> is <see langword="null"/> or empty.</exception>
    /// <exception cref="InvalidOperationException">
    /// No <see cref="OpenIddictClientRegistration"/> was registered with the specified <paramref name="identifier"/>.
    /// </exception>
    public virtual ValueTask<OpenIddictClientRegistration> GetClientRegistrationByIdAsync(
        string identifier, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(identifier))
        {
            throw new ArgumentException(SR.FormatID0366(nameof(identifier)), nameof(identifier));
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return new(Task.FromCanceled<OpenIddictClientRegistration>(cancellationToken));
        }

        var options = _provider.GetRequiredService<IOptionsMonitor<OpenIddictClientOptions>>();

        return new(options.CurrentValue.Registrations.Find(registration => string.Equals(
            registration.RegistrationId, identifier, StringComparison.Ordinal)) ??
            throw new InvalidOperationException(SR.GetResourceString(SR.ID0410)));
    }

    /// <summary>
    /// Resolves the server configuration associated with the specified issuer <paramref name="uri"/>.
    /// </summary>
    /// <param name="uri">The issuer.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
    /// <returns>The <see cref="OpenIddictConfiguration"/> associated with the specified issuer <paramref name="uri"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="uri"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// No <see cref="OpenIddictClientRegistration"/> was registered with the specified issuer <paramref name="uri"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Multiple <see cref="OpenIddictClientRegistration"/> instances share the same issuer <paramref name="uri"/>.
    /// </exception>
    public virtual async ValueTask<OpenIddictConfiguration> GetServerConfigurationByIssuerAsync(
        Uri uri, CancellationToken cancellationToken = default)
    {
        if (uri is null)
        {
            throw new ArgumentNullException(nameof(uri));
        }

        var registration = await GetClientRegistrationByIssuerAsync(uri, cancellationToken);
        if (registration.ConfigurationManager is null)
        {
            throw new InvalidOperationException(SR.GetResourceString(SR.ID0422));
        }

        return await registration.ConfigurationManager
            .GetConfigurationAsync(cancellationToken)
            .WaitAsync(cancellationToken) ??
            throw new InvalidOperationException(SR.GetResourceString(SR.ID0140));
    }

    /// <summary>
    /// Resolves the server configuration associated with the specified provider <paramref name="name"/>.
    /// </summary>
    /// <param name="name">The provider name.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
    /// <returns>The <see cref="OpenIddictConfiguration"/> associated with the specified provider <paramref name="name"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// No <see cref="OpenIddictClientRegistration"/> was registered with the specified provider <paramref name="name"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Multiple <see cref="OpenIddictClientRegistration"/> instances share the same provider <paramref name="name"/>.
    /// </exception>
    public virtual async ValueTask<OpenIddictConfiguration> GetServerConfigurationByProviderNameAsync(
        string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException(SR.FormatID0366(nameof(name)), nameof(name));
        }

        var registration = await GetClientRegistrationByProviderNameAsync(name, cancellationToken);
        if (registration.ConfigurationManager is null)
        {
            throw new InvalidOperationException(SR.GetResourceString(SR.ID0422));
        }

        return await registration.ConfigurationManager
            .GetConfigurationAsync(cancellationToken)
            .WaitAsync(cancellationToken) ??
            throw new InvalidOperationException(SR.GetResourceString(SR.ID0140));
    }

    /// <summary>
    /// Resolves the server configuration associated with the specified registration <paramref name="identifier"/>.
    /// </summary>
    /// <param name="identifier">The registration identifier.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
    /// <returns>The <see cref="OpenIddictConfiguration"/> associated with the specified <paramref name="identifier"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="identifier"/> is <see langword="null"/> or empty.</exception>
    /// <exception cref="InvalidOperationException">
    /// No <see cref="OpenIddictClientRegistration"/> was registered with the specified <paramref name="identifier"/>.
    /// </exception>
    public virtual async ValueTask<OpenIddictConfiguration> GetServerConfigurationByRegistrationIdAsync(
        string identifier, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(identifier))
        {
            throw new ArgumentException(SR.FormatID0366(nameof(identifier)), nameof(identifier));
        }

        var registration = await GetClientRegistrationByIdAsync(identifier, cancellationToken);
        if (registration.ConfigurationManager is null)
        {
            throw new InvalidOperationException(SR.GetResourceString(SR.ID0422));
        }

        return await registration.ConfigurationManager
            .GetConfigurationAsync(cancellationToken)
            .WaitAsync(cancellationToken) ??
            throw new InvalidOperationException(SR.GetResourceString(SR.ID0140));
    }

    /// <summary>
    /// Completes the interactive authentication demand corresponding to the specified nonce.
    /// </summary>
    /// <param name="request">The interactive authentication request.</param>
    /// <returns>The interactive authentication result.</returns>
    public async ValueTask<InteractiveAuthenticationResult> AuthenticateInteractivelyAsync(InteractiveAuthenticationRequest request)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        request.CancellationToken.ThrowIfCancellationRequested();

        // Note: this service is registered as a singleton service. As such, it cannot
        // directly depend on scoped services like the validation provider. To work around
        // this limitation, a scope is manually created for each method to this service.
        var scope = _provider.CreateScope();

        // Note: a try/finally block is deliberately used here to ensure the service scope
        // can be disposed of asynchronously if it implements IAsyncDisposable.
        try
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<IOpenIddictClientDispatcher>();
            var factory = scope.ServiceProvider.GetRequiredService<IOpenIddictClientFactory>();

            var transaction = await factory.CreateTransactionAsync();

            var context = new ProcessAuthenticationContext(transaction)
            {
                CancellationToken = request.CancellationToken,
                Nonce = request.Nonce
            };

            if (request.Properties is { Count: > 0 })
            {
                foreach (var property in request.Properties)
                {
                    context.Properties[property.Key] = property.Value;
                }
            }

            await dispatcher.DispatchAsync(context);

            if (context.IsRejected)
            {
                throw new ProtocolException(
                    message: SR.GetResourceString(SR.ID0374),
                    context.Error, context.ErrorDescription, context.ErrorUri);
            }

            else
            {
                Debug.Assert(context.Registration.Issuer is { IsAbsoluteUri: true }, SR.GetResourceString(SR.ID4013));

                return new()
                {
                    AuthorizationCode = context.AuthorizationCode,
                    AuthorizationResponse = context.Request is not null ? new(context.Request.GetParameters()) : new(),
                    BackchannelAccessToken = context.BackchannelAccessToken,
                    BackchannelAccessTokenExpirationDate = context.BackchannelAccessTokenExpirationDate,
                    BackchannelIdentityToken = context.BackchannelIdentityToken,
                    BackchannelIdentityTokenPrincipal = context.BackchannelIdentityTokenPrincipal,
                    FrontchannelAccessToken = context.FrontchannelAccessToken,
                    FrontchannelAccessTokenExpirationDate = context.FrontchannelAccessTokenExpirationDate,
                    FrontchannelIdentityToken = context.FrontchannelIdentityToken,
                    FrontchannelIdentityTokenPrincipal = context.FrontchannelIdentityTokenPrincipal,
                    Principal = context.MergedPrincipal,
                    Properties = context.Properties,
                    RefreshToken = context.RefreshToken,
                    StateTokenPrincipal = context.StateTokenPrincipal,
                    TokenResponse = context.TokenResponse ?? new(),
                    UserinfoTokenPrincipal = context.UserinfoTokenPrincipal
                };
            }
        }

        finally
        {
            if (scope is IAsyncDisposable disposable)
            {
                await disposable.DisposeAsync();
            }

            else
            {
                scope.Dispose();
            }
        }
    }

    /// <summary>
    /// Initiates an interactive user authentication demand.
    /// </summary>
    /// <param name="request">The interactive challenge request.</param>
    /// <returns>The interactive challenge result.</returns>
    public async ValueTask<InteractiveChallengeResult> ChallengeInteractivelyAsync(InteractiveChallengeRequest request)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        request.CancellationToken.ThrowIfCancellationRequested();

        // Note: this service is registered as a singleton service. As such, it cannot
        // directly depend on scoped services like the validation provider. To work around
        // this limitation, a scope is manually created for each method to this service.
        var scope = _provider.CreateScope();

        // Note: a try/finally block is deliberately used here to ensure the service scope
        // can be disposed of asynchronously if it implements IAsyncDisposable.
        try
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<IOpenIddictClientDispatcher>();
            var factory = scope.ServiceProvider.GetRequiredService<IOpenIddictClientFactory>();

            var transaction = await factory.CreateTransactionAsync();

            var context = new ProcessChallengeContext(transaction)
            {
                CancellationToken = request.CancellationToken,
                Issuer = request.Issuer,
                Principal = new ClaimsPrincipal(new ClaimsIdentity()),
                ProviderName = request.ProviderName,
                RegistrationId = request.RegistrationId,
                Request = request.AdditionalAuthorizationRequestParameters
                    is Dictionary<string, OpenIddictParameter> parameters ? new(parameters) : new(),
            };

            if (request.Scopes is { Count: > 0 })
            {
                context.Scopes.UnionWith(request.Scopes);
            }

            if (request.Properties is { Count: > 0 })
            {
                foreach (var property in request.Properties)
                {
                    context.Properties[property.Key] = property.Value;
                }
            }

            await dispatcher.DispatchAsync(context);

            if (context.IsRejected)
            {
                throw new ProtocolException(
                    message: SR.GetResourceString(SR.ID0374),
                    context.Error, context.ErrorDescription, context.ErrorUri);
            }

            if (string.IsNullOrEmpty(context.Nonce))
            {
                throw new InvalidOperationException(SR.GetResourceString(SR.ID0352));
            }

            return new()
            {
                Nonce = context.Nonce,
                Properties = context.Properties
            };
        }

        finally
        {
            if (scope is IAsyncDisposable disposable)
            {
                await disposable.DisposeAsync();
            }

            else
            {
                scope.Dispose();
            }
        }
    }

    /// <summary>
    /// Authenticates using the client credentials grant.
    /// </summary>
    /// <param name="request">The client credentials authentication request.</param>
    /// <returns>The client credentials authentication result.</returns>
    public async ValueTask<ClientCredentialsAuthenticationResult> AuthenticateWithClientCredentialsAsync(
        ClientCredentialsAuthenticationRequest request)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        request.CancellationToken.ThrowIfCancellationRequested();

        // Note: this service is registered as a singleton service. As such, it cannot
        // directly depend on scoped services like the validation provider. To work around
        // this limitation, a scope is manually created for each method to this service.
        var scope = _provider.CreateScope();

        // Note: a try/finally block is deliberately used here to ensure the service scope
        // can be disposed of asynchronously if it implements IAsyncDisposable.
        try
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<IOpenIddictClientDispatcher>();
            var factory = scope.ServiceProvider.GetRequiredService<IOpenIddictClientFactory>();
            var transaction = await factory.CreateTransactionAsync();

            var context = new ProcessAuthenticationContext(transaction)
            {
                CancellationToken = request.CancellationToken,
                GrantType = GrantTypes.ClientCredentials,
                Issuer = request.Issuer,
                ProviderName = request.ProviderName,
                RegistrationId = request.RegistrationId,
                TokenRequest = request.AdditionalTokenRequestParameters
                    is Dictionary<string, OpenIddictParameter> parameters ? new(parameters) : new(),
            };

            if (request.Scopes is { Count: > 0 })
            {
                context.Scopes.UnionWith(request.Scopes);
            }

            if (request.Properties is { Count: > 0 })
            {
                foreach (var property in request.Properties)
                {
                    context.Properties[property.Key] = property.Value;
                }
            }

            await dispatcher.DispatchAsync(context);

            if (context.IsRejected)
            {
                throw new ProtocolException(
                    SR.FormatID0319(context.Error, context.ErrorDescription, context.ErrorUri),
                    context.Error, context.ErrorDescription, context.ErrorUri);
            }

            Debug.Assert(context.Registration.Issuer is { IsAbsoluteUri: true }, SR.GetResourceString(SR.ID4013));
            Debug.Assert(context.TokenResponse is not null, SR.GetResourceString(SR.ID4007));

            return new()
            {
                AccessToken = context.BackchannelAccessToken!,
                AccessTokenExpirationDate = context.BackchannelAccessTokenExpirationDate,
                IdentityToken = context.BackchannelIdentityToken,
                IdentityTokenPrincipal = context.BackchannelIdentityTokenPrincipal,
                Principal = context.MergedPrincipal,
                Properties = context.Properties,
                RefreshToken = context.RefreshToken,
                TokenResponse = context.TokenResponse,
                UserinfoToken = context.UserinfoToken,
                UserinfoTokenPrincipal = context.UserinfoTokenPrincipal
            };
        }

        finally
        {
            if (scope is IAsyncDisposable disposable)
            {
                await disposable.DisposeAsync();
            }

            else
            {
                scope.Dispose();
            }
        }
    }

    /// <summary>
    /// Authenticates using the specified device authorization code.
    /// </summary>
    /// <param name="request">The device authentication request.</param>
    /// <returns>The device authentication result.</returns>
    public async ValueTask<DeviceAuthenticationResult> AuthenticateWithDeviceAsync(DeviceAuthenticationRequest request)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        using var source = CancellationTokenSource.CreateLinkedTokenSource(request.CancellationToken);
        source.CancelAfter(request.Timeout);

        var interval = request.Interval;

        while (true)
        {
            source.Token.ThrowIfCancellationRequested();

            try
            {
                // Note: this service is registered as a singleton service. As such, it cannot
                // directly depend on scoped services like the validation provider. To work around
                // this limitation, a scope is manually created for each method to this service.
                var scope = _provider.CreateScope();

                // Note: a try/finally block is deliberately used here to ensure the service scope
                // can be disposed of asynchronously if it implements IAsyncDisposable.
                try
                {
                    var dispatcher = scope.ServiceProvider.GetRequiredService<IOpenIddictClientDispatcher>();
                    var factory = scope.ServiceProvider.GetRequiredService<IOpenIddictClientFactory>();

                    var transaction = await factory.CreateTransactionAsync();

                    var context = new ProcessAuthenticationContext(transaction)
                    {
                        CancellationToken = source.Token,
                        DeviceCode = request.DeviceCode,
                        DisableUserinfoRetrieval = request.DisableUserinfo,
                        DisableUserinfoValidation = request.DisableUserinfo,
                        GrantType = GrantTypes.DeviceCode,
                        Issuer = request.Issuer,
                        ProviderName = request.ProviderName,
                        RegistrationId = request.RegistrationId,
                        TokenRequest = request.AdditionalTokenRequestParameters
                            is Dictionary<string, OpenIddictParameter> parameters ? new(parameters) : new(),
                    };

                    if (request.Scopes is { Count: > 0 })
                    {
                        context.Scopes.UnionWith(request.Scopes);
                    }

                    if (request.Properties is { Count: > 0 })
                    {
                        foreach (var property in request.Properties)
                        {
                            context.Properties[property.Key] = property.Value;
                        }
                    }

                    await dispatcher.DispatchAsync(context);

                    if (context.IsRejected)
                    {
                        throw new ProtocolException(
                            message: SR.GetResourceString(SR.ID0374),
                            context.Error, context.ErrorDescription, context.ErrorUri);
                    }

                    else
                    {
                        Debug.Assert(context.Registration.Issuer is { IsAbsoluteUri: true }, SR.GetResourceString(SR.ID4013));

                        return new()
                        {
                            AccessToken = context.BackchannelAccessToken!,
                            AccessTokenExpirationDate = context.BackchannelAccessTokenExpirationDate,
                            IdentityToken = context.BackchannelIdentityToken,
                            IdentityTokenPrincipal = context.BackchannelIdentityTokenPrincipal,
                            Principal = context.MergedPrincipal,
                            Properties = context.Properties,
                            RefreshToken = context.RefreshToken,
                            TokenResponse = context.TokenResponse ?? new(),
                            UserinfoToken = context.UserinfoToken,
                            UserinfoTokenPrincipal = context.UserinfoTokenPrincipal
                        };
                    }
                }

                finally
                {
                    if (scope is IAsyncDisposable disposable)
                    {
                        await disposable.DisposeAsync();
                    }

                    else
                    {
                        scope.Dispose();
                    }
                }
            }

            catch (ProtocolException exception) when (exception.Error is Errors.AuthorizationPending)
            {
                // Default to a standard 5-second interval if no explicit value was configured.
                // See https://www.rfc-editor.org/rfc/rfc8628#section-3.5 for more information.
                await Task.Delay(interval, source.Token);
            }

            catch (ProtocolException exception) when (exception.Error is Errors.SlowDown)
            {
                // When the error indicates that token requests are sent too frequently,
                // slow down the token redeeming process by increasing the interval.
                //
                // See https://www.rfc-editor.org/rfc/rfc8628#section-3.5 for more information.
                await Task.Delay(interval += TimeSpan.FromSeconds(5), source.Token);
            }
        }
    }

    /// <summary>
    /// Initiates a device authorization process.
    /// </summary>
    /// <param name="request">The device challenge request.</param>
    /// <returns>The device challenge result.</returns>
    public async ValueTask<DeviceChallengeResult> ChallengeUsingDeviceAsync(DeviceChallengeRequest request)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        request.CancellationToken.ThrowIfCancellationRequested();

        // Note: this service is registered as a singleton service. As such, it cannot
        // directly depend on scoped services like the validation provider. To work around
        // this limitation, a scope is manually created for each method to this service.
        var scope = _provider.CreateScope();

        // Note: a try/finally block is deliberately used here to ensure the service scope
        // can be disposed of asynchronously if it implements IAsyncDisposable.
        try
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<IOpenIddictClientDispatcher>();
            var factory = scope.ServiceProvider.GetRequiredService<IOpenIddictClientFactory>();

            var transaction = await factory.CreateTransactionAsync();

            var context = new ProcessChallengeContext(transaction)
            {
                CancellationToken = request.CancellationToken,
                DeviceAuthorizationRequest = request.AdditionalDeviceAuthorizationRequestParameters
                    is Dictionary<string, OpenIddictParameter> parameters ? new(parameters) : new(),
                GrantType = GrantTypes.DeviceCode,
                Issuer = request.Issuer,
                Principal = new ClaimsPrincipal(new ClaimsIdentity()),
                ProviderName = request.ProviderName,
                RegistrationId = request.RegistrationId
            };

            if (request.Scopes is { Count: > 0 })
            {
                context.Scopes.UnionWith(request.Scopes);
            }

            if (request.Properties is { Count: > 0 })
            {
                foreach (var property in request.Properties)
                {
                    context.Properties[property.Key] = property.Value;
                }
            }

            await dispatcher.DispatchAsync(context);

            if (context.IsRejected)
            {
                throw new ProtocolException(
                    message: SR.GetResourceString(SR.ID0374),
                    context.Error, context.ErrorDescription, context.ErrorUri);
            }

            return new()
            {
                DeviceAuthorizationResponse = context.DeviceAuthorizationResponse ?? new(),
                DeviceCode = context.DeviceCode!,
                ExpiresIn = TimeSpan.FromSeconds((double) context.DeviceAuthorizationResponse?.ExpiresIn!),
                Interval = TimeSpan.FromSeconds((long?) context.DeviceAuthorizationResponse[Parameters.Interval] ?? 5),
                Properties = context.Properties,
                UserCode = context.UserCode!,
                VerificationUri = new Uri(context.DeviceAuthorizationResponse?.VerificationUri!, UriKind.Absolute),
                VerificationUriComplete = context.DeviceAuthorizationResponse?.VerificationUriComplete
                    is string value ? new Uri(value, UriKind.Absolute) : null
            };
        }

        finally
        {
            if (scope is IAsyncDisposable disposable)
            {
                await disposable.DisposeAsync();
            }

            else
            {
                scope.Dispose();
            }
        }
    }

    /// <summary>
    /// Authenticates using the resource owner password credentials grant.
    /// </summary>
    /// <param name="request">The resource owner password credentials authentication request.</param>
    /// <returns>The resource owner password credentials authentication result.</returns>
    public async ValueTask<PasswordAuthenticationResult> AuthenticateWithPasswordAsync(PasswordAuthenticationRequest request)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        request.CancellationToken.ThrowIfCancellationRequested();

        // Note: this service is registered as a singleton service. As such, it cannot
        // directly depend on scoped services like the validation provider. To work around
        // this limitation, a scope is manually created for each method to this service.
        var scope = _provider.CreateScope();

        // Note: a try/finally block is deliberately used here to ensure the service scope
        // can be disposed of asynchronously if it implements IAsyncDisposable.
        try
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<IOpenIddictClientDispatcher>();
            var factory = scope.ServiceProvider.GetRequiredService<IOpenIddictClientFactory>();
            var transaction = await factory.CreateTransactionAsync();

            var context = new ProcessAuthenticationContext(transaction)
            {
                CancellationToken = request.CancellationToken,
                DisableUserinfoRetrieval = request.DisableUserinfo,
                DisableUserinfoValidation = request.DisableUserinfo,
                GrantType = GrantTypes.Password,
                Issuer = request.Issuer,
                Password = request.Password,
                ProviderName = request.ProviderName,
                RegistrationId = request.RegistrationId,
                TokenRequest = request.AdditionalTokenRequestParameters
                    is Dictionary<string, OpenIddictParameter> parameters ? new(parameters) : new(),
                Username = request.Username
            };

            if (request.Scopes is { Count: > 0 })
            {
                context.Scopes.UnionWith(request.Scopes);
            }

            if (request.Properties is { Count: > 0 })
            {
                foreach (var property in request.Properties)
                {
                    context.Properties[property.Key] = property.Value;
                }
            }

            await dispatcher.DispatchAsync(context);

            if (context.IsRejected)
            {
                throw new ProtocolException(
                    SR.FormatID0319(context.Error, context.ErrorDescription, context.ErrorUri),
                    context.Error, context.ErrorDescription, context.ErrorUri);
            }

            Debug.Assert(context.Registration.Issuer is { IsAbsoluteUri: true }, SR.GetResourceString(SR.ID4013));
            Debug.Assert(context.TokenResponse is not null, SR.GetResourceString(SR.ID4007));

            return new()
            {
                AccessToken = context.BackchannelAccessToken!,
                AccessTokenExpirationDate = context.BackchannelAccessTokenExpirationDate,
                IdentityToken = context.BackchannelIdentityToken,
                IdentityTokenPrincipal = context.BackchannelIdentityTokenPrincipal,
                Principal = context.MergedPrincipal,
                Properties = context.Properties,
                RefreshToken = context.RefreshToken,
                TokenResponse = context.TokenResponse,
                UserinfoToken = context.UserinfoToken,
                UserinfoTokenPrincipal = context.UserinfoTokenPrincipal
            };
        }

        finally
        {
            if (scope is IAsyncDisposable disposable)
            {
                await disposable.DisposeAsync();
            }

            else
            {
                scope.Dispose();
            }
        }
    }

    /// <summary>
    /// Authenticates using the specified refresh token.
    /// </summary>
    /// <param name="request">The refresh token authentication request.</param>
    /// <returns>The refresh token authentication result.</returns>
    public async ValueTask<RefreshTokenAuthenticationResult> AuthenticateWithRefreshTokenAsync(
        RefreshTokenAuthenticationRequest request)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        request.CancellationToken.ThrowIfCancellationRequested();

        // Note: this service is registered as a singleton service. As such, it cannot
        // directly depend on scoped services like the validation provider. To work around
        // this limitation, a scope is manually created for each method to this service.
        var scope = _provider.CreateScope();

        // Note: a try/finally block is deliberately used here to ensure the service scope
        // can be disposed of asynchronously if it implements IAsyncDisposable.
        try
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<IOpenIddictClientDispatcher>();
            var factory = scope.ServiceProvider.GetRequiredService<IOpenIddictClientFactory>();
            var transaction = await factory.CreateTransactionAsync();

            var context = new ProcessAuthenticationContext(transaction)
            {
                CancellationToken = request.CancellationToken,
                DisableUserinfoRetrieval = request.DisableUserinfo,
                DisableUserinfoValidation = request.DisableUserinfo,
                GrantType = GrantTypes.RefreshToken,
                Issuer = request.Issuer,
                ProviderName = request.ProviderName,
                RefreshToken = request.RefreshToken,
                RegistrationId = request.RegistrationId,
                TokenRequest = request.AdditionalTokenRequestParameters
                    is Dictionary<string, OpenIddictParameter> parameters ? new(parameters) : new(),
            };

            if (request.Scopes is { Count: > 0 })
            {
                context.Scopes.UnionWith(request.Scopes);
            }

            if (request.Properties is { Count: > 0 })
            {
                foreach (var property in request.Properties)
                {
                    context.Properties[property.Key] = property.Value;
                }
            }

            await dispatcher.DispatchAsync(context);

            if (context.IsRejected)
            {
                throw new ProtocolException(
                    SR.FormatID0319(context.Error, context.ErrorDescription, context.ErrorUri),
                    context.Error, context.ErrorDescription, context.ErrorUri);
            }

            Debug.Assert(context.Registration.Issuer is { IsAbsoluteUri: true }, SR.GetResourceString(SR.ID4013));
            Debug.Assert(context.TokenResponse is not null, SR.GetResourceString(SR.ID4007));

            return new()
            {
                AccessToken = context.BackchannelAccessToken!,
                AccessTokenExpirationDate = context.BackchannelAccessTokenExpirationDate,
                IdentityToken = context.BackchannelIdentityToken,
                IdentityTokenPrincipal = context.BackchannelIdentityTokenPrincipal,
                Principal = context.MergedPrincipal,
                Properties = context.Properties,
                RefreshToken = context.RefreshToken,
                TokenResponse = context.TokenResponse,
                UserinfoToken = context.UserinfoToken,
                UserinfoTokenPrincipal = context.UserinfoTokenPrincipal
            };
        }

        finally
        {
            if (scope is IAsyncDisposable disposable)
            {
                await disposable.DisposeAsync();
            }

            else
            {
                scope.Dispose();
            }
        }
    }

    /// <summary>
    /// Introspects the specified token.
    /// </summary>
    /// <param name="request">The introspection request.</param>
    /// <returns>The introspection result.</returns>
    public async ValueTask<IntrospectionResult> IntrospectTokenAsync(IntrospectionRequest request)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        request.CancellationToken.ThrowIfCancellationRequested();

        // Note: this service is registered as a singleton service. As such, it cannot
        // directly depend on scoped services like the validation provider. To work around
        // this limitation, a scope is manually created for each method to this service.
        var scope = _provider.CreateScope();

        // Note: a try/finally block is deliberately used here to ensure the service scope
        // can be disposed of asynchronously if it implements IAsyncDisposable.
        try
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<IOpenIddictClientDispatcher>();
            var factory = scope.ServiceProvider.GetRequiredService<IOpenIddictClientFactory>();
            var transaction = await factory.CreateTransactionAsync();

            var context = new ProcessIntrospectionContext(transaction)
            {
                CancellationToken = request.CancellationToken,
                IntrospectionRequest = request.AdditionalIntrospectionRequestParameters
                    is Dictionary<string, OpenIddictParameter> parameters ? new(parameters) : new(),
                Issuer = request.Issuer,
                ProviderName = request.ProviderName,
                RegistrationId = request.RegistrationId,
                Token = request.Token,
                TokenTypeHint = request.TokenTypeHint
            };

            if (request.Properties is { Count: > 0 })
            {
                foreach (var property in request.Properties)
                {
                    context.Properties[property.Key] = property.Value;
                }
            }

            await dispatcher.DispatchAsync(context);

            if (context.IsRejected)
            {
                throw new ProtocolException(
                    SR.FormatID0428(context.Error, context.ErrorDescription, context.ErrorUri),
                    context.Error, context.ErrorDescription, context.ErrorUri);
            }

            Debug.Assert(context.Registration.Issuer is { IsAbsoluteUri: true }, SR.GetResourceString(SR.ID4013));
            Debug.Assert(context.IntrospectionResponse is not null, SR.GetResourceString(SR.ID4007));

            return new()
            {
                IntrospectionResponse = context.IntrospectionResponse,
                Principal = context.Principal!,
                Properties = context.Properties
            };
        }

        finally
        {
            if (scope is IAsyncDisposable disposable)
            {
                await disposable.DisposeAsync();
            }

            else
            {
                scope.Dispose();
            }
        }
    }

    /// <summary>
    /// Revokes the specified token.
    /// </summary>
    /// <param name="request">The revocation request.</param>
    /// <returns>The revocation result.</returns>
    public async ValueTask<RevocationResult> RevokeTokenAsync(RevocationRequest request)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        request.CancellationToken.ThrowIfCancellationRequested();

        // Note: this service is registered as a singleton service. As such, it cannot
        // directly depend on scoped services like the validation provider. To work around
        // this limitation, a scope is manually created for each method to this service.
        var scope = _provider.CreateScope();

        // Note: a try/finally block is deliberately used here to ensure the service scope
        // can be disposed of asynchronously if it implements IAsyncDisposable.
        try
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<IOpenIddictClientDispatcher>();
            var factory = scope.ServiceProvider.GetRequiredService<IOpenIddictClientFactory>();
            var transaction = await factory.CreateTransactionAsync();

            var context = new ProcessRevocationContext(transaction)
            {
                CancellationToken = request.CancellationToken,
                Issuer = request.Issuer,
                ProviderName = request.ProviderName,
                RegistrationId = request.RegistrationId,
                RevocationRequest = request.AdditionalRevocationRequestParameters
                    is Dictionary<string, OpenIddictParameter> parameters ? new(parameters) : new(),
                Token = request.Token,
                TokenTypeHint = request.TokenTypeHint
            };

            if (request.Properties is { Count: > 0 })
            {
                foreach (var property in request.Properties)
                {
                    context.Properties[property.Key] = property.Value;
                }
            }

            await dispatcher.DispatchAsync(context);

            if (context.IsRejected)
            {
                throw new ProtocolException(
                    SR.FormatID0429(context.Error, context.ErrorDescription, context.ErrorUri),
                    context.Error, context.ErrorDescription, context.ErrorUri);
            }

            Debug.Assert(context.Registration.Issuer is { IsAbsoluteUri: true }, SR.GetResourceString(SR.ID4013));
            Debug.Assert(context.RevocationResponse is not null, SR.GetResourceString(SR.ID4007));

            return new()
            {
                Properties = context.Properties,
                RevocationResponse = context.RevocationResponse
            };
        }

        finally
        {
            if (scope is IAsyncDisposable disposable)
            {
                await disposable.DisposeAsync();
            }

            else
            {
                scope.Dispose();
            }
        }
    }

    /// <summary>
    /// Retrieves the OpenID Connect server configuration from the specified uri.
    /// </summary>
    /// <param name="registration">The client registration.</param>
    /// <param name="uri">The uri of the remote metadata endpoint.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
    /// <returns>The OpenID Connect server configuration retrieved from the remote server.</returns>
    internal async ValueTask<OpenIddictConfiguration> GetConfigurationAsync(
        OpenIddictClientRegistration registration, Uri uri, CancellationToken cancellationToken = default)
    {
        if (registration is null)
        {
            throw new ArgumentNullException(nameof(uri));
        }

        if (uri is null)
        {
            throw new ArgumentNullException(nameof(uri));
        }

        if (!uri.IsAbsoluteUri || !uri.IsWellFormedOriginalString())
        {
            throw new ArgumentException(SR.GetResourceString(SR.ID0144), nameof(uri));
        }

        cancellationToken.ThrowIfCancellationRequested();

        // Note: this service is registered as a singleton service. As such, it cannot
        // directly depend on scoped services like the validation provider. To work around
        // this limitation, a scope is manually created for each method to this service.
        var scope = _provider.CreateScope();

        // Note: a try/finally block is deliberately used here to ensure the service scope
        // can be disposed of asynchronously if it implements IAsyncDisposable.
        try
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<IOpenIddictClientDispatcher>();
            var factory = scope.ServiceProvider.GetRequiredService<IOpenIddictClientFactory>();
            var transaction = await factory.CreateTransactionAsync();

            var request = new OpenIddictRequest();
            request = await PrepareConfigurationRequestAsync();
            request = await ApplyConfigurationRequestAsync();
            var response = await ExtractConfigurationResponseAsync();

            return await HandleConfigurationResponseAsync() ??
                throw new InvalidOperationException(SR.GetResourceString(SR.ID0145));

            async ValueTask<OpenIddictRequest> PrepareConfigurationRequestAsync()
            {
                var context = new PrepareConfigurationRequestContext(transaction)
                {
                    CancellationToken = cancellationToken,
                    RemoteUri = uri,
                    Registration = registration,
                    Request = request
                };

                await dispatcher.DispatchAsync(context);

                if (context.IsRejected)
                {
                    throw new ProtocolException(
                        SR.FormatID0148(context.Error, context.ErrorDescription, context.ErrorUri),
                        context.Error, context.ErrorDescription, context.ErrorUri);
                }

                return context.Request;
            }

            async ValueTask<OpenIddictRequest> ApplyConfigurationRequestAsync()
            {
                var context = new ApplyConfigurationRequestContext(transaction)
                {
                    CancellationToken = cancellationToken,
                    RemoteUri = uri,
                    Registration = registration,
                    Request = request
                };

                await dispatcher.DispatchAsync(context);

                if (context.IsRejected)
                {
                    throw new ProtocolException(
                        SR.FormatID0149(context.Error, context.ErrorDescription, context.ErrorUri),
                        context.Error, context.ErrorDescription, context.ErrorUri);
                }

                context.Logger.LogInformation(SR.GetResourceString(SR.ID6186), context.RemoteUri, context.Request);

                return context.Request;
            }

            async ValueTask<OpenIddictResponse> ExtractConfigurationResponseAsync()
            {
                var context = new ExtractConfigurationResponseContext(transaction)
                {
                    CancellationToken = cancellationToken,
                    RemoteUri = uri,
                    Registration = registration,
                    Request = request
                };

                await dispatcher.DispatchAsync(context);

                if (context.IsRejected)
                {
                    throw new ProtocolException(
                        SR.FormatID0150(context.Error, context.ErrorDescription, context.ErrorUri),
                        context.Error, context.ErrorDescription, context.ErrorUri);
                }

                Debug.Assert(context.Response is not null, SR.GetResourceString(SR.ID4007));

                context.Logger.LogInformation(SR.GetResourceString(SR.ID6187), context.RemoteUri, context.Response);

                return context.Response;
            }

            async ValueTask<OpenIddictConfiguration> HandleConfigurationResponseAsync()
            {
                var context = new HandleConfigurationResponseContext(transaction)
                {
                    CancellationToken = cancellationToken,
                    RemoteUri = uri,
                    Registration = registration,
                    Request = request,
                    Response = response
                };

                await dispatcher.DispatchAsync(context);

                if (context.IsRejected)
                {
                    throw new ProtocolException(
                        SR.FormatID0151(context.Error, context.ErrorDescription, context.ErrorUri),
                        context.Error, context.ErrorDescription, context.ErrorUri);
                }

                return context.Configuration;
            }
        }

        finally
        {
            if (scope is IAsyncDisposable disposable)
            {
                await disposable.DisposeAsync();
            }

            else
            {
                scope.Dispose();
            }
        }
    }

    /// <summary>
    /// Retrieves the security keys exposed by the specified JWKS endpoint.
    /// </summary>
    /// <param name="registration">The client registration.</param>
    /// <param name="uri">The uri of the remote metadata endpoint.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
    /// <returns>The security keys retrieved from the remote server.</returns>
    internal async ValueTask<JsonWebKeySet> GetSecurityKeysAsync(
        OpenIddictClientRegistration registration, Uri uri, CancellationToken cancellationToken = default)
    {
        if (registration is null)
        {
            throw new ArgumentNullException(nameof(registration));
        }

        if (uri is null)
        {
            throw new ArgumentNullException(nameof(uri));
        }

        if (!uri.IsAbsoluteUri || !uri.IsWellFormedOriginalString())
        {
            throw new ArgumentException(SR.GetResourceString(SR.ID0144), nameof(uri));
        }

        cancellationToken.ThrowIfCancellationRequested();

        // Note: this service is registered as a singleton service. As such, it cannot
        // directly depend on scoped services like the validation provider. To work around
        // this limitation, a scope is manually created for each method to this service.
        var scope = _provider.CreateScope();

        // Note: a try/finally block is deliberately used here to ensure the service scope
        // can be disposed of asynchronously if it implements IAsyncDisposable.
        try
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<IOpenIddictClientDispatcher>();
            var factory = scope.ServiceProvider.GetRequiredService<IOpenIddictClientFactory>();
            var transaction = await factory.CreateTransactionAsync();

            var request = new OpenIddictRequest();
            request = await PrepareCryptographyRequestAsync();
            request = await ApplyCryptographyRequestAsync();

            var response = await ExtractCryptographyResponseAsync();

            return await HandleCryptographyResponseAsync() ??
                throw new InvalidOperationException(SR.GetResourceString(SR.ID0147));

            async ValueTask<OpenIddictRequest> PrepareCryptographyRequestAsync()
            {
                var context = new PrepareCryptographyRequestContext(transaction)
                {
                    CancellationToken = cancellationToken,
                    RemoteUri = uri,
                    Registration = registration,
                    Request = request
                };

                await dispatcher.DispatchAsync(context);

                if (context.IsRejected)
                {
                    throw new ProtocolException(
                        SR.FormatID0152(context.Error, context.ErrorDescription, context.ErrorUri),
                        context.Error, context.ErrorDescription, context.ErrorUri);
                }

                return context.Request;
            }

            async ValueTask<OpenIddictRequest> ApplyCryptographyRequestAsync()
            {
                var context = new ApplyCryptographyRequestContext(transaction)
                {
                    CancellationToken = cancellationToken,
                    RemoteUri = uri,
                    Registration = registration,
                    Request = request
                };

                await dispatcher.DispatchAsync(context);

                if (context.IsRejected)
                {
                    throw new ProtocolException(
                        SR.FormatID0153(context.Error, context.ErrorDescription, context.ErrorUri),
                        context.Error, context.ErrorDescription, context.ErrorUri);
                }

                context.Logger.LogInformation(SR.GetResourceString(SR.ID6188), context.RemoteUri, context.Request);

                return context.Request;
            }

            async ValueTask<OpenIddictResponse> ExtractCryptographyResponseAsync()
            {
                var context = new ExtractCryptographyResponseContext(transaction)
                {
                    CancellationToken = cancellationToken,
                    RemoteUri = uri,
                    Registration = registration,
                    Request = request
                };

                await dispatcher.DispatchAsync(context);

                if (context.IsRejected)
                {
                    throw new ProtocolException(
                        SR.FormatID0154(context.Error, context.ErrorDescription, context.ErrorUri),
                        context.Error, context.ErrorDescription, context.ErrorUri);
                }

                Debug.Assert(context.Response is not null, SR.GetResourceString(SR.ID4007));

                context.Logger.LogInformation(SR.GetResourceString(SR.ID6189), context.RemoteUri, context.Response);

                return context.Response;
            }

            async ValueTask<JsonWebKeySet> HandleCryptographyResponseAsync()
            {
                var context = new HandleCryptographyResponseContext(transaction)
                {
                    CancellationToken = cancellationToken,
                    RemoteUri = uri,
                    Registration = registration,
                    Request = request,
                    Response = response
                };

                await dispatcher.DispatchAsync(context);

                if (context.IsRejected)
                {
                    throw new ProtocolException(
                        SR.FormatID0155(context.Error, context.ErrorDescription, context.ErrorUri),
                        context.Error, context.ErrorDescription, context.ErrorUri);
                }

                return context.SecurityKeys;
            }
        }

        finally
        {
            if (scope is IAsyncDisposable disposable)
            {
                await disposable.DisposeAsync();
            }

            else
            {
                scope.Dispose();
            }
        }
    }

    /// <summary>
    /// Sends the device authorization request and retrieves the corresponding response.
    /// </summary>
    /// <param name="registration">The client registration.</param>
    /// <param name="configuration">The server configuration.</param>
    /// <param name="request">The device authorization request.</param>
    /// <param name="uri">The uri of the remote device authorization endpoint.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
    /// <returns>The token response.</returns>
    internal async ValueTask<OpenIddictResponse> SendDeviceAuthorizationRequestAsync(
        OpenIddictClientRegistration registration, OpenIddictConfiguration configuration,
        OpenIddictRequest request, Uri? uri = null, CancellationToken cancellationToken = default)
    {
        if (registration is null)
        {
            throw new ArgumentNullException(nameof(registration));
        }

        if (configuration is null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (uri is null)
        {
            throw new ArgumentNullException(nameof(uri));
        }

        if (!uri.IsAbsoluteUri || !uri.IsWellFormedOriginalString())
        {
            throw new ArgumentException(SR.GetResourceString(SR.ID0144), nameof(uri));
        }

        cancellationToken.ThrowIfCancellationRequested();

        // Note: this service is registered as a singleton service. As such, it cannot
        // directly depend on scoped services like the validation provider. To work around
        // this limitation, a scope is manually created for each method to this service.
        var scope = _provider.CreateScope();

        // Note: a try/finally block is deliberately used here to ensure the service scope
        // can be disposed of asynchronously if it implements IAsyncDisposable.
        try
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<IOpenIddictClientDispatcher>();
            var factory = scope.ServiceProvider.GetRequiredService<IOpenIddictClientFactory>();
            var transaction = await factory.CreateTransactionAsync();

            request = await PrepareDeviceAuthorizationRequestAsync();
            request = await ApplyDeviceAuthorizationRequestAsync();

            var response = await ExtractDeviceAuthorizationResponseAsync();

            return await HandleDeviceAuthorizationResponseAsync();

            async ValueTask<OpenIddictRequest> PrepareDeviceAuthorizationRequestAsync()
            {
                var context = new PrepareDeviceAuthorizationRequestContext(transaction)
                {
                    CancellationToken = cancellationToken,
                    RemoteUri = uri,
                    Configuration = configuration,
                    Registration = registration,
                    Request = request
                };

                await dispatcher.DispatchAsync(context);

                if (context.IsRejected)
                {
                    throw new ProtocolException(
                        SR.FormatID0398(context.Error, context.ErrorDescription, context.ErrorUri),
                        context.Error, context.ErrorDescription, context.ErrorUri);
                }

                return context.Request;
            }

            async ValueTask<OpenIddictRequest> ApplyDeviceAuthorizationRequestAsync()
            {
                var context = new ApplyDeviceAuthorizationRequestContext(transaction)
                {
                    CancellationToken = cancellationToken,
                    RemoteUri = uri,
                    Configuration = configuration,
                    Registration = registration,
                    Request = request
                };

                await dispatcher.DispatchAsync(context);

                if (context.IsRejected)
                {
                    throw new ProtocolException(
                        SR.FormatID0399(context.Error, context.ErrorDescription, context.ErrorUri),
                        context.Error, context.ErrorDescription, context.ErrorUri);
                }

                context.Logger.LogInformation(SR.GetResourceString(SR.ID6217), context.RemoteUri, context.Request);

                return context.Request;
            }

            async ValueTask<OpenIddictResponse> ExtractDeviceAuthorizationResponseAsync()
            {
                var context = new ExtractDeviceAuthorizationResponseContext(transaction)
                {
                    CancellationToken = cancellationToken,
                    RemoteUri = uri,
                    Configuration = configuration,
                    Registration = registration,
                    Request = request
                };

                await dispatcher.DispatchAsync(context);

                if (context.IsRejected)
                {
                    throw new ProtocolException(
                        SR.FormatID0400(context.Error, context.ErrorDescription, context.ErrorUri),
                        context.Error, context.ErrorDescription, context.ErrorUri);
                }

                Debug.Assert(context.Response is not null, SR.GetResourceString(SR.ID4007));

                context.Logger.LogInformation(SR.GetResourceString(SR.ID6218), context.RemoteUri, context.Response);

                return context.Response;
            }

            async ValueTask<OpenIddictResponse> HandleDeviceAuthorizationResponseAsync()
            {
                var context = new HandleDeviceAuthorizationResponseContext(transaction)
                {
                    CancellationToken = cancellationToken,
                    RemoteUri = uri,
                    Configuration = configuration,
                    Registration = registration,
                    Request = request,
                    Response = response
                };

                await dispatcher.DispatchAsync(context);

                if (context.IsRejected)
                {
                    throw new ProtocolException(
                        SR.FormatID0401(context.Error, context.ErrorDescription, context.ErrorUri),
                        context.Error, context.ErrorDescription, context.ErrorUri);
                }

                return context.Response;
            }
        }

        finally
        {
            if (scope is IAsyncDisposable disposable)
            {
                await disposable.DisposeAsync();
            }

            else
            {
                scope.Dispose();
            }
        }
    }

    /// <summary>
    /// Sends the introspection request and retrieves the corresponding response.
    /// </summary>
    /// <param name="registration">The client registration.</param>
    /// <param name="configuration">The server configuration.</param>
    /// <param name="request">The token request.</param>
    /// <param name="uri">The uri of the remote token endpoint.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
    /// <returns>The response and the principal extracted from the introspection response.</returns>
    internal async ValueTask<(OpenIddictResponse, ClaimsPrincipal)> SendIntrospectionRequestAsync(
        OpenIddictClientRegistration registration, OpenIddictConfiguration configuration,
        OpenIddictRequest request, Uri uri, CancellationToken cancellationToken = default)
    {
        if (configuration is null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (uri is null)
        {
            throw new ArgumentNullException(nameof(uri));
        }

        if (!uri.IsAbsoluteUri || !uri.IsWellFormedOriginalString())
        {
            throw new ArgumentException(SR.GetResourceString(SR.ID0144), nameof(uri));
        }

        cancellationToken.ThrowIfCancellationRequested();

        // Note: this service is registered as a singleton service. As such, it cannot
        // directly depend on scoped services like the validation provider. To work around
        // this limitation, a scope is manually created for each method to this service.
        var scope = _provider.CreateScope();

        // Note: a try/finally block is deliberately used here to ensure the service scope
        // can be disposed of asynchronously if it implements IAsyncDisposable.
        try
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<IOpenIddictClientDispatcher>();
            var factory = scope.ServiceProvider.GetRequiredService<IOpenIddictClientFactory>();
            var transaction = await factory.CreateTransactionAsync();

            request = await PrepareIntrospectionRequestAsync();
            request = await ApplyIntrospectionRequestAsync();

            var response = await ExtractIntrospectionResponseAsync();

            return await HandleIntrospectionResponseAsync();

            async ValueTask<OpenIddictRequest> PrepareIntrospectionRequestAsync()
            {
                var context = new PrepareIntrospectionRequestContext(transaction)
                {
                    CancellationToken = cancellationToken,
                    Configuration = configuration,
                    Registration = registration,
                    RemoteUri = uri,
                    Request = request
                };

                await dispatcher.DispatchAsync(context);

                if (context.IsRejected)
                {
                    throw new ProtocolException(
                        SR.FormatID0158(context.Error, context.ErrorDescription, context.ErrorUri),
                        context.Error, context.ErrorDescription, context.ErrorUri);
                }

                return context.Request;
            }

            async ValueTask<OpenIddictRequest> ApplyIntrospectionRequestAsync()
            {
                var context = new ApplyIntrospectionRequestContext(transaction)
                {
                    CancellationToken = cancellationToken,
                    Configuration = configuration,
                    Registration = registration,
                    RemoteUri = uri,
                    Request = request
                };

                await dispatcher.DispatchAsync(context);

                if (context.IsRejected)
                {
                    throw new ProtocolException(
                        SR.FormatID0159(context.Error, context.ErrorDescription, context.ErrorUri),
                        context.Error, context.ErrorDescription, context.ErrorUri);
                }

                context.Logger.LogInformation(SR.GetResourceString(SR.ID6192), context.RemoteUri, context.Request);

                return context.Request;
            }

            async ValueTask<OpenIddictResponse> ExtractIntrospectionResponseAsync()
            {
                var context = new ExtractIntrospectionResponseContext(transaction)
                {
                    CancellationToken = cancellationToken,
                    Configuration = configuration,
                    Registration = registration,
                    RemoteUri = uri,
                    Request = request
                };

                await dispatcher.DispatchAsync(context);

                if (context.IsRejected)
                {
                    throw new ProtocolException(
                        SR.FormatID0160(context.Error, context.ErrorDescription, context.ErrorUri),
                        context.Error, context.ErrorDescription, context.ErrorUri);
                }

                Debug.Assert(context.Response is not null, SR.GetResourceString(SR.ID4007));

                context.Logger.LogInformation(SR.GetResourceString(SR.ID6193), context.RemoteUri, context.Response);

                return context.Response;
            }

            async ValueTask<(OpenIddictResponse, ClaimsPrincipal)> HandleIntrospectionResponseAsync()
            {
                var context = new HandleIntrospectionResponseContext(transaction)
                {
                    CancellationToken = cancellationToken,
                    Configuration = configuration,
                    Registration = registration,
                    RemoteUri = uri,
                    Request = request,
                    Response = response
                };

                await dispatcher.DispatchAsync(context);

                if (context.IsRejected)
                {
                    throw new ProtocolException(
                        SR.FormatID0161(context.Error, context.ErrorDescription, context.ErrorUri),
                        context.Error, context.ErrorDescription, context.ErrorUri);
                }

                Debug.Assert(context.Principal is { Identity: ClaimsIdentity }, SR.GetResourceString(SR.ID4006));

                return (context.Response, context.Principal);
            }
        }

        finally
        {
            if (scope is IAsyncDisposable disposable)
            {
                await disposable.DisposeAsync();
            }

            else
            {
                scope.Dispose();
            }
        }
    }

    /// <summary>
    /// Sends the revocation request and retrieves the corresponding response.
    /// </summary>
    /// <param name="registration">The client registration.</param>
    /// <param name="configuration">The server configuration.</param>
    /// <param name="request">The token request.</param>
    /// <param name="uri">The uri of the remote token endpoint.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
    /// <returns>The response extracted from the revocation response.</returns>
    internal async ValueTask<OpenIddictResponse> SendRevocationRequestAsync(
        OpenIddictClientRegistration registration, OpenIddictConfiguration configuration,
        OpenIddictRequest request, Uri uri, CancellationToken cancellationToken = default)
    {
        if (configuration is null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (uri is null)
        {
            throw new ArgumentNullException(nameof(uri));
        }

        if (!uri.IsAbsoluteUri || !uri.IsWellFormedOriginalString())
        {
            throw new ArgumentException(SR.GetResourceString(SR.ID0144), nameof(uri));
        }

        cancellationToken.ThrowIfCancellationRequested();

        // Note: this service is registered as a singleton service. As such, it cannot
        // directly depend on scoped services like the validation provider. To work around
        // this limitation, a scope is manually created for each method to this service.
        var scope = _provider.CreateScope();

        // Note: a try/finally block is deliberately used here to ensure the service scope
        // can be disposed of asynchronously if it implements IAsyncDisposable.
        try
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<IOpenIddictClientDispatcher>();
            var factory = scope.ServiceProvider.GetRequiredService<IOpenIddictClientFactory>();
            var transaction = await factory.CreateTransactionAsync();

            request = await PrepareRevocationRequestAsync();
            request = await ApplyRevocationRequestAsync();

            var response = await ExtractRevocationResponseAsync();

            return await HandleRevocationResponseAsync();

            async ValueTask<OpenIddictRequest> PrepareRevocationRequestAsync()
            {
                var context = new PrepareRevocationRequestContext(transaction)
                {
                    CancellationToken = cancellationToken,
                    Configuration = configuration,
                    Registration = registration,
                    RemoteUri = uri,
                    Request = request
                };

                await dispatcher.DispatchAsync(context);

                if (context.IsRejected)
                {
                    throw new ProtocolException(
                        SR.FormatID0430(context.Error, context.ErrorDescription, context.ErrorUri),
                        context.Error, context.ErrorDescription, context.ErrorUri);
                }

                return context.Request;
            }

            async ValueTask<OpenIddictRequest> ApplyRevocationRequestAsync()
            {
                var context = new ApplyRevocationRequestContext(transaction)
                {
                    CancellationToken = cancellationToken,
                    Configuration = configuration,
                    Registration = registration,
                    RemoteUri = uri,
                    Request = request
                };

                await dispatcher.DispatchAsync(context);

                if (context.IsRejected)
                {
                    throw new ProtocolException(
                        SR.FormatID0431(context.Error, context.ErrorDescription, context.ErrorUri),
                        context.Error, context.ErrorDescription, context.ErrorUri);
                }

                context.Logger.LogInformation(SR.GetResourceString(SR.ID6192), context.RemoteUri, context.Request);

                return context.Request;
            }

            async ValueTask<OpenIddictResponse> ExtractRevocationResponseAsync()
            {
                var context = new ExtractRevocationResponseContext(transaction)
                {
                    CancellationToken = cancellationToken,
                    Configuration = configuration,
                    Registration = registration,
                    RemoteUri = uri,
                    Request = request
                };

                await dispatcher.DispatchAsync(context);

                if (context.IsRejected)
                {
                    throw new ProtocolException(
                        SR.FormatID0432(context.Error, context.ErrorDescription, context.ErrorUri),
                        context.Error, context.ErrorDescription, context.ErrorUri);
                }

                Debug.Assert(context.Response is not null, SR.GetResourceString(SR.ID4007));

                context.Logger.LogInformation(SR.GetResourceString(SR.ID6193), context.RemoteUri, context.Response);

                return context.Response;
            }

            async ValueTask<OpenIddictResponse> HandleRevocationResponseAsync()
            {
                var context = new HandleRevocationResponseContext(transaction)
                {
                    CancellationToken = cancellationToken,
                    Configuration = configuration,
                    Registration = registration,
                    RemoteUri = uri,
                    Request = request,
                    Response = response
                };

                await dispatcher.DispatchAsync(context);

                if (context.IsRejected)
                {
                    throw new ProtocolException(
                        SR.FormatID0433(context.Error, context.ErrorDescription, context.ErrorUri),
                        context.Error, context.ErrorDescription, context.ErrorUri);
                }

                return context.Response;
            }
        }

        finally
        {
            if (scope is IAsyncDisposable disposable)
            {
                await disposable.DisposeAsync();
            }

            else
            {
                scope.Dispose();
            }
        }
    }

    /// <summary>
    /// Sends the token request and retrieves the corresponding response.
    /// </summary>
    /// <param name="registration">The client registration.</param>
    /// <param name="configuration">The server configuration.</param>
    /// <param name="request">The token request.</param>
    /// <param name="uri">The uri of the remote token endpoint.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
    /// <returns>The token response.</returns>
    internal async ValueTask<OpenIddictResponse> SendTokenRequestAsync(
        OpenIddictClientRegistration registration, OpenIddictConfiguration configuration,
        OpenIddictRequest request, Uri uri, CancellationToken cancellationToken = default)
    {
        if (registration is null)
        {
            throw new ArgumentNullException(nameof(registration));
        }

        if (configuration is null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (uri is null)
        {
            throw new ArgumentNullException(nameof(uri));
        }

        if (!uri.IsAbsoluteUri || !uri.IsWellFormedOriginalString())
        {
            throw new ArgumentException(SR.GetResourceString(SR.ID0144), nameof(uri));
        }

        cancellationToken.ThrowIfCancellationRequested();

        // Note: this service is registered as a singleton service. As such, it cannot
        // directly depend on scoped services like the validation provider. To work around
        // this limitation, a scope is manually created for each method to this service.
        var scope = _provider.CreateScope();

        // Note: a try/finally block is deliberately used here to ensure the service scope
        // can be disposed of asynchronously if it implements IAsyncDisposable.
        try
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<IOpenIddictClientDispatcher>();
            var factory = scope.ServiceProvider.GetRequiredService<IOpenIddictClientFactory>();
            var transaction = await factory.CreateTransactionAsync();

            request = await PrepareTokenRequestAsync();
            request = await ApplyTokenRequestAsync();

            var response = await ExtractTokenResponseAsync();

            return await HandleTokenResponseAsync();

            async ValueTask<OpenIddictRequest> PrepareTokenRequestAsync()
            {
                var context = new PrepareTokenRequestContext(transaction)
                {
                    CancellationToken = cancellationToken,
                    Configuration = configuration,
                    Registration = registration,
                    RemoteUri = uri,
                    Request = request
                };

                await dispatcher.DispatchAsync(context);

                if (context.IsRejected)
                {
                    throw new ProtocolException(
                        SR.FormatID0320(context.Error, context.ErrorDescription, context.ErrorUri),
                        context.Error, context.ErrorDescription, context.ErrorUri);
                }

                return context.Request;
            }

            async ValueTask<OpenIddictRequest> ApplyTokenRequestAsync()
            {
                var context = new ApplyTokenRequestContext(transaction)
                {
                    CancellationToken = cancellationToken,
                    Configuration = configuration,
                    Registration = registration,
                    RemoteUri = uri,
                    Request = request
                };

                await dispatcher.DispatchAsync(context);

                if (context.IsRejected)
                {
                    throw new ProtocolException(
                        SR.FormatID0321(context.Error, context.ErrorDescription, context.ErrorUri),
                        context.Error, context.ErrorDescription, context.ErrorUri);
                }

                context.Logger.LogInformation(SR.GetResourceString(SR.ID6192), context.RemoteUri, context.Request);

                return context.Request;
            }

            async ValueTask<OpenIddictResponse> ExtractTokenResponseAsync()
            {
                var context = new ExtractTokenResponseContext(transaction)
                {
                    CancellationToken = cancellationToken,
                    Configuration = configuration,
                    Registration = registration,
                    RemoteUri = uri,
                    Request = request
                };

                await dispatcher.DispatchAsync(context);

                if (context.IsRejected)
                {
                    throw new ProtocolException(
                        SR.FormatID0322(context.Error, context.ErrorDescription, context.ErrorUri),
                        context.Error, context.ErrorDescription, context.ErrorUri);
                }

                Debug.Assert(context.Response is not null, SR.GetResourceString(SR.ID4007));

                context.Logger.LogInformation(SR.GetResourceString(SR.ID6193), context.RemoteUri, context.Response);

                return context.Response;
            }

            async ValueTask<OpenIddictResponse> HandleTokenResponseAsync()
            {
                var context = new HandleTokenResponseContext(transaction)
                {
                    CancellationToken = cancellationToken,
                    Configuration = configuration,
                    Registration = registration,
                    RemoteUri = uri,
                    Request = request,
                    Response = response
                };

                await dispatcher.DispatchAsync(context);

                if (context.IsRejected)
                {
                    throw new ProtocolException(
                        SR.FormatID0323(context.Error, context.ErrorDescription, context.ErrorUri),
                        context.Error, context.ErrorDescription, context.ErrorUri);
                }

                return context.Response;
            }
        }

        finally
        {
            if (scope is IAsyncDisposable disposable)
            {
                await disposable.DisposeAsync();
            }

            else
            {
                scope.Dispose();
            }
        }
    }

    /// <summary>
    /// Sends the userinfo request and retrieves the corresponding response.
    /// </summary>
    /// <param name="registration">The client registration.</param>
    /// <param name="configuration">The server configuration.</param>
    /// <param name="request">The userinfo request.</param>
    /// <param name="uri">The uri of the remote userinfo endpoint.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
    /// <returns>The response and the principal extracted from the userinfo response or the userinfo token.</returns>
    internal async ValueTask<(OpenIddictResponse Response, (ClaimsPrincipal? Principal, string? Token))> SendUserinfoRequestAsync(
        OpenIddictClientRegistration registration, OpenIddictConfiguration configuration,
        OpenIddictRequest request, Uri uri, CancellationToken cancellationToken = default)
    {
        if (registration is null)
        {
            throw new ArgumentNullException(nameof(registration));
        }

        if (configuration is null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        if (uri is null)
        {
            throw new ArgumentNullException(nameof(uri));
        }

        if (!uri.IsAbsoluteUri || !uri.IsWellFormedOriginalString())
        {
            throw new ArgumentException(SR.GetResourceString(SR.ID0144), nameof(uri));
        }

        cancellationToken.ThrowIfCancellationRequested();

        // Note: this service is registered as a singleton service. As such, it cannot
        // directly depend on scoped services like the validation provider. To work around
        // this limitation, a scope is manually created for each method to this service.
        var scope = _provider.CreateScope();

        // Note: a try/finally block is deliberately used here to ensure the service scope
        // can be disposed of asynchronously if it implements IAsyncDisposable.
        try
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<IOpenIddictClientDispatcher>();
            var factory = scope.ServiceProvider.GetRequiredService<IOpenIddictClientFactory>();
            var transaction = await factory.CreateTransactionAsync();

            request = await PrepareUserinfoRequestAsync();
            request = await ApplyUserinfoRequestAsync();

            var (response, token) = await ExtractUserinfoResponseAsync();

            return await HandleUserinfoResponseAsync();

            async ValueTask<OpenIddictRequest> PrepareUserinfoRequestAsync()
            {
                var context = new PrepareUserinfoRequestContext(transaction)
                {
                    CancellationToken = cancellationToken,
                    Configuration = configuration,
                    RemoteUri = uri,
                    Registration = registration,
                    Request = request
                };

                await dispatcher.DispatchAsync(context);

                if (context.IsRejected)
                {
                    throw new ProtocolException(
                        SR.FormatID0324(context.Error, context.ErrorDescription, context.ErrorUri),
                        context.Error, context.ErrorDescription, context.ErrorUri);
                }

                return context.Request;
            }

            async ValueTask<OpenIddictRequest> ApplyUserinfoRequestAsync()
            {
                var context = new ApplyUserinfoRequestContext(transaction)
                {
                    CancellationToken = cancellationToken,
                    Configuration = configuration,
                    RemoteUri = uri,
                    Registration = registration,
                    Request = request
                };

                await dispatcher.DispatchAsync(context);

                if (context.IsRejected)
                {
                    throw new ProtocolException(
                        SR.FormatID0325(context.Error, context.ErrorDescription, context.ErrorUri),
                        context.Error, context.ErrorDescription, context.ErrorUri);
                }

                context.Logger.LogInformation(SR.GetResourceString(SR.ID6194), context.RemoteUri, context.Request);

                return context.Request;
            }

            async ValueTask<(OpenIddictResponse, string?)> ExtractUserinfoResponseAsync()
            {
                var context = new ExtractUserinfoResponseContext(transaction)
                {
                    CancellationToken = cancellationToken,
                    Configuration = configuration,
                    RemoteUri = uri,
                    Registration = registration,
                    Request = request
                };

                await dispatcher.DispatchAsync(context);

                if (context.IsRejected)
                {
                    throw new ProtocolException(
                        SR.FormatID0326(context.Error, context.ErrorDescription, context.ErrorUri),
                        context.Error, context.ErrorDescription, context.ErrorUri);
                }

                Debug.Assert(context.Response is not null, SR.GetResourceString(SR.ID4007));

                context.Logger.LogInformation(SR.GetResourceString(SR.ID6195), context.RemoteUri, context.Response);

                return (context.Response, context.UserinfoToken);
            }

            async ValueTask<(OpenIddictResponse, (ClaimsPrincipal?, string?))> HandleUserinfoResponseAsync()
            {
                var context = new HandleUserinfoResponseContext(transaction)
                {
                    CancellationToken = cancellationToken,
                    Configuration = configuration,
                    Registration = registration,
                    RemoteUri = uri,
                    Request = request,
                    Response = response,
                    UserinfoToken = token
                };

                await dispatcher.DispatchAsync(context);

                if (context.IsRejected)
                {
                    throw new ProtocolException(
                        SR.FormatID0327(context.Error, context.ErrorDescription, context.ErrorUri),
                        context.Error, context.ErrorDescription, context.ErrorUri);
                }

                return (context.Response, (context.Principal, context.UserinfoToken));
            }
        }

        finally
        {
            if (scope is IAsyncDisposable disposable)
            {
                await disposable.DisposeAsync();
            }

            else
            {
                scope.Dispose();
            }
        }
    }
}

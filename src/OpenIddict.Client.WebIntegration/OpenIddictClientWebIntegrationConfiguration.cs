﻿/*
 * Licensed under the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
 * See https://github.com/openiddict/openiddict-core for more information concerning
 * the license and the contributors participating to this project.
 */

using System.ComponentModel;
using System.Net.Http;
using Microsoft.Extensions.Options;
using OpenIddict.Client.SystemNetHttp;
using static OpenIddict.Client.WebIntegration.OpenIddictClientWebIntegrationConstants;

namespace OpenIddict.Client.WebIntegration;

/// <summary>
/// Contains the methods required to ensure that the OpenIddict client Web integration configuration is valid.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Advanced)]
public sealed partial class OpenIddictClientWebIntegrationConfiguration : IConfigureOptions<OpenIddictClientOptions>,
                                                                          IConfigureOptions<OpenIddictClientSystemNetHttpOptions>,
                                                                          IPostConfigureOptions<OpenIddictClientOptions>
{
    /// <inheritdoc/>
    public void Configure(OpenIddictClientOptions options)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        // Register the built-in event handlers used by the OpenIddict client Web components.
        options.Handlers.AddRange(OpenIddictClientWebIntegrationHandlers.DefaultHandlers);
    }

    /// <inheritdoc/>
    public void Configure(OpenIddictClientSystemNetHttpOptions options)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        options.HttpClientHandlerActions.Add(static (registration, handler) =>
        {
            var certificate = registration.ProviderType switch
            {
                // Note: while not enforced yet, Pro Santé Connect's specification requires sending a TLS
                // client certificate when communicating with its backchannel OpenID Connect endpoints.
                //
                // For more information, see EXI PSC 24 in the annex part of
                // https://www.legifrance.gouv.fr/jorf/id/JORFTEXT000045551195.
                ProviderTypes.ProSantéConnect => registration.GetProSantéConnectSettings().ClientCertificate,

                _ => null
            };

            if (certificate is not null)
            {
                handler.ClientCertificates.Add(certificate);
                handler.ClientCertificateOptions = ClientCertificateOption.Manual;
            }
        });
    }

    /// <inheritdoc/>
    public void PostConfigure(string? name, OpenIddictClientOptions options)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        options.Registrations.ForEach(static registration =>
        {
            // If the client registration has a provider type attached, apply
            // the configuration logic corresponding to the specified provider.
            if (!string.IsNullOrEmpty(registration.ProviderType))
            {
                ConfigureProvider(registration);
            }
        });
    }

    /// <summary>
    /// Amends the registration with the provider-specific configuration logic.
    /// </summary>
    /// <param name="registration">The client registration.</param>
    // Note: the implementation of this method is automatically generated by the source generator.
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static partial void ConfigureProvider(OpenIddictClientRegistration registration);
}

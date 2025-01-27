﻿namespace MassTransit
{
    using System;
    using Azure.ServiceBus.Core;
    using Azure.ServiceBus.Core.Configurators;
    using Definition;


    public static class ServiceBusBusFactoryConfiguratorExtensions
    {
        /// <summary>
        /// Adds a service bus host using the MassTransit style URI host name
        /// </summary>
        /// <param name="configurator">The bus factory configurator</param>
        /// <param name="hostAddress">
        /// The host address, in MassTransit format (sb://namespace.servicebus.windows.net/scope)
        /// </param>
        /// <param name="configure">A callback to further configure the service bus</param>
        /// <returns>The service bus host</returns>
        public static void Host(this IServiceBusBusFactoryConfigurator configurator, Uri hostAddress,
            Action<IServiceBusHostConfigurator> configure = null)
        {
            var hostConfigurator = new ServiceBusHostConfigurator(hostAddress);

            configure?.Invoke(hostConfigurator);

            configurator.Host(hostConfigurator.Settings);
        }

        /// <summary>
        /// Adds a Service Bus host using a connection string (Endpoint=...., etc.).
        /// </summary>
        /// <param name="configurator">The bus factory configurator</param>
        /// <param name="connectionString">The connection string in the proper format</param>
        /// <param name="configure">A callback to further configure the service bus</param>
        /// <returns>The service bus host</returns>
        public static void Host(this IServiceBusBusFactoryConfigurator configurator, string connectionString,
            Action<IServiceBusHostConfigurator> configure = null)
        {
            // in case they pass a URI by mistake (it happens)
            if (Uri.IsWellFormedUriString(connectionString, UriKind.Absolute))
            {
                var hostAddress = new Uri(connectionString);

                Host(configurator, hostAddress, configure);
            }
            else
            {
                var hostConfigurator = new ServiceBusHostConfigurator(connectionString);

                configure?.Invoke(hostConfigurator);

                configurator.Host(hostConfigurator.Settings);
            }
        }

        public static void SharedAccessSignature(this IServiceBusHostConfigurator configurator,
            Action<ISharedAccessSignatureTokenProviderConfigurator> configure)
        {
            var tokenProviderConfigurator = new SharedAccessSignatureTokenProviderConfigurator();

            configure(tokenProviderConfigurator);

            configurator.TokenProvider = tokenProviderConfigurator.GetTokenProvider();
        }

        /// <summary>
        /// Declare a ReceiveEndpoint using a unique generated queue name. This queue defaults to auto-delete
        /// and non-durable. By default all services bus instances include a default receiveEndpoint that is
        /// of this type (created automatically upon the first receiver binding).
        /// </summary>
        /// <param name="configurator"></param>
        /// <param name="configure"></param>
        public static void ReceiveEndpoint(this IServiceBusBusFactoryConfigurator configurator, Action<IServiceBusReceiveEndpointConfigurator> configure = null)
        {
            configurator.ReceiveEndpoint(new TemporaryEndpointDefinition(), DefaultEndpointNameFormatter.Instance, configure);
        }

        /// <summary>
        /// Declare a ReceiveEndpoint using a unique generated queue name. This queue defaults to auto-delete
        /// and non-durable. By default all services bus instances include a default receiveEndpoint that is
        /// of this type (created automatically upon the first receiver binding).
        /// </summary>
        /// <param name="configurator"></param>
        /// <param name="definition"></param>
        /// <param name="configure"></param>
        public static void ReceiveEndpoint(this IServiceBusBusFactoryConfigurator configurator, IEndpointDefinition definition,
            Action<IServiceBusReceiveEndpointConfigurator> configure = null)
        {
            configurator.ReceiveEndpoint(definition, DefaultEndpointNameFormatter.Instance, configure);
        }
    }
}

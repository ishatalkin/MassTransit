﻿namespace MassTransit
{
    using System;
    using System.IO;
    using System.Net.Mime;
    using System.Threading.Tasks;
    using GreenPipes;
    using Topology;


    /// <summary>
    /// The receive context is sent from the transport when a message is ready to be processed
    /// from the transport.
    /// </summary>
    public interface ReceiveContext :
        PipeContext
    {
        // the amount of time elapsed since the message was read from the queue
        TimeSpan ElapsedTime { get; }

        /// <summary>
        /// The address on which the message was received
        /// </summary>
        Uri InputAddress { get; }

        /// <summary>
        /// The content type of the message, as determined by the available headers
        /// </summary>
        ContentType ContentType { get; }

        /// <summary>
        /// If True, the message is being redelivered by the transport
        /// </summary>
        bool Redelivered { get; }

        /// <summary>
        /// Headers specific to the transport
        /// </summary>
        Headers TransportHeaders { get; }

        /// <summary>
        /// The task that is completed once all pending tasks are completed
        /// </summary>
        Task ReceiveCompleted { get; }

        /// <summary>
        /// Returns true if the message was successfully consumed by at least one consumer
        /// </summary>
        bool IsDelivered { get; }

        /// <summary>
        /// Returns true if a fault occurred during the message delivery
        /// </summary>
        bool IsFaulted { get; }

        /// <summary>
        /// The send endpoint provider from the transport
        /// </summary>
        ISendEndpointProvider SendEndpointProvider { get; }

        /// <summary>
        /// The publish endpoint provider from the transport
        /// </summary>
        IPublishEndpointProvider PublishEndpointProvider { get; }

        /// <summary>
        /// The publish topology of the underlying transport
        /// </summary>
        IPublishTopology PublishTopology { get; }

        /// <summary>
        /// If true, faults should be published when no <see cref="ConsumeContext.ResponseAddress"/> or
        /// <see cref="ConsumeContext.FaultAddress"/> are present.
        /// </summary>
        bool PublishFaults { get; }

        /// <summary>
        /// Returns the message body as a stream that can be deserialized. The stream
        /// must be disposed by the caller, a reference is not retained
        /// </summary>
        Stream GetBodyStream();

        /// <summary>
        /// Returns the body as a byte[]
        /// </summary>
        /// <returns></returns>
        byte[] GetBody();

        /// <summary>
        /// Notify that a message has been consumed from the received context
        /// </summary>
        /// <param name="context">The consume context of the message</param>
        /// <param name="duration">The time spent by the consumer</param>
        /// <param name="consumerType">The consumer type</param>
        Task NotifyConsumed<T>(ConsumeContext<T> context, TimeSpan duration, string consumerType)
            where T : class;

        /// <summary>
        /// Notify that a message consumer faulted
        /// </summary>
        /// <param name="context">The consume context of the message</param>
        /// <param name="duration">The time spent by the consumer</param>
        /// <param name="consumerType">The message consumer type that faulted</param>
        /// <param name="exception">The exception that occurred</param>
        Task NotifyFaulted<T>(ConsumeContext<T> context, TimeSpan duration, string consumerType, Exception exception)
            where T : class;

        /// <summary>
        /// Notify that a message receive faulted outside of the message consumer
        /// </summary>
        /// <param name="exception">The exception that occurred</param>
        Task NotifyFaulted(Exception exception);

        // TODO to tie sends back to the receiver?
        //void NotifySend(string messageType, Uri destinationAddress);

        /// <summary>
        /// Adds a pending Task to the completion of the message receiver
        /// </summary>
        /// <param name="task"></param>
        void AddReceiveTask(Task task);
    }
}

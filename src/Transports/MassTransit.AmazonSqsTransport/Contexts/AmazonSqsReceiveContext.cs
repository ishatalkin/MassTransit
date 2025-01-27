﻿namespace MassTransit.AmazonSqsTransport.Contexts
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Amazon.SQS;
    using Amazon.SQS.Model;
    using Context;
    using Exceptions;
    using Internals.Extensions;
    using Metadata;
    using Topology;
    using Transports;
    using Util;


    public sealed class AmazonSqsReceiveContext :
        BaseReceiveContext,
        AmazonSqsMessageContext,
        ReceiveLockContext
    {
        static readonly int MaxVisibilityTimeout = (int)TimeSpan.FromHours(12).TotalSeconds;

        readonly CancellationTokenSource _activeTokenSource;
        readonly SqsReceiveEndpointContext _context;
        readonly ClientContext _clientContext;
        readonly ReceiveSettings _receiveSettings;
        byte[] _body;
        bool _locked;

        public AmazonSqsReceiveContext(Message transportMessage, bool redelivered, SqsReceiveEndpointContext context,
            ClientContext clientContext, ReceiveSettings receiveSettings, ConnectionContext connectionContext)
            : base(redelivered, context, receiveSettings, clientContext, connectionContext)
        {
            _context = context;
            _clientContext = clientContext;
            _receiveSettings = receiveSettings;
            TransportMessage = transportMessage;

            _activeTokenSource = new CancellationTokenSource();
            _locked = true;

            Task.Factory.StartNew(RenewMessageVisibility, _activeTokenSource.Token, TaskCreationOptions.None, TaskScheduler.Default);
        }

        protected override IHeaderProvider HeaderProvider => new AmazonSqsHeaderProvider(TransportMessage);

        public Message TransportMessage { get; }

        public Dictionary<string, MessageAttributeValue> Attributes => TransportMessage.MessageAttributes;

        public Task Complete()
        {
            _activeTokenSource.Cancel();

            return _clientContext.DeleteMessage(_receiveSettings.EntityName, TransportMessage.ReceiptHandle);
        }

        public async Task Faulted(Exception exception)
        {
            _activeTokenSource.Cancel();

            try
            {
                // return message to available message pool immediately
                await _clientContext.ChangeMessageVisibility(_receiveSettings.QueueUrl, TransportMessage.ReceiptHandle, 0).ConfigureAwait(false);
                _locked = false;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                LogContext.Error?.Log(ex, "ChangeMessageVisibility failed: {ReceiptHandle}, Original Exception: {Exception}", TransportMessage.ReceiptHandle,
                    exception);
            }
        }

        public Task ValidateLockStatus()
        {
            if (_locked)
                return TaskUtil.Completed;

            throw new TransportException(_context.InputAddress, $"Message Lock Lost: {TransportMessage.ReceiptHandle}");
        }

        public override void Dispose()
        {
            _activeTokenSource.Dispose();

            base.Dispose();
        }

        public override byte[] GetBody()
        {
            if (_body != null)
                return _body;

            if (TransportMessage != null)
                return _body = Encoding.UTF8.GetBytes(TransportMessage.Body);

            throw new AmazonSqsTransportException($"The message type is not supported: {TypeMetadataCache.GetShortName(typeof(Message))}");
        }

        public override Stream GetBodyStream()
        {
            return new MemoryStream(GetBody());
        }

        async Task RenewMessageVisibility()
        {
            TimeSpan CalculateDelay(int timeout) => TimeSpan.FromSeconds((timeout - ElapsedTime.TotalSeconds) * 0.6);

            var visibilityTimeout = _receiveSettings.VisibilityTimeout;

            var delay = CalculateDelay(visibilityTimeout);

            while (_activeTokenSource.Token.IsCancellationRequested == false)
            {
                try
                {
                    if (delay > TimeSpan.Zero)
                        await Task.Delay(delay, _activeTokenSource.Token).ConfigureAwait(false);

                    if (_activeTokenSource.Token.IsCancellationRequested)
                        break;

                    visibilityTimeout = Math.Min(MaxVisibilityTimeout, visibilityTimeout * 2);

                    await _clientContext.ChangeMessageVisibility(_receiveSettings.QueueUrl, TransportMessage.ReceiptHandle, visibilityTimeout)
                        .ConfigureAwait(false);

                    // LogContext.Debug?.Log("Extended message {ReceiptHandle} visibility to {VisibilityTimeout} ({ElapsedTime})", TransportMessage.ReceiptHandle,
                    //     TimeSpan.FromSeconds(visibilityTimeout).ToFriendlyString(), ElapsedTime);

                    if (visibilityTimeout >= MaxVisibilityTimeout)
                        break;

                    delay = CalculateDelay(visibilityTimeout);
                }
                catch (MessageNotInflightException exception)
                {
                    LogContext.Warning?.Log(exception, "Message no longer in flight: {ReceiptHandle}", TransportMessage.ReceiptHandle);

                    _locked = false;

                    Cancel();
                    break;
                }
                catch (ReceiptHandleIsInvalidException exception)
                {
                    LogContext.Warning?.Log(exception, "Message receipt handle is invalid: {ReceiptHandle}", TransportMessage.ReceiptHandle);

                    _locked = false;

                    Cancel();
                    break;
                }
                catch (AmazonSQSException exception)
                {
                    LogContext.Error?.Log(exception, "Failed to extend message {ReceiptHandle} visibility to {VisibilityTimeout} ({ElapsedTime})",
                        TransportMessage.ReceiptHandle, TimeSpan.FromSeconds(visibilityTimeout).ToFriendlyString(), ElapsedTime);

                    delay = TimeSpan.FromSeconds(1);
                }
                catch (TimeoutException)
                {
                    delay = TimeSpan.Zero;
                }
                catch (OperationCanceledException)
                {
                    _activeTokenSource.Cancel();
                }
                catch (Exception)
                {
                    _activeTokenSource.Cancel();
                }
            }
        }
    }
}

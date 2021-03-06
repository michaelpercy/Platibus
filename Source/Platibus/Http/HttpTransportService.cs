﻿// The MIT License (MIT)
// 
// Copyright (c) 2014 Jesse Sweetland
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Common.Logging;

namespace Platibus.Http
{
    public class HttpTransportService : ITransportService
    {
        private static readonly ILog Log = LogManager.GetLogger(LoggingCategories.Http);
        public event MessageReceivedHandler MessageReceived;
        public event SubscriptionRequestReceivedHandler SubscriptionRequestReceived;

        private HttpClient GetClient(Uri uri, IEndpointCredentials credentials)
        {
            var clientHandler = new HttpClientHandler
            {
                AllowAutoRedirect = true,
                UseProxy = false
            };

            if (credentials != null)
            {
                credentials.Accept(new HttpEndpointCredentialsVisitor(clientHandler));
            }

            var httpClient = new HttpClient(clientHandler)
            {
                BaseAddress = uri
            };

            return httpClient;
        }

        public async Task SendMessage(Message message, IEndpointCredentials credentials = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (message == null) throw new ArgumentNullException("message");
            if (message.Headers.Destination == null) throw new ArgumentException("Message has no destination");
            try
            {
                var httpContent = new StringContent(message.Content);
                WriteHttpContentHeaders(message, httpContent);
                var endpointBaseUri = message.Headers.Destination;

                var httpClient = GetClient(endpointBaseUri, credentials);

                var messageId = message.Headers.MessageId;
                var urlEncondedMessageId = HttpUtility.UrlEncode(messageId);
                var relativeUri = string.Format("message/{0}", urlEncondedMessageId);
                var httpResponseMessage = await httpClient
                    .PostAsync(relativeUri, httpContent, cancellationToken)
                    .ConfigureAwait(false);

                HandleHttpErrorResponse(httpResponseMessage);
            }
            catch (TransportException)
            {
                throw;
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (MessageNotAcknowledgedException)
            {
                throw;
            }
            catch (TaskCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                var errorMessage = string.Format("Error sending message ID {0}", message.Headers.MessageId);
                Log.ErrorFormat(errorMessage, ex);

                HandleCommunicationException(ex, message.Headers.Destination);

                throw new TransportException(errorMessage, ex);
            }
        }

        public async Task SendSubscriptionRequest(SubscriptionRequestType requestType, Uri publisherUri, IEndpointCredentials credentials, TopicName topic,
            Uri subscriberUri, TimeSpan ttl, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (publisherUri == null) throw new ArgumentNullException("publisherUri");
            if (topic == null) throw new ArgumentNullException("topic");
            if (subscriberUri == null) throw new ArgumentNullException("subscriber");

            try
            {
                var httpClient = GetClient(publisherUri, credentials);

                var urlSafeTopicName = HttpUtility.UrlEncode(topic);
                var relativeUri = string.Format("topic/{0}/subscriber?uri={1}", urlSafeTopicName, subscriberUri);
                if (ttl > TimeSpan.Zero)
                {
                    relativeUri += "&ttl=" + ttl.TotalSeconds;
                }

                HttpResponseMessage httpResponseMessage;
                switch (requestType)
                {
                    case SubscriptionRequestType.Remove:
                        httpResponseMessage = await httpClient
                            .DeleteAsync(relativeUri, cancellationToken)
                            .ConfigureAwait(false);
                        break;
                    default:
                        httpResponseMessage = await httpClient
                            .PostAsync(relativeUri, new StringContent(""), cancellationToken)
                            .ConfigureAwait(false);
                        break;
                }

                HandleHttpErrorResponse(httpResponseMessage);
            }
            catch (TransportException)
            {
                throw;
            }
            catch (TaskCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                var errorMessage = string.Format("Error sending subscription request for topic {0} of publisher {1}", topic, publisherUri);
                Log.ErrorFormat(errorMessage, ex);

                HandleCommunicationException(ex, publisherUri);

                throw new TransportException(errorMessage, ex);
            }
        }

        public async Task AcceptMessage(Message message, IPrincipal senderPrincipal, CancellationToken cancellationToken = default(CancellationToken))
        {
            var handlers = MessageReceived;
            if (handlers != null)
            {
                var args = new MessageReceivedEventArgs(message, senderPrincipal);
                var handlerTasks = handlers.GetInvocationList()
                    .Cast<MessageReceivedHandler>()
                    .Select(handler => handler(this, args));

                await Task.WhenAll(handlerTasks);
            }
        }

        public async Task AcceptSubscriptionRequest(SubscriptionRequestType requestType, TopicName topic, Uri subscriber, TimeSpan ttl, IPrincipal senderPrincipal, CancellationToken cancellationToken = default(CancellationToken))
        {
            var handlers = SubscriptionRequestReceived;
            if (handlers != null)
            {
                var args = new SubscriptionRequestReceivedEventArgs(requestType, topic, subscriber, ttl, senderPrincipal);
                var handlerTasks = handlers.GetInvocationList()
                    .Cast<SubscriptionRequestReceivedHandler>()
                    .Select(handler => handler(this, args));

                await Task.WhenAll(handlerTasks);
            }
        }

        private static void HandleHttpErrorResponse(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode) return;

            var statusCode = (int)response.StatusCode;
            var statusDescription = response.ReasonPhrase;

            if (statusCode == 401)
            {
                throw new UnauthorizedAccessException(string.Format("HTTP {0}: {1}", statusCode, statusDescription));
            }

            if (statusCode == 422)
            {
                throw new MessageNotAcknowledgedException(string.Format("HTTP {0}: {1}", statusCode, statusDescription));
            }

            if (statusCode < 500)
            {
                // HTTP 400-499 are invalid requests (bad request, authentication required, not authorized, etc.)
                throw new InvalidRequestException(string.Format("HTTP {0}: {1}", statusCode, statusDescription));
            }

            // HTTP 500+ are internal server errors
            throw new TransportException(string.Format("HTTP {0}: {1}", statusCode, statusDescription));
        }

        private static void HandleCommunicationException(Exception ex, Uri uri)
        {
            var hre = ex as HttpRequestException;
            if (hre != null && hre.InnerException != null)
            {
                HandleCommunicationException(hre.InnerException, uri);
                return;
            }

            var we = ex as WebException;
            if (we != null)
            {
                switch (we.Status)
                {
                    case WebExceptionStatus.NameResolutionFailure:
                        throw new NameResolutionFailedException(uri.Host);
                    case WebExceptionStatus.ConnectFailure:
                        throw new ConnectionRefusedException(uri.Host, uri.Port, ex.InnerException ?? ex);
                }
            }

            var se = ex as SocketException;
            if (se != null)
            {
                switch (se.SocketErrorCode)
                {
                    case SocketError.ConnectionRefused:
                        throw new ConnectionRefusedException(uri.Host, uri.Port, ex.InnerException ?? ex);
                }
            }
        }

        private static void WriteHttpContentHeaders(Message message, HttpContent content)
        {
            foreach (var header in message.Headers)
            {
                if ("Content-Type".Equals(header.Key, StringComparison.OrdinalIgnoreCase))
                {
                    content.Headers.ContentType = new MediaTypeHeaderValue(header.Value);
                    continue;
                }

                content.Headers.Add(header.Key, header.Value);
            }
        }
    }
}
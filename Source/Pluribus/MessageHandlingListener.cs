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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;

namespace Pluribus
{
    internal class MessageHandlingListener : IQueueListener
    {
        private static readonly ILog Log = LogManager.GetLogger(LoggingCategories.Filesystem);
        private readonly Bus _bus;
        private readonly IEnumerable<IMessageHandler> _messageHandlers;

        public MessageHandlingListener(Bus bus, IEnumerable<IMessageHandler> messageHandlers)
        {
            if (bus == null) throw new ArgumentNullException("bus");
            if (messageHandlers == null) throw new ArgumentNullException("messageHandlers");

            var handlerList = messageHandlers.Where(h => h != null).ToList();
            if (!handlerList.Any()) throw new ArgumentNullException("messageHandlers");

            _bus = bus;
            _messageHandlers = handlerList;
        }

        public async Task MessageReceived(Message message, IQueuedMessageContext context,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (message.Headers.Expires < DateTime.UtcNow)
            {
                Log.WarnFormat("Discarding expired \"{0}\" message (ID {1}, expired {2})", message.Headers.MessageName,
                    message.Headers.MessageId, message.Headers.Expires);
                return;
            }

            var messageContext = new BusMessageContext(_bus, context.Headers, context.SenderPrincipal);
            var handlingTasks = _messageHandlers
                .Select(handler => handler.HandleMessage(message, messageContext, cancellationToken));
            
            await Task.WhenAll(handlingTasks).ConfigureAwait(false);
            
            if (messageContext.MessageAcknowledged)
            {
                await context.Acknowledge().ConfigureAwait(false);
            }
        }
    }
}
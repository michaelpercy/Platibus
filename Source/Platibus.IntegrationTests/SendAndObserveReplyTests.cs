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
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Platibus.IntegrationTests
{
    class SendAndObserveReplyTests
    {
        private static readonly Random RNG = new Random();

        [Test]
        public async Task Given_Noncritical_Message_Not_Authorized_When_Sending_Then_UnauthorizedAccessException()
        {
            await With.HttpHostedBusInstances(async (platibus0, platibus1) =>
            {
                var message = new TestMessage
                {
                    GuidData = Guid.NewGuid(),
                    IntData = RNG.Next(0, int.MaxValue),
                    StringData = "Hello, world!",
                    DateData = DateTime.UtcNow,
                    SimulateAuthorizationFailure = true
                };

                Exception exception = null;
                try
                {
                    await platibus0.Send(message, new SendOptions
                    {
                        UseDurableTransport = false
                    });
                }
                catch (Exception ex)
                {
                    exception = ex;
                }

                Assert.That(exception, Is.Not.Null);
                Assert.That(exception, Is.InstanceOf<UnauthorizedAccessException>());
            });
        }

        [Test]
        public async Task Given_Noncritical_Message_Not_Acknowledged_When_Sending_Then_MessageNotAcknowledgedException()
        {
            await With.HttpHostedBusInstances(async (platibus0, platibus1) =>
            {
                var message = new TestMessage
                {
                    GuidData = Guid.NewGuid(),
                    IntData = RNG.Next(0, int.MaxValue),
                    StringData = "Hello, world!",
                    DateData = DateTime.UtcNow,
                    SimulateAcknowledgementFailure = true
                };

                Exception exception = null;
                try
                {
                    await platibus0.Send(message, new SendOptions
                    {
                        UseDurableTransport = false
                    });
                }
                catch (Exception ex)
                {
                    exception = ex;
                }

                Assert.That(exception, Is.Not.Null);
                Assert.That(exception, Is.InstanceOf<MessageNotAcknowledgedException>());
            });
        }

        [Test]
        public async Task Given_Message_When_Sending_Then_Reply_Should_Be_Observed()
        {
            await With.HttpHostedBusInstances(async (platibus0, platibus1) =>
            {
                var replyReceivedEvent = new ManualResetEvent(false);
                var repliesCompletedEvent = new ManualResetEvent(false);
                var replies = new ConcurrentQueue<object>();
                var message = new TestMessage
                {
                    GuidData = Guid.NewGuid(),
                    IntData = RNG.Next(0, int.MaxValue),
                    StringData = "Hello, world!",
                    DateData = DateTime.UtcNow
                };

                var sentMessage = await platibus0.Send(message);
                var subscription = sentMessage
                    .ObserveReplies()
                    .Subscribe(r =>
                    {
                        replies.Enqueue(r);
                        replyReceivedEvent.Set();
                    }, () => repliesCompletedEvent.Set());

                var replyReceived = await replyReceivedEvent.WaitOneAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
                var repliesCompleted = await repliesCompletedEvent.WaitOneAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
                subscription.Dispose();

                Assert.That(replyReceived, Is.True);
                Assert.That(repliesCompleted, Is.True);
                Assert.That(replies.Count, Is.EqualTo(1));

                var reply = replies.First();
                Assert.That(reply, Is.InstanceOf<TestReply>());
            });
        }

        [Test]
        public async Task When_Sending_And_Replying_With_Basic_Auth_Replies_Should_Be_Observed()
        {
            await With.HttpHostedBusInstancesBasicAuth(async (platibus0, platibus1) =>
            {
                var replyReceivedEvent = new ManualResetEvent(false);
                var repliesCompletedEvent = new ManualResetEvent(false);
                var replies = new ConcurrentQueue<object>();
                var message = new TestMessage
                {
                    GuidData = Guid.NewGuid(),
                    IntData = RNG.Next(0, int.MaxValue),
                    StringData = "Hello, world!",
                    DateData = DateTime.UtcNow
                };

                var sentMessage = await platibus0.Send(message);
                var subscription = sentMessage
                    .ObserveReplies()
                    .Subscribe(r =>
                    {
                        replies.Enqueue(r);
                        replyReceivedEvent.Set();
                    }, () => repliesCompletedEvent.Set());

                var replyReceived = await replyReceivedEvent.WaitOneAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
                var repliesCompleted = await repliesCompletedEvent.WaitOneAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
                subscription.Dispose();

                Assert.That(replyReceived, Is.True);
                Assert.That(repliesCompleted, Is.True);
                Assert.That(replies.Count, Is.EqualTo(1));

                var reply = replies.First();
                Assert.That(reply, Is.InstanceOf<TestReply>());
            });
        }
    }
}

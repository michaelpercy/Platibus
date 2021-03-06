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
using System.Configuration;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Common.Logging;
using Platibus.Config.Extensibility;
using Platibus.Filesystem;
using Platibus.Security;
using Platibus.Serialization;

namespace Platibus.Config
{
    public static class PlatibusConfigurationManager
    {
        private static readonly ILog Log = LogManager.GetLogger(LoggingCategories.Config);

        public static Task<PlatibusConfiguration> LoadConfiguration(bool processConfigurationHooks = true)
        {
            return LoadConfiguration("platibus", processConfigurationHooks);
        }

        public static async Task<PlatibusConfiguration> LoadConfiguration(string sectionName, bool processConfigurationHooks = true)
        {
            var configuration = new PlatibusConfiguration();

            var configSection = (PlatibusConfigurationSection) ConfigurationManager.GetSection(sectionName) ??
                                new PlatibusConfigurationSection();
            configuration.BaseUri = configSection.BaseUri;
            configuration.SerializationService = new DefaultSerializationService();
            configuration.MessageNamingService = new DefaultMessageNamingService();

            IEnumerable<EndpointElement> endpoints = configSection.Endpoints;
            foreach (var endpointConfig in endpoints)
            {
                IEndpointCredentials credentials = null;
                switch (endpointConfig.CredentialType)
                {
                    case ClientCredentialType.Basic:
                        var un = endpointConfig.Username;
                        var pw = endpointConfig.Password;
                        credentials = new BasicAuthCredentials(un, pw);
                        break;
                    case ClientCredentialType.Windows:
                        credentials = new DefaultCredentials();
                        break;
                }

                var endpoint = new Endpoint(endpointConfig.Address, credentials);
                configuration.AddEndpoint(endpointConfig.Name, endpoint);
            }

            IEnumerable<TopicElement> topics = configSection.Topics;
            foreach (var topic in topics)
            {
                configuration.AddTopic(topic.Name);
            }

            // Journaling is optional
            var journaling = configSection.Journaling;
            if (journaling != null && journaling.IsEnabled && !string.IsNullOrWhiteSpace(journaling.Provider))
            {
                configuration.MessageJournalingService = await InitMessageJournalingService(journaling);
            }

            var queueing = configSection.Queueing ?? new QueueingElement();
            configuration.MessageQueueingService = await InitMessageQueueingService(queueing);

            var subscriptionTracking = configSection.SubscriptionTracking ?? new SubscriptionTrackingElement();
            configuration.SubscriptionTrackingService = await InitSubscriptionTrackingService(subscriptionTracking);

            IEnumerable<SendRuleElement> sendRules = configSection.SendRules;
            foreach (var sendRule in sendRules)
            {
                var messageSpec = new MessageNamePatternSpecification(sendRule.NamePattern);
                var endpointName = (EndpointName) sendRule.Endpoint;
                configuration.AddSendRule(new SendRule(messageSpec, endpointName));
            }

            IEnumerable<SubscriptionElement> subscriptions = configSection.Subscriptions;
            foreach (var subscription in subscriptions)
            {
                var endpointName = subscription.Endpoint;
                var topicName = subscription.Topic;
                var ttl = subscription.TTL;
                configuration.AddSubscription(new Subscription(endpointName, topicName, ttl));
            }

            if (processConfigurationHooks)
            {
                ProcessConfigurationHooks(configuration);
            }
            return configuration;
        }

        public static Task<IMessageQueueingService> InitMessageQueueingService(QueueingElement config)
        {
            var providerName = config.Provider;
            IMessageQueueingServiceProvider provider;
            if (string.IsNullOrWhiteSpace(providerName))
            {
                Log.Debug("No message queueing service provider specified; using default provider...");
                provider = new FilesystemServicesProvider();
            }
            else
            {
                provider = ProviderHelper.GetProvider<IMessageQueueingServiceProvider>(providerName);
            }

            Log.Debug("Initializing message queueing service...");
            return provider.CreateMessageQueueingService(config);
        }

        public static Task<IMessageJournalingService> InitMessageJournalingService(JournalingElement config)
        {
            var providerName = config.Provider;
            if (string.IsNullOrWhiteSpace(providerName))
            {
                Log.Debug("No message journaling service provider specified; journaling will be disabled");
                return null;
            }
            
            var provider = ProviderHelper.GetProvider<IMessageJournalingServiceProvider>(providerName);
            
            Log.Debug("Initializing message journaling service...");
            return provider.CreateMessageJournalingService(config);
        }

        public static Task<ISubscriptionTrackingService> InitSubscriptionTrackingService(SubscriptionTrackingElement config)
        {
            var providerName = config.Provider;
            ISubscriptionTrackingServiceProvider provider;
            if (string.IsNullOrWhiteSpace(providerName))
            {
                Log.Debug("No subscription tracking service provider specified; using default provider...");
                provider = new FilesystemServicesProvider();
            }
            else
            {
                provider = ProviderHelper.GetProvider<ISubscriptionTrackingServiceProvider>(providerName);
            }

            Log.Debug("Initializing subscription tracking service...");
            return provider.CreateSubscriptionTrackingService(config);
        }

        public static string GetRootedPath(string path)
        {
            if (Path.IsPathRooted(path)) return path;

            var appDomainDir = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(appDomainDir, path);
        }

        public static void ProcessConfigurationHooks(PlatibusConfiguration configuration)
        {
            if (configuration == null) return;

            var hookTypes = ReflectionHelper.FindConcreteSubtypes<IConfigurationHook>();
            foreach (var hookType in hookTypes.Distinct())
            {
                try
                {
                    Log.InfoFormat("Processing configuration hook {0}...", hookType.FullName);
                    var hook = (IConfigurationHook)Activator.CreateInstance(hookType);
                    hook.Configure(configuration);
                    Log.InfoFormat("Configuration hook {0} processed successfully.", hookType.FullName);
                }
                catch (Exception ex)
                {
                    Log.ErrorFormat("Unhandled exception in configuration hook {0}", ex, hookType.FullName);
                }
            }
        }
    }
}
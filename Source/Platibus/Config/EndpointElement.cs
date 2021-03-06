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
using System.Configuration;

namespace Platibus.Config
{
    public class EndpointElement : ConfigurationElement
    {
        private const string NamePropertyName = "name";
        private const string AddressPropertyName = "address";
        private const string CredentialTypePropertyName = "credentialType";
        private const string UsernamePropertyName = "username";
        private const string PasswordPropertyName = "password";

        [ConfigurationProperty(NamePropertyName, IsRequired = true, IsKey = true)]
        public string Name
        {
            get { return (string) base[NamePropertyName]; }
            set { base[NamePropertyName] = value; }
        }

        [ConfigurationProperty(AddressPropertyName, IsRequired = true)]
        public Uri Address
        {
            get
            {
                var baseValue = base[AddressPropertyName];
                if (baseValue == null) return null;
                var uri = baseValue as Uri;
                if (uri != null) return uri;
                return new Uri(baseValue.ToString());
            }
            set { base[AddressPropertyName] = value; }
        }

        [ConfigurationProperty(CredentialTypePropertyName, IsRequired = false, DefaultValue = ClientCredentialType.None)]
        public ClientCredentialType CredentialType
        {
            get { return (ClientCredentialType)base[CredentialTypePropertyName]; }
            set { base[CredentialTypePropertyName] = value; }
        }

        [ConfigurationProperty(UsernamePropertyName, IsRequired = false)]
        public string Username
        {
            get { return (string)base[UsernamePropertyName]; }
            set { base[UsernamePropertyName] = value; }
        }

        [ConfigurationProperty(PasswordPropertyName, IsRequired = false)]
        public string Password
        {
            get { return (string)base[PasswordPropertyName]; }
            set { base[PasswordPropertyName] = value; }
        }
    }
}
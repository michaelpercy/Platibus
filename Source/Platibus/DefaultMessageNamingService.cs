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
using System.Runtime.Caching;

namespace Platibus
{
    public class DefaultMessageNamingService : IMessageNamingService, IDisposable
    {
        private readonly MemoryCache _nameTypeCache = new MemoryCache("DefaultMessageNamingService");

        protected bool _disposed;

        public MessageName GetNameForType(Type messageType)
        {
            return messageType.FullName;
        }

        public Type GetTypeForName(MessageName messageName)
        {
            Type type = null;
            if (messageName != null)
            {
                type = _nameTypeCache.Get(messageName) as Type;
            }

            if (type == null)
            {
                type = Type.GetType(messageName) ?? AppDomain.CurrentDomain
                    .GetAssemblies()
                    .Select(assembly => assembly.GetType(messageName))
                    .FirstOrDefault(t => t != null);

                if (type != null)
                {
                    _nameTypeCache[messageName] = type;
                }
            }
            return type;
        }

        public void Dispose()
        {
            if (_disposed) return;
            Dispose(true);
            _disposed = true;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _nameTypeCache.Dispose();
            }
        }
    }
}
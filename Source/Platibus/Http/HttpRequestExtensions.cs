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
using System.IO;
using System.Threading.Tasks;

namespace Platibus.Http
{
    public static class HttpResourceRequestExtensions
    {
        public static bool IsPost(this IHttpResourceRequest request)
        {
            return request != null && "POST".Equals(request.HttpMethod, StringComparison.OrdinalIgnoreCase);
        }

        public static async Task<string> ReadContentAsString(this IHttpResourceRequest request)
        {
            if (request == null) return null;

            var contentStream = request.InputStream;
            var contentEncoding = request.ContentEncoding;
            using (var contentReader = new StreamReader(contentStream, contentEncoding))
            {
                return await contentReader.ReadToEndAsync().ConfigureAwait(false);
            }
        }
    }
}
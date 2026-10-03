# Notices

## Unofficial status

`Jev.TypeSafe.Unofficial` is an independent, community-maintained .NET SDK for the TypeSafe AI API. It is **not**
affiliated with, endorsed by, or supported by TypeSafe AI. "TypeSafe", "TypeSafe AI", and "Jev" are names used by
their respective owners and appear here only to describe the service this library talks to.

## Upstream attribution

This library is a .NET port of the official JavaScript/TypeScript SDK, `@typesafe-ai/sdk`
(<https://github.com/typesafe-ai/typesafe-sdk-js>), tracking version 0.6.0 (see `SdkInfo.UpstreamVersion`).
Wire formats, retry and timeout semantics, error messages, and configuration precedence follow that SDK. No upstream
source code is copied; the implementation is written for .NET.

The upstream project is distributed under the following license, reproduced as required:

```
MIT License

Copyright (c) 2026 TypeSafe

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## Third-party packages

This library depends on the following Microsoft packages, each distributed under the MIT license:
`System.Text.Json`, `Microsoft.Extensions.Logging.Abstractions`, `Microsoft.Bcl.TimeProvider`, and
`System.Collections.Immutable`.

﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

#if ADAL
namespace Microsoft.Identity.Client.Extensions.Adal
#elif MSAL
namespace Microsoft.Identity.Client.Extensions.Msal
#else // WEB
namespace Microsoft.Identity.Client.Extensions.Web
#endif
{
    internal class CrossPlatLock : IDisposable
    {
        private const int LockfileRetryWait = 100;
        private const int LockfileRetryCount = 60000 / LockfileRetryWait;
        private FileStream _lockFileStream;

        public CrossPlatLock(string lockfilePath)
        {
            Exception exception = null;
            FileStream fileStream = null;

            // Create lock file dir if it doesn't already exist
            Directory.CreateDirectory(Path.GetDirectoryName(lockfilePath));

            for (int tryCount = 0; tryCount < LockfileRetryCount; tryCount++)
            {

                try
                {
                    // We are using the file locking to synchronize the store, do not allow multiple writers or readers for the file.
                    const int defaultBufferSize = 4096;
                    var fileShare = FileShare.None;
                    if (SharedUtilities.IsWindowsPlatform())
                    {
                        // This is so that Windows can offer read due to the granularity of the locking. Unix will not
                        // lock with FileShare.Read. Read access on Windows is only for debugging purposes and will not
                        // affect the functionality.
                        //
                        // See: https://github.com/dotnet/coreclr/blob/98472784f82cee7326a58e0c4acf77714cdafe03/src/System.Private.CoreLib/shared/System/IO/FileStream.Unix.cs#L74-L89
                        fileShare = FileShare.Read;
                    }

                    fileStream = new FileStream(lockfilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, fileShare, defaultBufferSize, FileOptions.DeleteOnClose);
                    
                    using (var writer = new StreamWriter(fileStream, Encoding.UTF8, defaultBufferSize, leaveOpen: true))
                    {
                        writer.WriteLine($"{Process.GetCurrentProcess().Id} {Process.GetCurrentProcess().ProcessName}");
                    }
                        break;
                }
                catch (IOException ex)
                {
                    exception = ex;
                    Thread.Sleep(LockfileRetryWait);
                }
                catch (UnauthorizedAccessException ex)
                {
                    exception = ex;
                    Thread.Sleep(LockfileRetryCount);
                }
            }

            _lockFileStream = fileStream ?? throw new InvalidOperationException("Could not get access to the shared lock file.", exception);
        }

        public void Dispose()
        {
            _lockFileStream?.Dispose();
            _lockFileStream = null;
        }
    }
}

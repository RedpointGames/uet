namespace Redpoint.Windows.HandleManagement.Tests
{
    using System.Diagnostics;
    using System.Runtime.Versioning;

    public class NativeHandlesTests
    {
        private class IntHolder
        {
            public int HandlePathsQueriedSuccessfully;
        }

        [Fact]
        [SupportedOSPlatform("windows6.2")]
        public async Task CanQuerySystemHandlesInternal()
        {
            var handles = NativeHandles.GetSystemHandles();
            Assert.NotEmpty(handles);

            var holder = new IntHolder();
            await Parallel.ForEachAsync(
                handles.GroupBy(x => x.ProcessId),
                async (handleGroup, cancellationToken) =>
                {
                    await Task.Run(() =>
                    {
                        foreach (var handle in handleGroup)
                        {
                            string? handlePath = null;
                            string[]? filePaths = null;
                            var result = NativeHandles.GetPathForHandle(handle, false, ref handlePath, ref filePaths);
                            if (result == NativeHandles.GetPathResultCode.AccessDenied)
                            {
                                // We can't access handles in the target process.
                                return;
                            }
                            else if (result == NativeHandles.GetPathResultCode.Success)
                            {
                                holder.HandlePathsQueriedSuccessfully++;
                            }
                        }
                    });
                });
            Assert.True(holder.HandlePathsQueriedSuccessfully > 0, "Expected to be able to get the path of at least one handle on the system.");
        }

        [Fact]
        [SupportedOSPlatform("windows6.2")]
        public async Task CanQueryRawHandles()
        {
            Assert.NotEmpty(await NativeHandles.GetAllHandlesAsync(CancellationToken.None).ToListAsync());
        }

        [Fact]
        [SupportedOSPlatform("windows6.2")]
        public async Task CanQueryFileHandles()
        {
            Assert.NotEmpty(await NativeHandles.GetAllFileHandlesAsync(CancellationToken.None).ToListAsync());
        }

        [Fact]
        [SupportedOSPlatform("windows6.2")]
        public async Task CanSeeOurOwnHandle()
        {
            var tempPath = Path.GetTempFileName();
            var found = false;
            using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            {
                var pid = Process.GetCurrentProcess().Id;
                await foreach (var fileHandle in NativeHandles.GetAllFileHandlesAsync(CancellationToken.None))
                {
                    if (fileHandle.ProcessId == pid)
                    {
                        if (fileHandle.FilePath.Equals(tempPath, StringComparison.InvariantCultureIgnoreCase))
                        {
                            found = true;
                            break;
                        }
                    }
                }
            }
            Assert.True(found);
        }

        [Fact]
        [SupportedOSPlatform("windows6.2")]
        public async Task CanCloseOurOwnHandle()
        {
            var tempPath = Path.GetTempFileName();
            var found = false;
            using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            {
                var pid = Process.GetCurrentProcess().Id;
                await foreach (var fileHandle in NativeHandles.GetAllFileHandlesAsync(CancellationToken.None))
                {
                    if (fileHandle.ProcessId == pid)
                    {
                        if (fileHandle.FilePath.Equals(tempPath, StringComparison.InvariantCultureIgnoreCase))
                        {
                            await NativeHandles.ForciblyCloseHandleAsync(fileHandle, CancellationToken.None);
                            found = true;
                            break;
                        }
                    }
                }
                if (found)
                {
                    var exception = Assert.Throws<IOException>(() =>
                    {
                        stream.WriteByte(1);

                        // This should throw an exception because the handle has been closed.
                        stream.Flush();
                    });
                    // HRESULT for "handle is invalid".
                    Assert.Equal(unchecked((int)0x80070006u), exception.HResult);
                }
            }
            Assert.True(found);
        }
    }
}
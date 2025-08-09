﻿namespace Redpoint.ProcessExecution.Windows
{
    using global::Windows.Win32.Foundation;
    using global::Windows.Win32.Security;
    using global::Windows.Win32.System.Console;
    using global::Windows.Win32.System.Services;
    using global::Windows.Win32.System.Threading;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Win32.SafeHandles;
    using Redpoint.ProcessExecution.Enumerable;
    using Redpoint.ProcessExecution.Windows.Ntdll;
    using Redpoint.ProcessExecution.Windows.SystemCopy;
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Linq;
    using System.Reflection.Metadata;
    using System.Runtime.InteropServices;
    using System.Runtime.Versioning;
    using System.ServiceProcess;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using static Redpoint.ProcessExecution.Windows.WindowsTrustedInstaller;
    using PInvoke = global::Windows.Win32.PInvoke;

    /// <remarks>
    /// This implementation is mostly from https://github.com/dotnet/runtime/blob/55c896f28b418893e202b4d20e95f5ed62402b91/src/libraries/System.Diagnostics.Process/src/System/Diagnostics/Process.Windows.cs, but with added support for suspending and chroot'ing Windows processes.
    /// </remarks>
    [SupportedOSPlatform("windows6.0.6000")]
    internal class WindowsProcessExecutor : IProcessExecutor
    {
        private static readonly object _createProcessLock = new object();
        private readonly ILogger<WindowsProcessExecutor> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly DefaultProcessExecutor _fallbackExecutor;
        private readonly IProcessArgumentParser _processArgumentParser;

        public WindowsProcessExecutor(
            ILogger<WindowsProcessExecutor> logger,
            IServiceProvider serviceProvider,
            DefaultProcessExecutor fallbackExecutor,
            IProcessArgumentParser processArgumentParser)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _fallbackExecutor = fallbackExecutor;
            _processArgumentParser = processArgumentParser;
        }

        public Task<int> ExecuteAsync(
            ProcessSpecification processSpecification,
            ICaptureSpecification captureSpecification,
            CancellationToken cancellationToken)
        {
            if (processSpecification.PerProcessDriveMappings != null ||
                processSpecification.RunAsTrustedInstaller)
            {
                return ExecuteInternalAsync(
                    processSpecification,
                    captureSpecification,
                    cancellationToken);
            }
            else
            {
                return _fallbackExecutor.ExecuteAsync(
                    processSpecification,
                    captureSpecification,
                    cancellationToken);
            }
        }

        private class AttributeList : IDisposable
        {
            private readonly LPPROC_THREAD_ATTRIBUTE_LIST _attributeList;
            private bool _disposedValue;

            public AttributeList(LPPROC_THREAD_ATTRIBUTE_LIST attributeList)
            {
                _attributeList = attributeList;
            }

            public LPPROC_THREAD_ATTRIBUTE_LIST Value => _attributeList;

            protected virtual void Dispose(bool disposing)
            {
                if (!_disposedValue)
                {
                    if (disposing)
                    {
                    }

                    PInvoke.DeleteProcThreadAttributeList(_attributeList);
                    _disposedValue = true;
                }
            }

            ~AttributeList()
            {
                // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
                Dispose(disposing: false);
            }

            public void Dispose()
            {
                // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
                Dispose(disposing: true);
                GC.SuppressFinalize(this);
            }
        }

        private static unsafe AttributeList CreateAttributeList(Dictionary<uint, (nint ptr, int size)> attributes)
        {
            nuint startupInfoAttributeListSize = 0;
            PInvoke.InitializeProcThreadAttributeList(
                (LPPROC_THREAD_ATTRIBUTE_LIST)(void*)null,
                (uint)attributes.Count,
                0,
                &startupInfoAttributeListSize);

            var buffer = Marshal.AllocHGlobal((int)startupInfoAttributeListSize);
            if (!PInvoke.InitializeProcThreadAttributeList(
                (LPPROC_THREAD_ATTRIBUTE_LIST)(void*)buffer,
                (uint)attributes.Count,
                0,
                &startupInfoAttributeListSize))
            {
                throw new InvalidOperationException("Unable to allocate attribute list!");
            }

            var attributeList = new AttributeList((LPPROC_THREAD_ATTRIBUTE_LIST)(void*)buffer);
            try
            {
                foreach (var kv in attributes)
                {
                    if (!PInvoke.UpdateProcThreadAttribute(
                        attributeList.Value,
                        0,
                        kv.Key,
                        (void*)kv.Value.ptr,
                        (nuint)kv.Value.size,
                        null,
                        (nuint*)null))
                    {
                        throw new InvalidOperationException("Unable to update attribute list!");
                    }
                }
            }
            catch
            {
                attributeList.Dispose();
                throw;
            }

            return attributeList;
        }

        private SafeProcessHandle CreateProcess(
            ProcessSpecification processSpecification,
            bool redirectStandardInput,
            bool redirectStandardOutput,
            bool redirectStandardError,
            StringBuilder commandLine,
            ref WindowsChrootState? chrootState,
            out StreamWriter? standardInput,
            out StreamReader? standardOutput,
            out StreamReader? standardError)
        {
            standardInput = null;
            standardOutput = null;
            standardError = null;

            // Define the structures for starting the process.
            STARTUPINFOEXW startupInfo = default;
            PROCESS_INFORMATION processInfo = default;
            SECURITY_ATTRIBUTES unusedSecurityAttrs = default;
            SafeProcessHandle procSH = new SafeProcessHandle();

            // Handles used in the parent process.
            SafeFileHandle? parentInputPipeHandle = null;
            SafeFileHandle? childInputPipeHandle = null;
            SafeFileHandle? parentOutputPipeHandle = null;
            SafeFileHandle? childOutputPipeHandle = null;
            SafeFileHandle? parentErrorPipeHandle = null;
            SafeFileHandle? childErrorPipeHandle = null;

            // Holds the parent process handle pointer when running underneath the TrustedInstaller.
            NtProcessSafeHandle? parentProcessHandle = null;

            // Take a global lock to prevent concurrent CreateProcess
            // calls, which would cause handles to be inherited
            // incorrectly.
            //
            // @todo: Do we need to steal the lock object from inside
            // the System.Diagnostics.Process class?
            //
            lock (_createProcessLock)
            {
                unsafe
                {
                    nint* parentProcessHandleValue = null;
                    AttributeList? parentProcessAttributeList = null;

                    try
                    {
                        if (processSpecification.RunAsTrustedInstaller)
                        {
                            parentProcessHandle = GetTrustedInstallerParentProcess();
                            if (parentProcessHandle.IsInvalid)
                            {
                                throw new RunAsTrustedInstallerFailedException("RunAsTrustedInstaller requested, but we could not access the 'TrustedInstaller' service.");
                            }
                            parentProcessHandleValue = (nint*)Marshal.AllocHGlobal(sizeof(nint));
                            *parentProcessHandleValue = parentProcessHandle.DangerousGetHandle();
                            parentProcessAttributeList = CreateAttributeList(new Dictionary<uint, (nint ptr, int size)>
                            {
                                {
                                    PInvoke.PROC_THREAD_ATTRIBUTE_PARENT_PROCESS | PInvoke.PROC_THREAD_ATTRIBUTE_INPUT,
                                    (ptr: (nint)parentProcessHandleValue, size: sizeof(nint))
                                }
                            });
                        }
                        else
                        {
                            parentProcessAttributeList = CreateAttributeList(new Dictionary<uint, (nint ptr, int size)>());
                        }

                        startupInfo.StartupInfo.cb = (uint)sizeof(STARTUPINFOEXW);
                        startupInfo.lpAttributeList = parentProcessAttributeList.Value;

                        // Set up the streams.
                        if (redirectStandardInput || redirectStandardOutput || redirectStandardError)
                        {
                            if (redirectStandardInput)
                            {
                                CreatePipe(out parentInputPipeHandle, out childInputPipeHandle, true);
                            }
                            else
                            {
                                childInputPipeHandle = new SafeFileHandle(PInvoke.GetStdHandle(STD_HANDLE.STD_INPUT_HANDLE), false);
                            }

                            if (redirectStandardOutput)
                            {
                                CreatePipe(out parentOutputPipeHandle, out childOutputPipeHandle, false);
                            }
                            else
                            {
                                childOutputPipeHandle = new SafeFileHandle(PInvoke.GetStdHandle(STD_HANDLE.STD_OUTPUT_HANDLE), false);
                            }

                            if (redirectStandardError)
                            {
                                CreatePipe(out parentErrorPipeHandle, out childErrorPipeHandle, false);
                            }
                            else
                            {
                                childErrorPipeHandle = new SafeFileHandle(PInvoke.GetStdHandle(STD_HANDLE.STD_ERROR_HANDLE), false);
                            }

                            startupInfo.StartupInfo.hStdInput = new HANDLE(childInputPipeHandle.DangerousGetHandle());
                            startupInfo.StartupInfo.hStdOutput = new HANDLE(childOutputPipeHandle.DangerousGetHandle());
                            startupInfo.StartupInfo.hStdError = new HANDLE(childErrorPipeHandle.DangerousGetHandle());

                            startupInfo.StartupInfo.dwFlags = STARTUPINFOW_FLAGS.STARTF_USESTDHANDLES;
                        }

                        // Set up the creation flags parameter.
                        PROCESS_CREATION_FLAGS creationFlags = 0;
                        creationFlags |= PROCESS_CREATION_FLAGS.EXTENDED_STARTUPINFO_PRESENT;
                        if (processSpecification.RunAsTrustedInstaller)
                        {
                            // This is required, otherwise the process will fail with STATUS_DLL_INIT_FAILED.
                            creationFlags |= PROCESS_CREATION_FLAGS.CREATE_NEW_CONSOLE;
                        }

                        // Set up the environment block parameter.
                        string? environmentBlock = null;
                        if (processSpecification.EnvironmentVariables != null)
                        {
                            creationFlags |= PROCESS_CREATION_FLAGS.CREATE_UNICODE_ENVIRONMENT;
                            environmentBlock = GetEnvironmentVariablesBlock(processSpecification.EnvironmentVariables);
                        }

                        // Set up the working directory.
                        string? workingDirectory = processSpecification.WorkingDirectory;
                        if (workingDirectory != null &&
                            workingDirectory.Length == 0)
                        {
                            workingDirectory = null;
                        }

                        // If we have per-process drive mappings, we need to create the process
                        // in a suspended state and set up our chroot state now (so the handles
                        // can be inherited).
                        if (processSpecification.PerProcessDriveMappings != null)
                        {
                            creationFlags |= PROCESS_CREATION_FLAGS.CREATE_SUSPENDED;
                            chrootState = WindowsChroot.SetupChrootState(processSpecification.PerProcessDriveMappings!);
                        }

                    retryCreation:

                        // Create the process.
                        bool retVal;
                        int errorCode = 0;
                        fixed (char* environmentBlockPtr = environmentBlock)
                        fixed (char* commandLinePtrRaw = commandLine.ToString())
                        fixed (char* workingDirectoryPtr = workingDirectory)
                        {
                            var commandLinePtr = new PWSTR(commandLinePtrRaw);
                            var currentDirectoryPtr = new PCWSTR(workingDirectoryPtr);
                            retVal = PInvoke.CreateProcess(
                                new PCWSTR(null),           // we don't need this since all the info is in commandLine
                                commandLinePtr,             // pointer to the command line string
                                &unusedSecurityAttrs,       // address to process security attributes, we don't need to inherit the handle
                                &unusedSecurityAttrs,       // address to thread security attributes.
                                new BOOL(true),             // handle inheritance flag
                                creationFlags,              // creation flags
                                (void*)environmentBlockPtr, // pointer to new environment block
                                                            // pointer to current directory name
                                                            // @note: If we're using per-process drive mappings, we inherit the current
                                                            // directory and change it after we've applied the device mappings to the new
                                                            // process, since our current process doesn't have the device mappings and thus
                                                            // it's view of what the working directory should be isn't the same.
                                processSpecification.PerProcessDriveMappings != null ? null : currentDirectoryPtr,
                                &startupInfo.StartupInfo,               // pointer to STARTUPINFO
                                &processInfo                // pointer to PROCESS_INFORMATION
                            );
                            if (!retVal)
                            {
                                errorCode = Marshal.GetLastWin32Error();
                            }
                        }

                        if (processInfo.hProcess != IntPtr.Zero && processInfo.hProcess != new IntPtr(-1))
                        {
                            Marshal.InitHandle(procSH, processInfo.hProcess);
                        }
                        try
                        {
                            if (!retVal)
                            {
                                throw new Win32Exception(errorCode, $"Result of CreateProcess was {retVal} with last Win32 error {errorCode}, hProcess handle was {processInfo.hProcess.Value}.");
                            }

                            // If we have per-process drive mappings, go and apply them to the
                            // process and resume it.
                            if (retVal && chrootState != null)
                            {
                                try
                                {
                                    _logger.LogTrace("WindowsProcessExecutor: Applying chroot state to process.");
                                    WindowsChroot.UseChrootState(chrootState, ref processInfo);
                                    if (workingDirectory != null)
                                    {
                                        _logger.LogTrace("WindowsProcessExecutor: Applying working directory to process.");
                                        WindowsWorkingDirectory.SetWorkingDirectoryOfAnotherProcess(processInfo.hProcess, workingDirectory);
                                    }
                                    _logger.LogTrace("WindowsProcessExecutor: Resuming thread.");
                                    WindowsChroot.ResumeThread(ref processInfo);
                                }
                                catch (InvalidOperationException ex) when (ex.Message.Contains("Got NTSTATUS C0000008 when setting information process ProcessDeviceMap", StringComparison.Ordinal))
                                {
                                    // This randomly seems to happen. Just retry creating the process.
                                    PInvoke.TerminateProcess(processInfo.hProcess, unchecked((uint)-1));
                                    goto retryCreation;
                                }
                                catch
                                {
                                    // The process will be in a suspended state and never resume.
                                    // Terminate it rather than leaving it running.
                                    PInvoke.TerminateProcess(processInfo.hProcess, unchecked((uint)-1));
                                    throw;
                                }
                            }
                        }
                        finally
                        {
                            if (processInfo.hThread != IntPtr.Zero && processInfo.hThread != new IntPtr(-1))
                            {
                                PInvoke.CloseHandle(processInfo.hThread);
                            }
                        }
                    }
                    catch
                    {
                        parentInputPipeHandle?.Dispose();
                        parentOutputPipeHandle?.Dispose();
                        parentErrorPipeHandle?.Dispose();
                        procSH.Dispose();
                        throw;
                    }
                    finally
                    {
                        childInputPipeHandle?.Dispose();
                        childOutputPipeHandle?.Dispose();
                        childErrorPipeHandle?.Dispose();
                        parentProcessHandle?.Dispose();
                        if (parentProcessAttributeList != null)
                        {
                            parentProcessAttributeList.Dispose();
                        }
                        if (parentProcessHandleValue != null)
                        {
                            Marshal.FreeHGlobal((nint)parentProcessHandleValue);
                            parentProcessHandleValue = null;
                        }
                    }
                }
            }

            if (redirectStandardInput)
            {
                Encoding enc = GetEncoding((int)PInvoke.GetConsoleCP());
                standardInput = new StreamWriter(new FileStream(parentInputPipeHandle!, FileAccess.Write, 4096, false), enc, 4096);
                standardInput.AutoFlush = true;
            }
            if (redirectStandardOutput)
            {
                Encoding enc = GetEncoding((int)PInvoke.GetConsoleOutputCP());
                standardOutput = new StreamReader(new FileStream(parentOutputPipeHandle!, FileAccess.Read, 4096, false), enc, true, 4096);
            }
            if (redirectStandardError)
            {
                Encoding enc = GetEncoding((int)PInvoke.GetConsoleOutputCP());
                standardError = new StreamReader(new FileStream(parentErrorPipeHandle!, FileAccess.Read, 4096, false), enc, true, 4096);
            }

            return procSH;
        }

        private async Task<int> ExecuteInternalAsync(
            ProcessSpecification processSpecification,
            ICaptureSpecification captureSpecification,
            CancellationToken cancellationToken)
        {
            var disposables = new List<IAsyncDisposable>();
            foreach (var hook in _serviceProvider.GetServices<IProcessExecutorHook>())
            {
                var disposable = await hook.ModifyProcessSpecificationWithCleanupAsync(processSpecification, cancellationToken).ConfigureAwait(false);
                if (disposable != null)
                {
                    disposables.Add(disposable);
                }
            }

            WindowsChrootState? chrootState = null;
            try
            {
                // Construct the command line.
                var commandLine = new StringBuilder();
                var fileName = processSpecification.FilePath.Trim();
                var fileNameIsQuoted = fileName.Length > 0 && fileName[0] == '\"' && fileName[fileName.Length - 1] == '\"';
                if (!fileNameIsQuoted)
                {
                    commandLine.Append('"');
                }
                commandLine.Append(fileName);
                if (!fileNameIsQuoted)
                {
                    commandLine.Append('"');
                }
                var argumentsEvaluated = processSpecification.Arguments.ToArray();
                var argumentsString = _processArgumentParser.JoinArguments(argumentsEvaluated);
                if (!string.IsNullOrWhiteSpace(argumentsString))
                {
                    commandLine.Append(' ');
                    commandLine.Append(argumentsString);
                }
                _logger.LogTrace($"WindowsProcessExecutor is executing with command line: {commandLine}");

                // Compute whether we're redirecting anything.
                var redirectStandardInput =
                    captureSpecification.InterceptStandardInput ||
                    processSpecification.StdinData != null;
                var redirectStandardOutput =
                    captureSpecification.InterceptStandardOutput;
                var redirectStandardError =
                    captureSpecification.InterceptStandardError;

                // Create the process handle.
                SafeProcessHandle procSH = CreateProcess(
                    processSpecification,
                    redirectStandardInput,
                    redirectStandardOutput,
                    redirectStandardError,
                    commandLine,
                    ref chrootState,
                    out var standardInput,
                    out var standardOutput,
                    out var standardError);

                _logger.LogTrace($"Starting process: {_processArgumentParser.CreateArgumentFromLogicalValue(processSpecification.FilePath).ToString()} {string.Join(" ", argumentsEvaluated.Select(x => x.ToString()))}");

                if (procSH.IsInvalid)
                {
                    procSH.Dispose();
                    throw new InvalidOperationException("Unable to start process!");
                }

                Task? outputReadingTask = null;
                Task? errorReadingTask = null;

                if (redirectStandardInput)
                {
                    if (processSpecification.StdinData != null)
                    {
                        standardInput!.Write(processSpecification.StdinData);
                    }
                    if (captureSpecification.InterceptStandardInput)
                    {
                        var data = captureSpecification.OnRequestStandardInputAtStartup();
                        if (data != null)
                        {
                            standardInput!.Write(data);
                        }
                    }
                    standardInput!.Close();
                }
                if (redirectStandardOutput)
                {
                    outputReadingTask = Task.Run(async () =>
                    {
                        while (!standardOutput!.EndOfStream)
                        {
                            var line = (await standardOutput.ReadLineAsync().ConfigureAwait(false))?.TrimEnd();
                            if (line != null)
                            {
                                captureSpecification.OnReceiveStandardOutput(line);
                            }
                        }
                    }, cancellationToken);
                }
                if (redirectStandardError)
                {
                    errorReadingTask = Task.Run(async () =>
                    {
                        while (!standardError!.EndOfStream)
                        {
                            var line = (await standardError.ReadLineAsync().ConfigureAwait(false))?.TrimEnd();
                            if (line != null)
                            {
                                captureSpecification.OnReceiveStandardError(line);
                            }
                        }
                    }, cancellationToken);
                }

                var hasExited = false;
                uint exitCode = unchecked((uint)-1);
                try
                {
                    var exitSemaphore = new SemaphoreSlim(0);

                    var waitHandle = new ProcessWaitHandle(new HANDLE(procSH.DangerousGetHandle()));
                    var registeredWaitHandle = ThreadPool.RegisterWaitForSingleObject(
                        waitHandle,
                        new WaitOrTimerCallback((state, timedOut) =>
                        {
                            hasExited = true;
                            if (PInvoke.GetExitCodeProcess(
                                procSH,
                                out exitCode) &&
                                exitCode == NTSTATUS.STILL_ACTIVE)
                            {
                                // @todo: What?
                            }
                            exitSemaphore.Release();
                        }),
                        waitHandle,
                        -1,
                        true);

                    // Check if we exited before the wait handle
                    // was registered.
                    if (PInvoke.GetExitCodeProcess(
                        procSH,
                        out exitCode) &&
                        exitCode != NTSTATUS.STILL_ACTIVE)
                    {
                        hasExited = true;
                        exitSemaphore.Release();
                        registeredWaitHandle.Unregister(waitHandle);
                    }

                    // Wait for the process to exit or until cancellation.
                    await exitSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        if (!hasExited)
                        {
                            // @note: If you're still seeing stalls here when Ctrl-C is pressed, it might
                            // be specific to running under the debugger! Make sure you can reproduce
                            // the stall when running from "dotnet run" or a packaged build before
                            // spending time on trying to fix stalls in this code.

                            // @note: There's a weird bug where if we try to terminate the whole
                            // process tree of cl.exe, then the Process.Kill call will stall for
                            // 30 seconds. Workaround this issue by only killing cl.exe itself
                            // if that's what we're running (it won't spawn child processes anyway).
                            if (Path.GetFileNameWithoutExtension(processSpecification.FilePath) == "cl")
                            {
                                TerminateProcess(procSH);
                            }
                            else
                            {
                                TerminateProcessTree(procSH);
                            }
                        }
                    }
                }
                if (!hasExited)
                {
                    // Give the process one last chance to exit normally
                    // so we can try to get the exit code.
                    await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                    if (!hasExited)
                    {
                        // We can't get the return code for this process.
                        cancellationToken.ThrowIfCancellationRequested();
                        return int.MaxValue;
                    }
                }
                if (outputReadingTask != null)
                {
                    try
                    {
                        await outputReadingTask.ConfigureAwait(false);
                    }
                    catch { }
                }
                if (errorReadingTask != null)
                {
                    try
                    {
                        await errorReadingTask.ConfigureAwait(false);
                    }
                    catch { }
                }
                cancellationToken.ThrowIfCancellationRequested();

                if (exitCode > int.MaxValue)
                {
                    var win32Error = NtdllPInvoke.RtlNtStatusToDosError(unchecked((int)exitCode)) | unchecked((int)0x80070000);
                    throw new Win32Exception(win32Error);
                }

                return unchecked((int)exitCode);
            }
            finally
            {
                if (chrootState != null)
                {
                    WindowsChroot.CleanupChrootState(chrootState);
                }
                foreach (var disposable in disposables)
                {
                    await disposable.DisposeAsync().ConfigureAwait(false);
                }
            }
        }

        private static int GetProcessId(SafeHandle handle)
        {
            PROCESS_BASIC_INFORMATION info;
            uint returnLength;
            unsafe
            {
                var statusResult = global::Windows.Wdk.PInvoke.NtQueryInformationProcess(
                    new HANDLE(handle.DangerousGetHandle()),
                    global::Windows.Wdk.System.Threading.PROCESSINFOCLASS.ProcessBasicInformation,
                    &info,
                    (uint)sizeof(PROCESS_BASIC_INFORMATION),
                    &returnLength);
                if (statusResult != 0)
                {
                    throw new Win32Exception(statusResult);
                }
            }
            return (int)info.UniqueProcessId;
        }

        private static int GetParentProcessId(SafeHandle handle)
        {
            PROCESS_BASIC_INFORMATION info;
            uint returnLength;
            unsafe
            {
                var statusResult = global::Windows.Wdk.PInvoke.NtQueryInformationProcess(
                    new HANDLE(handle.DangerousGetHandle()),
                    global::Windows.Wdk.System.Threading.PROCESSINFOCLASS.ProcessBasicInformation,
                    &info,
                    (uint)sizeof(PROCESS_BASIC_INFORMATION),
                    &returnLength);
                if (statusResult != 0)
                {
                    throw new Win32Exception(statusResult);
                }
            }
            return (int)info.InheritedFromUniqueProcessId;
        }

        private static List<Exception>? TerminateProcessTree(SafeHandle handle)
        {
            List<Exception>? exceptions = null;
            try
            {
                TerminateProcess(handle);
            }
            catch (Win32Exception e)
            {
                (exceptions ??= new List<Exception>()).Add(e);
            }
            var pid = GetProcessId(handle);
            var children = new List<SafeHandle>();
            foreach (Process p in Process.GetProcesses())
            {
                try
                {
                    var pHandle = p.SafeHandle;
                    if (!pHandle.IsInvalid && GetParentProcessId(pHandle) == pid)
                    {
                        children.Add(pHandle);
                    }
                }
                catch (Win32Exception ex) when (ex.NativeErrorCode == (int)WIN32_ERROR.ERROR_ACCESS_DENIED)
                {
                }
            }
            try
            {
                foreach (var child in children)
                {
                    List<Exception>? exceptionsFromChild = TerminateProcessTree(child);
                    if (exceptionsFromChild != null)
                    {
                        (exceptions ??= new List<Exception>()).AddRange(exceptionsFromChild);
                    }
                }
            }
            finally
            {
                foreach (var child in children)
                {
                    child.Dispose();
                }
            }
            return exceptions;
        }

        private static void TerminateProcess(SafeHandle handle)
        {
            if (!PInvoke.TerminateProcess(handle, unchecked((uint)-1)))
            {
                // Capture the exception
                var exception = new Win32Exception();

                // Don't throw if the process has exited.
                if (exception.NativeErrorCode == (int)WIN32_ERROR.ERROR_ACCESS_DENIED &&
                    PInvoke.GetExitCodeProcess(handle, out uint localExitCode) && localExitCode != NTSTATUS.STILL_ACTIVE)
                {
                    return;
                }

                throw exception;
            }
        }

        private static ConsoleEncoding GetEncoding(int codePage)
        {
            Encoding enc = GetSupportedConsoleEncoding(codePage);
            return new ConsoleEncoding(enc); // ensure encoding doesn't output a preamble
        }

        private const int _utf8CodePage = 65001;

        private static Encoding GetSupportedConsoleEncoding(int codepage)
        {
            int defaultEncCodePage = Encoding.GetEncoding(0).CodePage;

            if (defaultEncCodePage == codepage || defaultEncCodePage != _utf8CodePage)
            {
                return Encoding.GetEncoding(codepage);
            }

            if (codepage != _utf8CodePage)
            {
                // @note: This is probably wrong, but only for machines that
                // have weird console codepages set up. However, we can't practically
                // access the OSEncoding class and the internal dependency chain 
                // for that class is wild so we can't make a copy of it.
                return Encoding.ASCII;
            }

            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        }

        private static string GetEnvironmentVariablesBlock(IReadOnlyDictionary<string, string> sd)
        {
            // https://docs.microsoft.com/en-us/windows/win32/procthread/changing-environment-variables
            // "All strings in the environment block must be sorted alphabetically by name. The sort is
            //  case-insensitive, Unicode order, without regard to locale. Because the equal sign is a
            //  separator, it must not be used in the name of an environment variable."

            var keys = sd.Keys.ToArray();
            Array.Sort(keys, StringComparer.OrdinalIgnoreCase);

            // Join the null-terminated "key=val\0" strings
            var result = new StringBuilder(8 * keys.Length);
            foreach (string key in keys)
            {
                result.Append(key).Append('=').Append(sd[key]).Append('\0');
            }

            return result.ToString();
        }

        private static void CreatePipeWithSecurityAttributes(
            out SafeFileHandle hReadPipe,
            out SafeFileHandle hWritePipe,
            ref SECURITY_ATTRIBUTES lpPipeAttributes,
            uint nSize)
        {
            bool ret = PInvoke.CreatePipe(
                out hReadPipe,
                out hWritePipe,
                lpPipeAttributes,
                nSize);
            if (!ret || hReadPipe.IsInvalid || hWritePipe.IsInvalid)
            {
                throw new Win32Exception();
            }
        }

        private static unsafe void CreatePipe(out SafeFileHandle parentHandle, out SafeFileHandle childHandle, bool parentInputs)
        {
            SECURITY_ATTRIBUTES securityAttributesParent = default;
            securityAttributesParent.bInheritHandle = true;

            SafeFileHandle? hTmp = null;
            try
            {
                if (parentInputs)
                {
                    CreatePipeWithSecurityAttributes(
                        out childHandle,
                        out hTmp,
                        ref securityAttributesParent,
                        0);
                }
                else
                {
                    CreatePipeWithSecurityAttributes(
                        out hTmp,
                        out childHandle,
                        ref securityAttributesParent,
                        0);
                }

                HANDLE currentProcHandle = PInvoke.GetCurrentProcess();
                HANDLE parentHandleTmp;
                if (!PInvoke.DuplicateHandle(
                    currentProcHandle,
                    new HANDLE(hTmp.DangerousGetHandle()),
                    currentProcHandle,
                    &parentHandleTmp,
                    0,
                    false,
                    DUPLICATE_HANDLE_OPTIONS.DUPLICATE_SAME_ACCESS))
                {
                    throw new Win32Exception();
                }
                parentHandle = new SafeFileHandle(parentHandleTmp, true);
            }
            finally
            {
                if (hTmp != null && !hTmp.IsInvalid)
                {
                    hTmp.Dispose();
                }
            }
        }
    }
}

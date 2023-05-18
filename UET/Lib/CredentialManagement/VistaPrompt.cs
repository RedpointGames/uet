using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace CredentialManagement
{
    public class VistaPrompt: BaseCredentialsPrompt
    {
        string _domain;

        public VistaPrompt()
        {
            Title = "Please provide credentials";
        }

        public string Domain
        {
            get
            {
                CheckNotDisposed();
                return _domain;
            }
            set
            {
                CheckNotDisposed();
                if (string.IsNullOrEmpty(value))
                {
                    throw new ArgumentNullException("value");
                }
                _domain = value;
            }
        }
        public override bool ShowSaveCheckBox
        {
            get
            {
                CheckNotDisposed();
                return 0 != ((int)NativeMethods.WINVISTA_CREDUI_FLAGS.CREDUIWIN_CHECKBOX & DialogFlags);
            }
            set
            {
                CheckNotDisposed();
                AddFlag(value, (int)NativeMethods.WINVISTA_CREDUI_FLAGS.CREDUIWIN_CHECKBOX);
            }
        }
        public override bool GenericCredentials
        {
            get
            {
                CheckNotDisposed();
                return 0 != ((int)NativeMethods.WINVISTA_CREDUI_FLAGS.CREDUIWIN_GENERIC & DialogFlags);
            }
            set
            {
                CheckNotDisposed();
                AddFlag(value, (int)NativeMethods.WINVISTA_CREDUI_FLAGS.CREDUIWIN_GENERIC);
            }
        }

        public override DialogResult ShowDialog(IntPtr owner)
        {
            CheckNotDisposed();

            if (string.IsNullOrEmpty(Title) && string.IsNullOrEmpty(Message))
            {
                throw new InvalidOperationException("Title or Message should always be set.");
            }

            if (!IsWinVistaOrHigher)
            {
                throw new InvalidOperationException("This Operating System does not support this prompt.");
            }

            uint authPackage = 0;
            IntPtr outCredBuffer;
            uint outCredSize;
            IntPtr inCredBuffer = IntPtr.Zero;
            int inCredBufferSize = 0;

            bool persist = SaveChecked;

            NativeMethods.CREDUI_INFO credUI = CreateCREDUI_INFO(owner);

            if (!string.IsNullOrEmpty(Username) || !string.IsNullOrEmpty(SecureStringHelper.CreateString(SecurePassword)))
            {
                // This seems to be very hacky but don't know a better way to do it yet
                // Call this method with the same credentials with the empty credentials buffer so that we can get it's size first
                // but it throws an error because the buffer is too small. So we'll re-initialize the buffer with correct size
                // and call again to populate the buffer this time.
                NativeMethods.CredPackAuthenticationBuffer(0, new StringBuilder(Username), new StringBuilder(SecureStringHelper.CreateString(SecurePassword)), inCredBuffer, ref inCredBufferSize);
                if (Marshal.GetLastWin32Error() == 122)
                {
                    // returned from prior method call and we now should have a valid size for the buffer
                    inCredBuffer = Marshal.AllocCoTaskMem(inCredBufferSize);
                    if (!NativeMethods.CredPackAuthenticationBuffer(0, new StringBuilder(Username), new StringBuilder(SecureStringHelper.CreateString(SecurePassword)), inCredBuffer, ref inCredBufferSize))
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "There was an issue with the given Username or Password.");
                    }
                }
            }

            //Show the dialog
            NativeMethods.CredUIReturnCodes dialogResult;
            try
            {
                dialogResult = NativeMethods.CredUIPromptForWindowsCredentials(ref credUI, ErrorCode, ref authPackage,
                                                                                    inCredBuffer,
                    //You can force that a specific username is shown in the dialog. Create it with 'CredPackAuthenticationBuffer()'. Then, the buffer goes here...
                                                                                    (uint)inCredBufferSize,
                    //...and the size goes here. You also have to add CREDUIWIN_IN_CRED_ONLY to the flags (last argument).
                                                                                    out outCredBuffer,
                                                                                    out outCredSize,
                                                                                    ref persist,
                                                                                    DialogFlags);
                // If the user has checked the Save Credentials checkbox then the persist variable
                // will be set to true and we want to set it so that the consumer can approprietly act
                // on the user action.
                SaveChecked = persist;
            }
            catch (EntryPointNotFoundException e)
            {
                throw new InvalidOperationException("This functionality is not supported by this operating system.", e);
            }
            switch (dialogResult)
            {
                case NativeMethods.CredUIReturnCodes.ERROR_CANCELLED:
                    return DialogResult.Cancel;
                case NativeMethods.CredUIReturnCodes.ERROR_NO_SUCH_LOGON_SESSION:
                case NativeMethods.CredUIReturnCodes.ERROR_NOT_FOUND:
                case NativeMethods.CredUIReturnCodes.ERROR_INVALID_ACCOUNT_NAME:
                case NativeMethods.CredUIReturnCodes.ERROR_INSUFFICIENT_BUFFER:
                case NativeMethods.CredUIReturnCodes.ERROR_INVALID_PARAMETER:
                case NativeMethods.CredUIReturnCodes.ERROR_INVALID_FLAGS:
                case NativeMethods.CredUIReturnCodes.ERROR_BAD_ARGUMENTS:
                    throw new InvalidOperationException("Invalid properties were specified.", new Win32Exception(Marshal.GetLastWin32Error()));
            }

            int maxUsername = 1000;
            int maxPassword = 1000;
            int maxDomain = 1000;

            StringBuilder usernameBuffer = new StringBuilder(1000);
            StringBuilder passwordBuffer = new StringBuilder(1000);
            StringBuilder domainBuffer = new StringBuilder(1000);

            bool result = NativeMethods.CredUnPackAuthenticationBuffer(0, outCredBuffer, outCredSize,
                                                                       usernameBuffer,
                                                                       ref maxUsername, domainBuffer,
                                                                       ref maxDomain,
                                                                       passwordBuffer, ref maxPassword);
            if (result)
            {
                NativeMethods.CoTaskMemFree(outCredBuffer);

                Username = usernameBuffer.ToString();
                Password = passwordBuffer.ToString();

                if (passwordBuffer.Length > 0)
                {
                    passwordBuffer.Remove(0, passwordBuffer.Length);
                }
            }

            return DialogResult.OK;
        }

        bool IsWinVistaOrHigher
        {
            get
            {
                OperatingSystem OS = Environment.OSVersion;
                return (OS.Platform == PlatformID.Win32NT) && (OS.Version.Major >= 6);
            }
        }
    }
}

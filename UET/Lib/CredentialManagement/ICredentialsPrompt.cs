using System;
using System.Security;

namespace CredentialManagement
{
    interface ICredentialsPrompt: IDisposable
    {
        DialogResult ShowDialog();
        DialogResult ShowDialog(IntPtr owner);

        string Username { get; set; }
        string Password { get; set; }
        SecureString SecurePassword { get; set; }

        string Title { get; set; }
        string Message { get; set; }
        
        bool SaveChecked { get; set; }

        bool GenericCredentials { get; set; }
        bool ShowSaveCheckBox { get; set; }
        int ErrorCode { get; set; }

    }
}

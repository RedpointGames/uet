namespace Redpoint.Vfs.Windows
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public static class StatusConvert
    {
        public static int ConvertHResultToNTSTATUS(int hresult)
        {
            switch (hresult)
            {
                case 0:
                    return 0;
                case HResultConstants.IoPending:
                    return NTSTATUSConstants.Pending;
                case HResultConstants.EOF:
                    return NTSTATUSConstants.EOF;
                case HResultConstants.OperationAborted:
                    return NTSTATUSConstants.Cancelled;
                default:
                    return hresult;
            }
        }

        public static int ConvertNTSTATUSToHResult(int ntstatus)
        {
            switch (ntstatus)
            {
                case 0:
                    return 0;
                case NTSTATUSConstants.Pending:
                    return HResultConstants.IoPending;
                case NTSTATUSConstants.EOF:
                    return HResultConstants.EOF;
                case NTSTATUSConstants.Cancelled:
                    return HResultConstants.OperationAborted;
                default:
                    return ntstatus;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Tftp.Net.Transfer.States
{
    class Closed : BaseState
    {
        public override async Task OnStateEnter()
        {
            await Context.DisposeAsync();
        }
    }
}

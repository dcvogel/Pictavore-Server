using System;
using System.Runtime.InteropServices;
using ASCOM.Pictavore_4021a;

namespace ASCOM.Pictavore_Server
//namespace ASCOM. Pictavore_4021a
{
    [ComVisible(false)]
    public class ReferenceCountedObjectBase
    {
        public ReferenceCountedObjectBase()
        {
            // We increment the global count of objects.
            Server.CountObject();
        }

        ~ReferenceCountedObjectBase()
        {
            // We decrement the global count of objects.
            Server.UncountObject();
            // We then immediately test to see if we the conditions
            // are right to attempt to terminate this server application.
            Server.ExitIf();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text;

namespace HikvisionGetUsers
{
    class Configuration
    {
        public ushort controller { get; set; }
        public string ip { get; set; }
        public string username { get; set; }
        public string password { get; set; }
        public short port { get; set; }
    }
}

﻿using IntegrationTool.SDK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntegrationTool.Module.ConnectToUrl
{
    public class ConnectToUrlConfiguration : ConnectionConfigurationBase
    {
        public bool UseProxySettings { get; set; }

        public bool SendClientCertificate { get; set; }
    }
}

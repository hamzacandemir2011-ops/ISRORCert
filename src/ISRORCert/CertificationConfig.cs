using System;
using System.Linq;

namespace ISRORCert
{
    internal class CertificationConfig
    {
        public string DbConfig { get; set; } = "";

        /// <summary>
        /// Certificate packet format: "ISROR" (ISROR 2015+, default) or "VSRO188".
        /// </summary>
        public string Version { get; set; } = "ISROR";

        /// <summary>
        /// How often (seconds) a status summary is logged: connected modules and server body/cord states. 0 disables it.
        /// </summary>
        public int StatusIntervalSeconds { get; set; } = 300;
    }
}

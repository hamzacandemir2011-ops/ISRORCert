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
    }
}

using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace Web.Libraries.Start
{
    public class StartConfiguration
    {
        public static IConfiguration configuration;
        public static void Add(IConfiguration in_configuration)
        {
            configuration = in_configuration;
        }
    }
}
